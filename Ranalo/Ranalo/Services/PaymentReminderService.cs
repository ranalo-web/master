using Newtonsoft.Json;
using Ranalo.DataStore;
using Ranalo.Models;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Ranalo.Services
{
    public class PaymentReminderService : IPaymentReminderService
    {
        public PaymentReminderService()
        {
            
        }

        public async Task<List<AccountSendMessage>> RunRemindersAsync(List<AccountSendMessage> records, IPaymentsRepository paymentsRepository)
        {
            // 1️⃣ Use Africa/Nairobi timezone
            var tz = TimeZoneInfo.FindSystemTimeZoneById("E. Africa Standard Time");
            //var nairobiNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
            var nairobiNow = DateTime.Now;

            // 2️⃣ Define reference times
            var timeIn6Hrs = nairobiNow.AddHours(7);
            var timeIn4Hrs = nairobiNow.AddHours(5);
            var timeIn2Hrs = nairobiNow.AddHours(3);
            var timeIn1Hrs = nairobiNow.AddHours(2);
            var timeIn0Hrs = nairobiNow.AddHours(1);


            var allLocks = new List<AccountSendMessage>();

            // 3️⃣ Filter by lock intervals
            var lock6h = records.Where(r => r.AutoLockDatePmtR <= timeIn6Hrs && r.AutoLockDatePmtR >= timeIn4Hrs).ToList();
            var lock4h = records.Where(r => r.AutoLockDatePmtR <= timeIn4Hrs && r.AutoLockDatePmtR >= timeIn2Hrs).ToList();
            var lock2h = records.Where(r => r.AutoLockDatePmtR <= timeIn2Hrs && r.AutoLockDatePmtR >= timeIn1Hrs).ToList();
            var lock1h = records.Where(r => r.AutoLockDatePmtR <= timeIn1Hrs && r.AutoLockDatePmtR >= timeIn0Hrs).ToList();
            var lock0h = records.Where(r => r.AutoLockDatePmtR <= timeIn0Hrs && r.AutoLockDatePmtR >= nairobiNow).ToList();

            // 4️⃣ Process blocks
            await ProcessBlock(lock6h, "Friendly reminder: Your phone will lock in 6 hours if we don't receive your payment. To pay ", paymentsRepository);
            await ProcessBlock(lock4h, "Important reminder: Your phone will lock in 4 hours if we don't receive your payment. To pay ", paymentsRepository);
            await ProcessBlock(lock2h, "Urgent reminder: Your phone will lock in 2 hours if we don't receive your payment. To pay ", paymentsRepository);
            await ProcessBlock(lock1h, "Final notice: Your phone will lock in a few minutes if we don't receive your payment. To pay ", paymentsRepository);
            await ProcessBlock(lock0h, "Final notice: Your phone will lock in a few minutes if we don't receive your payment. To pay ", paymentsRepository);

            allLocks.AddRange(lock6h);
            allLocks.AddRange(lock4h);
            allLocks.AddRange(lock2h);
            allLocks.AddRange(lock1h);
            allLocks.AddRange(lock0h);

            return allLocks;
        }

        private async Task ProcessBlock(List<AccountSendMessage> block, string messageText, IPaymentsRepository paymentsRepository)
        {
            if (block.Count == 0)
            {
                Console.WriteLine("No records in this block.");
                return;
            }

            bool _sendLive = true;

            var df = new List<MessageLog>();

            using (var client = new HttpClient())
            {
                var consumerKey = "Token 8efccf09d4874f88ba2a62f5db8d8efc";
                var authToken = Encoding.ASCII.GetBytes(consumerKey);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.Add("Authorization", consumerKey);
                
                foreach (var rec in block)
                {
                    var name = string.IsNullOrWhiteSpace(rec.FirstName) ? "Customer" : rec.FirstName;
                    var message = $"Dear {name}: {messageText}<br>" +
                                  "Paybill 4090703 <br>" +
                                  $"Account No {rec.AccountId}<br>" +
                                  $"Amount to pay {rec.NewDaily}<br>" +
                                  "For assistance please contact us on: 0772 007 007. We're here to help. " +
                                  "If you've already paid, then no further action is needed. Thank you for being a valued customer!";

                    var payload = new
                    {
                        message_text = message,
                        device_ids = new[] { rec.AccountId }
                    };

                    var jsonBody = JsonConvert.SerializeObject(payload);
                    var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                    if (_sendLive)
                    {
                        
                        // Send POST request (optional mock response)
                        var response = client.PostAsync("https://app.nuovopay.com/dm/api/v1/payment_reminders/send_message.json", content).Result;
                        var resText = response.IsSuccessStatusCode ? response.Content.ReadAsStringAsync().Result : "Response placeholder";

                        df.Add(new MessageLog()
                        {
                            AccountNo = rec.AccountId.ToString(),
                            DateSent = DateTime.UtcNow,
                            Id = Guid.NewGuid(),
                            Message = message,
                            MessageError = resText,
                            MessageStatus = "sent",
                            MessageType = "Reminder",
                            PhoneNumber = ""
                        });
                    }
                    else
                    {
                        Console.WriteLine("----\nTEST payload:\n" + jsonBody + "\n");
                    }
                }
            }

            // Print results
            foreach (var record in df)
            {
                await paymentsRepository.CreateMessageLogAsync(record);
            }
        }


        public async Task<List<AccountSendMessage>> RunPaymentsSummariesAsync(List<AccountSendMessage> records, IPaymentsRepository paymentsRepository)
        {
            try
            {
                await ProcessSummaryBlock(records, paymentsRepository);
            }
            catch (Exception)
            {

                return new List<AccountSendMessage>();
            }

            return records;
        }

        private async Task ProcessSummaryBlock(List<AccountSendMessage> block, IPaymentsRepository paymentsRepository)
        {
            if (block.Count == 0)
            {
                Console.WriteLine("No records in this block.");
                return;
            }

            bool _sendLive = true;

            var df = new List<MessageLog>();
            int totalRows = block.Count();
            int batchSize = 100;

            using (var client = new HttpClient())
            {
                string consumerKey = "Token 8efccf09d4874f88ba2a62f5db8d8efc";

                client.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/json"));

                client.DefaultRequestHeaders.Add("Authorization", consumerKey);

                for (int start = 0; start < totalRows; start += batchSize)
                {
                    int end = Math.Min(start + batchSize, totalRows);

                    // Process one batch
                    var batch = block.Skip(start).Take(batchSize).ToList();

                    Console.WriteLine($"\n---- Processing batch {start} to {end - 1} ----\n");

                    foreach (var rec in batch)
                    {
                        // Build your message (example – replace with your logic)
                        string message = rec.MessageText;

                        var payload = new
                        {
                            message_text = message,
                            device_ids = new[] { rec.AccountId }
                        };

                        string jsonBody = JsonConvert.SerializeObject(payload);
                        var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                        string resText = "TEST MODE - not sent";

                        if (_sendLive)
                        {
                            // Safe-send to API
                            var response = client.PostAsync(
                                "https://app.nuovopay.com/dm/api/v1/payment_reminders/send_message.json",
                                content).Result;

                            resText = response.IsSuccessStatusCode
                                ? response.Content.ReadAsStringAsync().Result
                                : response.StatusCode.ToString();
                        }
                        else
                        {
                            Console.WriteLine($"TEST payload for {rec.AccountId}:\n{jsonBody}\n");
                        }

                        // Log results
                        df.Add(new MessageLog
                        {
                            Id = Guid.NewGuid(),
                            AccountNo = rec.AccountId.ToString(),
                            DateSent = DateTime.UtcNow,
                            Message = message,
                            MessageError = resText,
                            MessageStatus = _sendLive ? "sent" : "test",
                            MessageType = "Summary",
                            PhoneNumber = ""
                        });
                    }
                }
            }

            // Print results
            foreach (var record in df)
            {
                await paymentsRepository.CreateMessageLogAsync(record);
            }
        }

    }

}
