using Ranalo.Models;
using Ranalo.Services;
using static Ranalo.Services.CustomerMessages;

namespace Ranolo.Web.Tests
{
    public class CustomerMessagesTests
    {
        private static readonly DateTime Now = new(2026, 10, 25, 9, 0, 0);

        private static MobileStatusReport Record() => new()
        {
            AccountNo = 12345678,
            FirstName = "Jane",
            Arrears = -150m,
            TotalPaid = 9000m,
            TotalLoan = 20000m,
            Deposit = 4000m,
            LoanBalance = 15000m,
        };

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
            var message = Reminder("Jane", 12345678, 75m, LockReminder.In1Hour);

            Assert.That(message.Title, Does.Contain("about 1 hour"));
            Assert.That(message.Full, Does.Not.Contain("few minutes"));
        }

        [Test]
        public void Reminder_gives_paybill_account_and_amount()
        {
            var message = Reminder("Jane", 12345678, 1234.5m, LockReminder.In6Hours);
            var lines = message.Content.Split('\n');

            Assert.That(lines, Does.Contain("M-Pesa Paybill: 4090703"));
            Assert.That(lines, Does.Contain("Account: 12345678"));
            Assert.That(message.Content, Does.Contain("Ksh 1,234.5"));
        }

        [Test]
        public void No_name_greets_customer_and_names_are_tidied()
        {
            Assert.That(Reminder(" ", 1, 1m, LockReminder.In2Hours).Content, Does.StartWith("Hi Customer,"));
            Assert.That(Reminder("JANE", 1, 1m, LockReminder.In2Hours).Content, Does.StartWith("Hi Jane,"));
            Assert.That(Reminder(" mary ann ", 1, 1m, LockReminder.In2Hours).Content, Does.StartWith("Hi Mary Ann,"));
        }

        [Test]
        public void Payment_received_puts_the_amount_in_the_title()
        {
            var message = PaymentReceived("Jane", 300m, "12345678", "TJK12AB34C", new DateTime(2026, 10, 27, 14, 10, 0));

            Assert.That(message.Title, Is.EqualTo("Thank you! Ksh 300 received"));
            Assert.That(message.Content, Does.Contain("M-Pesa code: TJK12AB34C"));
            // Payment times are Kenya time already: shown as they are.
            Assert.That(message.Content, Does.Contain("Paid: Tue 27 Oct, 2:10 PM"));
        }

        [Test]
        public void Summary_shows_the_lock_time_in_Kenya_time()
        {
            // Faith's phone: Devices says 28/10/2026 09:31 UTC = 12:31 PM in Kenya.
            var message = Summary(Record(), new DateTime(2026, 10, 28, 9, 31, 0), Now.AddMonths(12));

            Assert.That(message.Content, Does.Contain("Phone locks: Wed 28 Oct, 12:31 PM"));
        }

        [Test]
        public void Summary_title_says_behind_or_up_to_date()
        {
            var record = Record();
            var behind = Summary(record, null, Now);
            Assert.That(behind.Title, Is.EqualTo("Your account: Ksh 150 behind"));
            Assert.That(behind.Content, Does.Contain("Behind by: Ksh 150"));

            record.Arrears = 80m;
            var ahead = Summary(record, null, Now);
            Assert.That(ahead.Title, Is.EqualTo("Your account: up to date"));
            Assert.That(ahead.Content, Does.Contain("Paid ahead: Ksh 80"));
        }

        [Test]
        public void Summary_shows_progress_against_the_total()
        {
            Assert.That(Summary(Record(), null, Now).Content, Does.Contain("Paid so far: Ksh 9,000 of 24,000 (38%)"));
        }

        [Test]
        public void Summary_with_nothing_owed_does_not_divide_by_zero()
        {
            var record = new MobileStatusReport { AccountNo = 1, FirstName = "Jane" };

            Assert.That(Summary(record, null, Now).Content, Does.Contain("(0%)"));
        }

        [Test]
        public void Nuovo_and_Knox_get_the_title_first_and_br_line_breaks()
        {
            Assert.That(ForNuovoKnox("a\nb"), Is.EqualTo("a<br>b"));
            Assert.That(ForNuovoKnox(new Message("Title", "a\nb")), Is.EqualTo("Title<br>a<br>b"));
        }

        [Test]
        public void Every_message_fits_a_Transsion_popup()
        {
            var record = Record();
            record.FirstName = "Bartholomew-Christopher";
            var messages = new List<Message>
            {
                Summary(record, Now, Now.AddMonths(12)),
                PaymentReceived(record.FirstName, 123456.78m, "12345678", "TJK12AB34C", Now),
            };
            messages.AddRange(Enum.GetValues<LockReminder>().Select(r => Reminder(record.FirstName, 12345678, 75m, r)));

            Assert.That(messages.Select(m => m.Content.Length), Is.All.LessThanOrEqualTo(ContentLimit));
            Assert.That(messages.Select(m => m.Title.Length), Is.All.LessThanOrEqualTo(TitleLimit));
        }

        [Test]
        public void Long_text_is_clipped_to_the_limit()
        {
            Assert.That(Clip(new string('x', 600), ContentLimit).Length, Is.EqualTo(ContentLimit));
            Assert.That(Clip("short", ContentLimit), Is.EqualTo("short"));
        }
    }
}
