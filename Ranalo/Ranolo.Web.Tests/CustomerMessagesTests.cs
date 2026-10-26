using Ranalo.Models;
using Ranalo.Services;
using static Ranalo.Services.CustomerMessages;

namespace Ranolo.Web.Tests
{
    public class CustomerMessagesTests
    {
        private static readonly DateTime Now = new(2026, 10, 25, 9, 0, 0);

        [TestCase(30, LockReminder.UnderAnHour)]
        [TestCase(60, LockReminder.UnderAnHour)]
        [TestCase(61, LockReminder.In1Hour)]
        [TestCase(120, LockReminder.In1Hour)]
        [TestCase(150, LockReminder.In2Hours)]
        [TestCase(240, LockReminder.In4Hours)]
        [TestCase(360, LockReminder.In6Hours)]
        [TestCase(420, LockReminder.In6Hours)]
        public void Reminder_matches_time_left(int minutesLeft, LockReminder expected)
        {
            Assert.That(ReminderFor(Now.AddMinutes(minutesLeft), Now), Is.EqualTo(expected));
        }

        [TestCase(-1)]
        [TestCase(421)]
        public void No_reminder_once_locked_or_more_than_7_hours_away(int minutesLeft)
        {
            Assert.That(ReminderFor(Now.AddMinutes(minutesLeft), Now), Is.Null);
        }

        [Test]
        public void One_hour_reminder_says_1_hour_not_a_few_minutes()
        {
            var text = LockReminderText("Jane", 12345678, 75m, LockReminder.In1Hour);

            Assert.That(text, Does.Contain("about 1 hour"));
            Assert.That(text, Does.Not.Contain("few minutes"));
            Assert.That(ReminderTitle(LockReminder.In1Hour), Does.Contain("1 hour"));
        }

        [Test]
        public void Reminder_gives_paybill_account_and_amount_on_their_own_lines()
        {
            var lines = LockReminderText("Jane", 12345678, 1234.5m, LockReminder.In6Hours).Split('\n');

            Assert.That(lines, Does.Contain("Paybill: 4090703"));
            Assert.That(lines, Does.Contain("Account: 12345678"));
            Assert.That(lines, Does.Contain("Amount: Ksh 1,234.5"));
        }

        [Test]
        public void No_name_greets_customer()
        {
            Assert.That(LockReminderText(" ", 1, 1m, LockReminder.In2Hours), Does.StartWith("Hi Customer,"));
        }

        [Test]
        public void Nuovo_and_Knox_get_br_line_breaks()
        {
            Assert.That(ForNuovoKnox("a\nb"), Is.EqualTo("a<br>b"));
        }

        [Test]
        public void Every_message_fits_a_Transsion_popup()
        {
            var record = new MobileStatusReport
            {
                AccountNo = 12345678,
                FirstName = "Jane",
                Arrears = -150m,
                TotalPaid = 9000m,
                TotalLoan = 20000m,
                Deposit = 4000m,
                LoanBalance = 15000m,
                NextLockDate = "26/10/2026T09:00:00",
                LastPaymentDate = Now,
                LastPaidMpesa = "TJK12AB34C"
            };

            var texts = new[]
            {
                SummaryText(record, Now.AddMonths(12), Now),
                PaymentReceivedText("Jane", 300m, "12345678", "TJK12AB34C", Now),
                LockReminderText("Jane", 12345678, 75m, LockReminder.UnderAnHour),
            };

            Assert.That(texts.Select(t => t.Length), Is.All.LessThanOrEqualTo(ContentLimit));
            Assert.That(Enum.GetValues<LockReminder>().Select(r => ReminderTitle(r).Length), Is.All.LessThanOrEqualTo(TitleLimit));
        }

        [Test]
        public void Summary_shows_arrears_as_a_positive_amount_owed()
        {
            var record = new MobileStatusReport { AccountNo = 1, FirstName = "Jane", Arrears = -150m };

            Assert.That(SummaryText(record, Now, Now), Does.Contain("Arrears: Ksh 150"));
            record.Arrears = 80m;
            Assert.That(SummaryText(record, Now, Now), Does.Contain("Paid ahead: Ksh 80"));
        }

        [Test]
        public void Summary_with_nothing_owed_does_not_divide_by_zero()
        {
            var record = new MobileStatusReport { AccountNo = 1, FirstName = "Jane" };

            Assert.That(SummaryText(record, Now, Now), Does.Contain("(0%)"));
        }

        [Test]
        public void Long_text_is_clipped_to_the_limit()
        {
            Assert.That(Clip(new string('x', 600), ContentLimit).Length, Is.EqualTo(ContentLimit));
            Assert.That(Clip("short", ContentLimit), Is.EqualTo("short"));
        }
    }
}
