using Newtonsoft.Json;
using Ranalo.DataStore;
using Ranalo.Models;
using Ranalo.PayTrigger;
using Ranalo.PayTrigger.Models;
using Ranalo.SumsungKnox;
using Ranalo.SumsungKnox.Models;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text;

namespace Ranalo.Services
{
    // Lock reminders and payment summaries, by lock provider: Nuovo (its
    // send_message API), Knox (SendMessageAsync) and Transsion (PayTrigger
    // popup). The wording is in CustomerMessages.
    public class PaymentReminderService : IPaymentReminderService
    {
        public const string NuovoMessageUrl = "https://app.nuovopay.com/dm/api/v1/payment_reminders/send_message.json";
        public const string NuovoToken = "Token 8efccf09d4874f88ba2a62f5db8d8efc";

        private readonly IPayTriggerClient _payTrigger;
        private readonly PayTriggerSettings? _popupSettings;

        public PaymentReminderService(IPayTriggerClient payTrigger, IOptions<PayTriggerSettings>? popupSettings = null)
        {
            _payTrigger = payTrigger;
            _popupSettings = popupSettings?.Value;
        }

        // One message per account, each to its own lock provider (Welcome,
        // FullyPaid). Logged per account, sent or failed.
        public async Task SendToEachProviderAsync(List<AccountSendMessage> records, string messageType,
            IPaymentsRepository paymentsRepository, IKnoxGuardClient knoxGuardClient)
        {
            await SendNuovoAsync(records.Where(r => r.LockGroup is not (2 or 3)).ToList(), messageType, paymentsRepository);
            await SendKnoxAsync(records.Where(r => r.LockGroup == 2).ToList(), messageType, paymentsRepository, knoxGuardClient);
            await SendTranssionAsync(records.Where(r => r.LockGroup == 3).ToList(), messageType, paymentsRepository);
        }

        // The three reminder jobs (active lock, auto-restructured,
        // restructured) start together every 2 hours and can list the same
        // phone. Each checks MessageLogs and then sends, so one at a time:
        // the next one sees what the last one logged (2026-11-04, a Knox
        // phone got the same reminder twice 1.5 s apart).
        private static readonly SemaphoreSlim ReminderGate = new(1, 1);

        private static async Task<List<AccountSendMessage>> OneAtATime(List<AccountSendMessage> records,
            IPaymentsRepository paymentsRepository, Func<List<AccountSendMessage>, Task> send)
        {
            await ReminderGate.WaitAsync();
            try
            {
                var due = await DueRemindersAsync(records, paymentsRepository);
                await send(due);
                return due;
            }
            finally
            {
                ReminderGate.Release();
            }
        }

        public async Task<List<AccountSendMessage>> RunRemindersAsync(List<AccountSendMessage> records, IPaymentsRepository paymentsRepository) =>
            await OneAtATime(records, paymentsRepository, due => SendNuovoAsync(due, "Reminder", paymentsRepository));

        public async Task RunRemindersKnoxAsync(List<AccountSendMessage> knoxReminders, IPaymentsRepository paymentsRepository, IKnoxGuardClient knoxGuardClient) =>
            await OneAtATime(knoxReminders, paymentsRepository, due => SendKnoxAsync(due, "Reminder", paymentsRepository, knoxGuardClient));

        public async Task RunRemindersTranssionAsync(List<AccountSendMessage> records, IPaymentsRepository paymentsRepository) =>
            await OneAtATime(records, paymentsRepository, due => SendTranssionAsync(due, "Reminder", paymentsRepository));

        public async Task<List<AccountSendMessage>> RunPaymentsSummariesAsync(List<AccountSendMessage> records, IPaymentsRepository paymentsRepository)
        {
            try
            {
                await SendNuovoAsync(records, "Summary", paymentsRepository);
            }
            catch (Exception)
            {
                return new List<AccountSendMessage>();
            }

            return records;
        }

        public async Task<List<AccountSendMessage>> RunKnoxPaymentsSummariesAsync(List<AccountSendMessage> records,
            IPaymentsRepository paymentsRepository, IKnoxGuardClient knoxGuardClient)
        {
            try
            {
                await SendKnoxAsync(records, "Summary", paymentsRepository, knoxGuardClient);
            }
            catch (Exception)
            {
                return new List<AccountSendMessage>();
            }

            return records;
        }

        public async Task<List<AccountSendMessage>> RunTranssionPaymentsSummariesAsync(List<AccountSendMessage> records, IPaymentsRepository paymentsRepository)
        {
            try
            {
                await SendTranssionAsync(records, "Summary", paymentsRepository);
            }
            catch (Exception)
            {
                return new List<AccountSendMessage>();
            }

            return records;
        }

        // A reminder already sent to an account isn't sent again within this
        // gap (the jobs run every 2 hours). Until 2026-11-04 a failing job
        // re-ran every few seconds and sent the same reminder hundreds of
        // times a day; three jobs can also pick up the same account.
        public static readonly TimeSpan ReminderRepeatGap = TimeSpan.FromMinutes(100);

        // A lock date the lock jobs sent the phone is trusted for this long.
        public static readonly TimeSpan LockDateBookMaxAge = TimeSpan.FromDays(1);

        private static async Task<List<AccountSendMessage>> DueRemindersAsync(List<AccountSendMessage> records, IPaymentsRepository paymentsRepository)
        {
            var now = DateTime.UtcNow;
            var recent = await paymentsRepository.GetSentMessagesSinceAsync("Reminder", now - ReminderRepeatGap);
            return WithReminderText(records, recent, now);
        }

        // The accounts a reminder is due for now, each with its wording, less
        // those already sent that reminder (recent: Reminder logs from the
        // last ReminderRepeatGap). Lock dates are UTC (LockTime). The lock
        // date is the one the phone was last sent (LockDateBook) when known,
        // else the job's own estimate.
        public static List<AccountSendMessage> WithReminderText(List<AccountSendMessage> records, IReadOnlyCollection<MessageLog> recent, DateTime now)
        {
            var due = new List<AccountSendMessage>();
            var picked = new HashSet<long>();
            foreach (var rec in records)
            {
                var sentLock = LockDateBook.Get(rec.AccountId);
                var lockAt = sentLock != null && now - sentLock.SentAtUtc <= LockDateBookMaxAge ? sentLock.LockAtUtc : rec.AutoLockDatePmtR;

                var reminder = CustomerMessages.ReminderFor(lockAt, now);
                if (reminder == null || !picked.Add(rec.AccountId))
                {
                    continue;
                }

                var message = CustomerMessages.Reminder(rec.FirstName, rec.AccountId, rec.NewDaily, reminder.Value);
                if (AlreadySent(rec, message.Title, recent))
                {
                    continue;
                }

                rec.MessageTitle = message.Title;
                rec.MessageText = message.Content;
                due.Add(rec);
            }
            return due;
        }

        // Nuovo and Transsion logs carry the account number, Knox the IMEI;
        // every format starts with the title.
        private static bool AlreadySent(AccountSendMessage rec, string title, IEnumerable<MessageLog> recent) =>
            recent.Any(log =>
                (log.AccountNo == rec.AccountId.ToString() || (!string.IsNullOrEmpty(rec.Imei) && log.AccountNo == rec.Imei))
                && log.Message?.StartsWith(title) == true);

        private static async Task SendNuovoAsync(List<AccountSendMessage> records, string messageType, IPaymentsRepository paymentsRepository)
        {
            if (records.Count == 0)
            {
                return;
            }

            var logs = new List<MessageLog>();
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.Add("Authorization", NuovoToken);

                foreach (var rec in records)
                {
                    var message = CustomerMessages.ForNuovoKnox(WithTitle(rec));
                    var body = JsonConvert.SerializeObject(new
                    {
                        message_text = message,
                        device_ids = new[] { rec.AccountId }
                    });

                    // One failed send is logged and the rest still go.
                    try
                    {
                        var response = await client.PostAsync(NuovoMessageUrl, new StringContent(body, Encoding.UTF8, "application/json"));
                        logs.Add(Log(rec.AccountId.ToString(), message, messageType, response.IsSuccessStatusCode,
                            response.IsSuccessStatusCode ? await response.Content.ReadAsStringAsync() : response.StatusCode.ToString()));
                    }
                    catch (Exception ex)
                    {
                        logs.Add(Log(rec.AccountId.ToString(), message, messageType, false, ex.Message));
                    }
                }
            }

            await SaveLogsAsync(logs, paymentsRepository);
        }

        private static async Task SendKnoxAsync(List<AccountSendMessage> records, string messageType, IPaymentsRepository paymentsRepository, IKnoxGuardClient knoxGuardClient)
        {
            var logs = new List<MessageLog>();
            foreach (var rec in records)
            {
                var message = CustomerMessages.ForKnox(WithTitle(rec));
                // Knox needs the IMEI. Without one, log it under the account
                // (MessageLogs.AccountNo can't be NULL: that insert failing
                // is what stopped the restructured reminder job, 2026-11-04).
                if (string.IsNullOrWhiteSpace(rec.Imei))
                {
                    logs.Add(Log(rec.AccountId.ToString(), message, messageType, false, "No IMEI for this Knox phone; not sent."));
                    continue;
                }

                // One failed send (a token or network error) is logged and
                // the rest still go.
                try
                {
                    var response = await knoxGuardClient.SendMessageAsync(new SendMessageRequest()
                    {
                        DeviceUid = rec.Imei,
                        Message = message,
                        Tel = "0001112233444"
                    });

                    var reply = await response.Content.ReadAsStringAsync();
                    logs.Add(Log(rec.Imei, message, messageType, response.IsSuccessStatusCode && KnoxReply.Accepted(reply), reply));
                }
                catch (Exception ex)
                {
                    logs.Add(Log(rec.Imei, message, messageType, false, ex.Message));
                }
            }

            await SaveLogsAsync(logs, paymentsRepository);
        }

        private async Task SendTranssionAsync(List<AccountSendMessage> records, string messageType, IPaymentsRepository paymentsRepository)
        {
            var logs = new List<MessageLog>();
            foreach (var rec in records)
            {
                var message = CustomerMessages.Clip(rec.MessageText ?? "", CustomerMessages.ContentLimit);
                string result;
                bool sent;
                try
                {
                    var response = await _payTrigger.SendPushInfoAsync(PayTriggerPopup.For(rec.Imei,
                        new CustomerMessages.Message(rec.MessageTitle ?? CustomerMessages.SignOff, rec.MessageText ?? ""), _popupSettings));
                    sent = response.IsSuccess;
                    result = $"{response.Code} {response.Message}";
                }
                catch (Exception ex)
                {
                    sent = false;
                    result = ex.Message;
                }

                // Logged with its title, like Nuovo and Knox, so a repeat can be spotted (AlreadySent).
                var logged = string.IsNullOrEmpty(rec.MessageTitle) ? message : new CustomerMessages.Message(rec.MessageTitle, message).Full;
                logs.Add(Log(rec.AccountId.ToString(), logged, messageType, sent, result));
            }

            await SaveLogsAsync(logs, paymentsRepository);
        }

        // Nuovo and Knox have no title: it goes first, as its own line.
        // The text still has "\n"; each provider formats it (ForNuovoKnox, ForKnox).
        private static string WithTitle(AccountSendMessage rec) =>
            string.IsNullOrEmpty(rec.MessageTitle) ? rec.MessageText ?? "" : new CustomerMessages.Message(rec.MessageTitle, rec.MessageText ?? "").Full;

        private static MessageLog Log(string? accountNo, string message, string messageType, bool sent, string result) => new()
        {
            Id = Guid.NewGuid(),
            AccountNo = accountNo,
            DateSent = DateTime.UtcNow,
            Message = message,
            MessageError = result,
            MessageStatus = sent ? "sent" : "failed",
            MessageType = messageType,
            PhoneNumber = ""
        };

        private static async Task SaveLogsAsync(List<MessageLog> logs, IPaymentsRepository paymentsRepository)
        {
            foreach (var log in logs)
            {
                await paymentsRepository.CreateMessageLogAsync(log);
            }
        }
    }
}
