using Google.Protobuf;
using Newtonsoft.Json;
using Ranalo.Calculator.Logic.Models;
using Ranalo.DataStore;
using Ranalo.Models;
using Ranalo.PayTrigger;
using Ranalo.PayTrigger.Models;
using Ranalo.Services;
using Ranalo.SumsungKnox;
using Ranalo.SumsungKnox.Models;
using Microsoft.Extensions.Options;
using Ranalo.Woocommece.Api.Services;
using System.Net.Http.Headers;
using System.Text;

namespace Ranalo.ScheduledServices
{
    public class ScheduledSendPaymentMessages : BackgroundService
    {
        private readonly ILogger<ScheduledSendPaymentMessages> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly TimeSpan _interval = TimeSpan.FromMinutes(10); // Run every 30 min

        public ScheduledSendPaymentMessages(ILogger<ScheduledSendPaymentMessages> logger,
            IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Send Payment messages started: {time}", DateTime.UtcNow);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Example: perform a database operation
                    _logger.LogInformation("Running scheduled task Send Payment messages at: {time}", DateTime.UtcNow);

                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var syncService = scope.ServiceProvider.GetRequiredService<IPaymentsRepository>();
                        var knoxGuardClient = scope.ServiceProvider.GetRequiredService<IKnoxGuardClient>();
                        var payTrigger = scope.ServiceProvider.GetRequiredService<IPayTriggerClient>();
                        var popupSettings = scope.ServiceProvider.GetService<IOptions<PayTriggerSettings>>()?.Value;
                        var inactiveUsers = await SendAsync(syncService, knoxGuardClient, payTrigger, popupSettings);
                        foreach (var order in inactiveUsers)
                        {
                            _logger.LogInformation("Sent payment message for : {AccountNo} with MpesaCode : {PhoneNumber}", order.AccountNo, order.PhoneNumber);
                            // Possibly send email reminders, clean up data, etc.
                        }
                    }

                    // Wait until next run
                    await Task.Delay(_interval, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    // Ignore when shutting down
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while running scheduled task Create contracts.");
                }
            }

            _logger.LogInformation("ScheduledTaskCreateContractOrders stopped at: {time}", DateTime.UtcNow);
        }



        public async Task<List<MessageLog>> SendAsync(IPaymentsRepository paymentRepository, IKnoxGuardClient knoxGuardClient, IPayTriggerClient payTrigger,
            PayTriggerSettings? popupSettings = null)
        {

            var df = new List<MessageLog>();
            var dtPayments = await paymentRepository.GetAllPaymentsForMessagesAsync();

            if (dtPayments == null) return df;

            // A receipt waits (unlogged, so it comes back next run) until the
            // lock jobs have counted the payment, to say "unlocked until".
            dtPayments = ReadyToSend(dtPayments, DateTime.UtcNow);

            var nouvaPayments = dtPayments
                .Where(m => m.LockGroup == 1)
                .ToList();

            var knoxPayments = dtPayments
                .Where(m => m.LockGroup == 2)
                .ToList();

            if(nouvaPayments.Any())
            {
                SendNouvaPaymentMessages(paymentRepository, df, nouvaPayments);
            }

            if(knoxPayments.Any())
            {
                await SendKnoxPaymentMessages(knoxGuardClient, paymentRepository, df, knoxPayments);
            }

            var transsionPayments = dtPayments
                .Where(m => m.LockGroup == 3)
                .ToList();

            if (transsionPayments.Any())
            {
                await SendTranssionPaymentMessages(payTrigger, popupSettings, df, transsionPayments, DateTime.Now);
            }

            // Print results
            foreach (var record in df)
            {
                await paymentRepository.CreateMessageLogAsync(record);
            }

            return df;
        }

        // The payments whose receipt can go now, each with its "unlocked
        // until" date when known (PaymentReceipt.Decide).
        public static List<PaymentMessage> ReadyToSend(List<PaymentMessage> payments, DateTime nowUtc)
        {
            var ready = new List<PaymentMessage>();
            foreach (var payment in payments)
            {
                var lastLock = long.TryParse(payment.AccountNo, out var accountId) ? LockDateBook.Get(accountId) : null;
                var (timing, unlockedUntil) = PaymentReceipt.Decide(payment.ReceivedAtUtc, lastLock, nowUtc);
                if (timing == PaymentReceipt.Timing.Send)
                {
                    payment.UnlockedUntilUtc = unlockedUntil;
                    ready.Add(payment);
                }
            }
            return ready;
        }

        private async Task SendKnoxPaymentMessages(IKnoxGuardClient knoxGuardClient, IPaymentsRepository paymentRepository, List<MessageLog> df, List<PaymentMessage> knoxPayments)
        {

            foreach (var payment in knoxPayments)
            {
                string str_message = MessageBody(payment.FirstName,
                payment.AmountValue,
                payment.AccountNo,
                payment.MpesaCode,
                payment.PaymentDateValue,
                payment.UnlockedUntilUtc);

                var message = new SendMessageRequest()
                {
                    DeviceUid = payment.Imei,
                    Message = str_message,
                    Tel = "0001112233444"

                };
                var messageResponse = await knoxGuardClient.SendMessageAsync(message);
                var responseString = await messageResponse.Content.ReadAsStringAsync();
                df.Add(new MessageLog()
                {
                    AccountNo = payment.AccountNo,
                    DateSent = DateTime.UtcNow,
                    Id = Guid.NewGuid(),
                    Message = str_message,
                    MessageError = responseString,
                    MessageStatus = "sent",
                    MessageType = "Payment",
                    PhoneNumber = payment.MpesaCode
                });
            }
        }

        // Transsion: a PayTrigger popup. Payments only drop out of this job
        // once logged (MessageId), and Transsion ones never were before
        // 2026-10-25 -- so any older than a day are logged as skipped, not
        // sent, rather than popping up weeks-old payments.
        public static readonly TimeSpan TranssionPaymentMaxAge = TimeSpan.FromDays(1);

        private static async Task SendTranssionPaymentMessages(IPayTriggerClient payTrigger, PayTriggerSettings? popupSettings, List<MessageLog> df, List<PaymentMessage> transsionPayments, DateTime now)
        {
            foreach (var payment in transsionPayments)
            {
                var message = CustomerMessages.PaymentReceived(payment.FirstName, payment.AmountValue,
                    payment.AccountNo, payment.MpesaCode, payment.PaymentDateValue, payment.UnlockedUntilUtc);
                var text = message.Full;

                string status;
                string result;
                if (now - payment.PaymentDateValue > TranssionPaymentMaxAge)
                {
                    status = "skipped";
                    result = "Older than a day when Transsion messages started; not sent.";
                }
                else
                {
                    try
                    {
                        var response = await payTrigger.SendPushInfoAsync(PayTriggerPopup.For(payment.Imei, message, popupSettings));
                        status = response.IsSuccess ? "sent" : "failed";
                        result = $"{response.Code} {response.Message}";
                    }
                    catch (Exception ex)
                    {
                        status = "failed";
                        result = ex.Message;
                    }
                }

                df.Add(new MessageLog()
                {
                    AccountNo = payment.AccountNo,
                    DateSent = DateTime.UtcNow,
                    Id = Guid.NewGuid(),
                    Message = text,
                    MessageError = result,
                    MessageStatus = status,
                    MessageType = "Payment",
                    PhoneNumber = payment.MpesaCode
                });
            }
        }

        private void SendNouvaPaymentMessages(IPaymentsRepository paymentRepository, List<MessageLog> df, List<PaymentMessage> nouvaPayments)
        {
            var validatedList = nouvaPayments.Select(p => new
            {
                account_no = (string.IsNullOrWhiteSpace(p.AccountNo) ||
                              !long.TryParse(p.AccountNo, out _)) ? "7" : p.AccountNo,
                amount = (p.AmountValue < 0) ? 0 : p.AmountValue,
                payment_date = p.PaymentDateValue,
                mpesa_code = p.MpesaCode,
                First_Name = p.FirstName,
                unlocked_until = p.UnlockedUntilUtc
            }).ToList();

            if (!validatedList.Any())
            {
                _logger.LogWarning("No records found in the merged dataframe. Adding 1 + 1 to log errors.");
            }
            else
            {

                using (var client = new HttpClient())
                {
                    var consumerKey = "Token 8efccf09d4874f88ba2a62f5db8d8efc";
                    var authToken = Encoding.ASCII.GetBytes(consumerKey);
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    client.DefaultRequestHeaders.Add("Authorization", consumerKey);

                    foreach (var row in validatedList)
                    {
                        string str_message = MessageBody(row.First_Name, row.amount, row.account_no, row.mpesa_code, row.payment_date, row.unlocked_until);

                        var body_msg = new
                        {
                            message_text = str_message,
                            device_ids = new[] { row.account_no }
                        };

                        var jsonBody = JsonConvert.SerializeObject(body_msg);
                        var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                        // Send POST request (optional mock response)
                        var response = client.PostAsync("https://app.nuovopay.com/dm/api/v1/payment_reminders/send_message.json", content).Result;
                        var resText = response.IsSuccessStatusCode ? response.Content.ReadAsStringAsync().Result : "Response placeholder";

                        df.Add(new MessageLog()
                        {
                            AccountNo = row.account_no,
                            DateSent = DateTime.UtcNow,
                            Id = Guid.NewGuid(),
                            Message = str_message,
                            MessageError = resText,
                            MessageStatus = "sent",
                            MessageType = "Payment",
                            PhoneNumber = row.mpesa_code
                        });
                    }
                }

            }
        }

        private static string MessageBody(string? firstName, decimal amount, string? accountNo, string? mpesa, DateTime paymentDate, DateTime? unlockedUntilUtc)
        {
            return CustomerMessages.ForNuovoKnox(CustomerMessages.PaymentReceived(firstName, amount, accountNo, mpesa, paymentDate, unlockedUntilUtc));
        }

        private static List<dynamic> LoadOrphanedPayments()
        {
            // Replace this with actual load logic
            return new List<dynamic>
        {
            new { mpesa_code = "ABC123", Orphaned_Acc_No = "10", Parent_Acc_No = "20" }
        };
        }

        private static List<ContractInfo> LoadContractInfo()
        {
            // Replace this with actual load logic
            return new List<ContractInfo>
        {
            new ContractInfo { ID = 20, FirstName = "John" }
        };
        }
    }

    public class PaymentRecord
    {
        public string mpesa_code { get; set; }
        public string Orphaned_Acc_No { get; set; }
        public string Parent_Acc_No { get; set; }
        public string account_no { get; set; }
        public decimal amount { get; set; }
        public DateTime payment_date { get; set; }
        public string First_Name { get; set; }
    }
}

