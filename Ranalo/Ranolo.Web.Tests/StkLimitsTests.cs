using Ranalo.Models;
using Ranalo.Services;

namespace Ranolo.Web.Tests
{
    // Who the app's M-Pesa prompt may go to, and how often (2026-11-07).
    public class StkLimitsTests
    {
        private static readonly DateTime Now = new(2026, 11, 7, 20, 0, 0, DateTimeKind.Utc);
        private const long Account = 1234567;
        private const string Own = "254712000001";

        private static StkRules.Sent S(string phone, int minutesAgo, long account = Account) =>
            new(account, phone, Now.AddMinutes(-minutesAgo));

        private static string? Check(string phone, params StkRules.Sent[] recent) =>
            StkRules.Refusal(Account, phone, "0712000001", 100, 5000m, recent, Now);

        [Test]
        public void First_prompt_to_own_number_is_fine() => Assert.That(Check(Own), Is.Null);

        [Test]
        public void Another_number_is_fine_too() => Assert.That(Check("254722000002"), Is.Null);

        [Test]
        public void One_a_minute() =>
            Assert.That(Check(Own, S(Own, 0)), Does.Contain("just sent"));

        [Test]
        public void Three_an_hour() =>
            Assert.That(Check(Own, S(Own, 10), S(Own, 20), S(Own, 30)), Does.Contain("last hour"));

        [Test]
        public void Six_a_day() =>
            Assert.That(Check(Own, S(Own, 70), S(Own, 140), S(Own, 210), S(Own, 280), S(Own, 350), S(Own, 420)), Does.Contain("today"));

        [Test]
        public void Older_than_a_day_does_not_count() =>
            Assert.That(Check(Own, S(Own, 1500), S(Own, 1510), S(Own, 1520), S(Own, 1530), S(Own, 1540), S(Own, 1550)), Is.Null);

        [Test]
        public void At_most_two_other_numbers_a_day()
        {
            var recent = new[] { S("254722000002", 120), S("254733000003", 180) };
            Assert.That(Check("254744000004", recent), Does.Contain("2 other numbers"));
            Assert.That(Check("254722000002", recent), Is.Null, "a number already used today is fine again");
            Assert.That(Check(Own, recent), Is.Null, "the customer's own number always is");
        }

        [Test]
        public void One_number_by_at_most_two_accounts_a_day()
        {
            var target = "254755000005";
            var recent = new[] { S(target, 100, 111), S(target, 200, 222) };
            Assert.That(Check(target, recent), Does.Contain("other accounts"));
            Assert.That(Check(target, S(target, 100, 111)), Is.Null);
        }

        [Test]
        public void Not_more_than_the_balance()
        {
            Assert.That(StkRules.Refusal(Account, Own, Own, 6000, 5000m, Array.Empty<StkRules.Sent>(), Now), Does.Contain("more than what's left"));
            Assert.That(StkRules.Refusal(Account, Own, Own, 5000, 4999.5m, Array.Empty<StkRules.Sent>(), Now), Is.Null, "rounded up to whole shillings");
            Assert.That(StkRules.Refusal(Account, Own, Own, 6000, null, Array.Empty<StkRules.Sent>(), Now), Is.Null, "balance unknown: no cap");
        }

        [Test]
        public void Prompt_stays_off_with_a_short_callback_token()
        {
            var s = new SafaricomStkSettings
            {
                Enabled = true, ConsumerKey = "k", ConsumerSecret = "s", Passkey = "p",
                CallbackBaseUrl = "https://ops.example.com", CallbackToken = new string('a', 30),
            };
            Assert.That(s.Ready, Is.False);
            s.CallbackToken = new string('a', SafaricomCallerRules.MinTokenLength);
            Assert.That(s.Ready, Is.True);
        }
    }
}
