using Ranalo.Models;
using Ranalo.Services;
using static Ranalo.Services.CustomerMessages;

namespace Ranolo.Web.Tests
{
    // PaymentReminderService.WithReminderText: which accounts get a lock
    // reminder this run (2026-11-04, after the same reminder went out
    // hundreds of times a day).
    public class ReminderRepeatTests
    {
        private static readonly DateTime Now = new(2026, 11, 2, 8, 0, 0, DateTimeKind.Utc);

        [SetUp]
        public void Reset() => LockDateBook.Clear();

        private static AccountSendMessage Account(long id = 8499170, double hoursLeft = 0.5, string? imei = null) => new()
        {
            AccountId = id,
            FirstName = "Mark",
            NewDaily = 74.83m,
            AutoLockDatePmtR = Now.AddHours(hoursLeft),
            Imei = imei,
        };

        private static MessageLog Sent(string accountNo, string message) =>
            new() { AccountNo = accountNo, Message = message, MessageType = "Reminder", MessageStatus = "sent", DateSent = Now.AddMinutes(-20) };

        private static string Title(LockReminder reminder) => ReminderTitle(reminder);

        [Test]
        public void A_reminder_not_sent_yet_goes()
        {
            var due = PaymentReminderService.WithReminderText(new() { Account() }, new List<MessageLog>(), Now);

            Assert.That(due.Select(d => d.MessageTitle), Is.EqualTo(new[] { Title(LockReminder.UnderAnHour) }));
        }

        [Test]
        public void The_same_reminder_already_sent_is_not_sent_again()
        {
            var nuovo = Sent("8499170", ForNuovoKnox(Title(LockReminder.UnderAnHour) + "\n\nHi Mark"));
            var knox = Sent("351065613492352", ForKnox(Title(LockReminder.UnderAnHour) + "\n\nHi Mark"));

            Assert.That(PaymentReminderService.WithReminderText(new() { Account() }, new List<MessageLog> { nuovo }, Now), Is.Empty);
            Assert.That(PaymentReminderService.WithReminderText(new() { Account(imei: "351065613492352") }, new List<MessageLog> { knox }, Now), Is.Empty);
        }

        [Test]
        public void The_next_reminder_still_goes()
        {
            var earlier = Sent("8499170", ForNuovoKnox(Title(LockReminder.In2Hours) + "\n\nHi Mark"));

            var due = PaymentReminderService.WithReminderText(new() { Account() }, new List<MessageLog> { earlier }, Now);

            Assert.That(due, Has.Count.EqualTo(1));
        }

        [Test]
        public void Another_account_is_not_held_back()
        {
            var other = Sent("7623909", ForNuovoKnox(Title(LockReminder.UnderAnHour) + "\n\nHi Felix"));

            Assert.That(PaymentReminderService.WithReminderText(new() { Account() }, new List<MessageLog> { other }, Now), Has.Count.EqualTo(1));
        }

        [Test]
        public void An_account_listed_twice_gets_one_reminder()
        {
            var due = PaymentReminderService.WithReminderText(new() { Account(), Account() }, new List<MessageLog>(), Now);

            Assert.That(due, Has.Count.EqualTo(1));
        }

        [Test]
        public void Uses_the_lock_date_the_phone_was_sent()
        {
            // The job's estimate says under an hour; the phone was sent 6 hours.
            LockDateBook.Record(8499170, Now.AddHours(6), Now.AddMinutes(-10));

            var due = PaymentReminderService.WithReminderText(new() { Account(hoursLeft: 0.5) }, new List<MessageLog>(), Now);

            Assert.That(due.Single().MessageTitle, Is.EqualTo(Title(LockReminder.In6Hours)));
        }

        [Test]
        public void An_old_lock_date_is_not_trusted()
        {
            LockDateBook.Record(8499170, Now.AddHours(6), Now.AddDays(-2));

            var due = PaymentReminderService.WithReminderText(new() { Account(hoursLeft: 0.5) }, new List<MessageLog>(), Now);

            Assert.That(due.Single().MessageTitle, Is.EqualTo(Title(LockReminder.UnderAnHour)));
        }
    }
}
