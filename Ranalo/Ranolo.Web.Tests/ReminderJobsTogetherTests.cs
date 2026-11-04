using System.Net;
using System.Text;
using Ranalo.DataStore;
using Ranalo.Models;
using Ranalo.Services;
using Ranalo.SumsungKnox;
using Ranalo.SumsungKnox.Models;

namespace Ranolo.Web.Tests
{
    // The three reminder jobs start together and can list the same phone
    // (2026-11-04: a Knox phone got the same reminder twice 1.5 s apart).
    public class ReminderJobsTogetherTests
    {
        [SetUp]
        public void Reset() => LockDateBook.Clear();

        [Test]
        public async Task Two_jobs_at_once_send_a_phone_one_reminder()
        {
            var logs = new List<MessageLog>();
            var (repo, _) = InterfaceFake<IPaymentsRepository>.Create(new()
            {
                ["GetSentMessagesSinceAsync"] = _ => Slow(() => { lock (logs) return logs.ToList(); }),
                ["CreateMessageLogAsync"] = a => { lock (logs) logs.Add((MessageLog)a![0]!); return Task.CompletedTask; },
            });
            var sends = 0;
            var (knox, _) = InterfaceFake<IKnoxGuardClient>.Create(new()
            {
                ["SendMessageAsync"] = _ => Slow(() =>
                {
                    Interlocked.Increment(ref sends);
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("{\"result\":\"SUCCESS\"}", Encoding.UTF8, "application/json"),
                    };
                }),
            });
            var service = new PaymentReminderService(null!);

            List<AccountSendMessage> John() => new()
            {
                new() { AccountId = 41090677, FirstName = "John", NewDaily = 100m, Imei = "358179230492647",
                        LockGroup = 2, AutoLockDatePmtR = DateTime.UtcNow.AddHours(5.5) },
            };

            await Task.WhenAll(
                service.RunRemindersKnoxAsync(John(), repo, knox),
                service.RunRemindersKnoxAsync(John(), repo, knox));

            Assert.That(sends, Is.EqualTo(1));
            Assert.That(logs, Has.Count.EqualTo(1));
        }

        // Answers after a moment, so the two jobs overlap.
        private static async Task<T> Slow<T>(Func<T> answer)
        {
            await Task.Delay(30);
            return answer();
        }
    }
}
