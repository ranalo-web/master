using Newtonsoft.Json;
using Ranalo.DataStore;
using Ranalo.Models;
using Ranalo.PayTrigger;
using Ranalo.PayTrigger.Models;
using Ranalo.SumsungKnox;
using Ranalo.SumsungKnox.Models;
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

        // PayTrigger pushType: 1 = popup.
        private const int Popup = 1;

        private readonly IPayTriggerClient _payTrigger;

        public PaymentReminderService(IPayTriggerClient payTrigger)
        {
            _payTrigger = payTrigger;
        }

        public async Task<List<AccountSendMessage>> RunRemindersAsync(List<AccountSendMessage> records, IPaymentsRepository paymentsRepository)
        {
            var due = WithReminderText(records);
            await SendNuovoAsync(due, "Reminder", paymentsRepository);
            return due;
        }

        public Task RunRemindersKnoxAsync(List<AccountSendMessage> knoxReminders, IPaymentsRepository paymentsRepository, IKnoxGuardClient knoxGuardClient) =>
            SendKnoxAsync(WithReminderText(knoxReminders), "Reminder", paymentsRepository, knoxGuardClient);

        public Task RunRemindersTranssionAsync(List<AccountSendMessage> records, IPaymentsRepository paymentsRepository) =>
            SendTranssionAsync(WithReminderText(records), "Reminder", paymentsRepository);

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

        // The accounts a reminder is due for now, each with its wording.
        // Lock dates are on the server's clock (DateTime.Now), as before.
        private static List<AccountSendMessage> WithReminderText(List<AccountSendMessage> records)
        {
            var now = DateTime.Now;
            var due = new List<AccountSendMessage>();
            foreach (var rec in records)
            {
                var reminder = CustomerMessages.ReminderFor(rec.AutoLockDatePmtR, now);
                if (reminder == null)
                {
                    continue;
                }

                rec.MessageTitle = CustomerMessages.ReminderTitle(reminder.Value);
                rec.MessageText = CustomerMessages.LockReminderText(rec.FirstName, rec.AccountId, rec.NewDaily, reminder.Value);
                due.Add(rec);
            }
            return due;
        }

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
                    var message = CustomerMessages.ForNuovoKnox(rec.MessageText ?? "");
                    var body = JsonConvert.SerializeObject(new
                    {
                        message_text = message,
                        device_ids = new[] { rec.AccountId }
                    });

                    var response = await client.PostAsync(NuovoMessageUrl, new StringContent(body, Encoding.UTF8, "application/json"));
                    logs.Add(Log(rec.AccountId.ToString(), message, messageType, response.IsSuccessStatusCode,
                        response.IsSuccessStatusCode ? await response.Content.ReadAsStringAsync() : response.StatusCode.ToString()));
                }
            }

            await SaveLogsAsync(logs, paymentsRepository);
        }

        private static async Task SendKnoxAsync(List<AccountSendMessage> records, string messageType, IPaymentsRepository paymentsRepository, IKnoxGuardClient knoxGuardClient)
        {
            var logs = new List<MessageLog>();
            foreach (var rec in records)
            {
                var message = CustomerMessages.ForNuovoKnox(rec.MessageText ?? "");
                var response = await knoxGuardClient.SendMessageAsync(new SendMessageRequest()
                {
                    DeviceUid = rec.Imei,
                    Message = message,
                    Tel = "0001112233444"
                });

                logs.Add(Log(rec.Imei, message, messageType, response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync()));
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
                    var response = await _payTrigger.SendPushInfoAsync(new SendPushInfoRequest
                    {
                        Imei = rec.Imei,
                        Title = CustomerMessages.Clip(rec.MessageTitle ?? CustomerMessages.SignOff, CustomerMessages.TitleLimit),
                        Content = message,
                        PushType = Popup
                    });
                    sent = response.IsSuccess;
                    result = $"{response.Code} {response.Message}";
                }
                catch (Exception ex)
                {
                    sent = false;
                    result = ex.Message;
                }

                logs.Add(Log(rec.AccountId.ToString(), message, messageType, sent, result));
            }

            await SaveLogsAsync(logs, paymentsRepository);
        }

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
