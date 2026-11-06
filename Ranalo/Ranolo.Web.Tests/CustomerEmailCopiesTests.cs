using Ranalo.DataStore;
using Ranalo.Models;
using Ranalo.ScheduledServices;
using Ranalo.Services;

namespace Ranolo.Web.Tests
{
    // Email copies of customer messages (2026-11-06).
    public class CustomerEmailCopiesTests
    {
        private sealed class FakeEmail : IEmailSender
        {
            public List<(string To, string Subject, string Body)> Sent { get; } = new();
            public HashSet<string> FailFor { get; } = new();
            public bool Ready => true;
            public Task SendAsync(string to, string subject, string body)
            {
                if (FailFor.Contains(to)) throw new InvalidOperationException("SMTP down");
                Sent.Add((to, subject, body));
                return Task.CompletedTask;
            }
        }

        private static MessageToEmail Msg(long account, string email, int minute) => new()
        {
            AccountNo = account, Email = email, MessageType = "Payment", MessageStatus = "sent",
            Message = $"Payment received: Ksh {minute}<br><br>Thank you.", DateSent = new DateTime(2026, 11, 6, 9, minute, 0),
        };

        [Test]
        public async Task Each_message_is_emailed_and_marked_and_a_failure_waits_for_next_run()
        {
            var upTo = new Dictionary<long, DateTime>();
            var (repo, _) = InterfaceFake<ICustomerAccessRepository>.Create(new()
            {
                ["GetMessagesToEmailAsync"] = _ => Task.FromResult(new List<MessageToEmail>
                {
                    Msg(1, "a@x.co", 1), Msg(2, "b@x.co", 2), Msg(1, "a@x.co", 3), Msg(2, "b@x.co", 4),
                }),
                ["SetEmailedUpToAsync"] = a => { upTo[(long)a![0]!] = (DateTime)a[1]!; return Task.CompletedTask; },
            });
            var email = new FakeEmail();
            email.FailFor.Add("b@x.co");

            var sent = await new ScheduledCustomerEmailCopies(Microsoft.Extensions.Logging.Abstractions.NullLogger<ScheduledCustomerEmailCopies>.Instance, null!).RunAsync(repo, email, "support@ranalocredit.com");

            Assert.That(sent, Is.EqualTo(2));
            Assert.That(email.Sent.Select(s => s.Subject), Is.EqualTo(new[] { "Ranalo: Payment received: Ksh 1", "Ranalo: Payment received: Ksh 3" }));
            Assert.That(upTo[1], Is.EqualTo(new DateTime(2026, 11, 6, 9, 3, 0)));
            Assert.That(upTo.ContainsKey(2), Is.False);
            Assert.That(email.Sent[0].Body, Does.Contain("support@ranalocredit.com"));
            Assert.That(email.Sent[0].Body, Does.Not.Contain("<br>"));
        }
    }
}
