using System.Globalization;
using Ranalo.Models;

namespace Ranalo.Services
{
    // Every message we send to a customer's phone, in one place (2026-10-25,
    // reworded 2026-10-27). Each is a title plus a few short lines, built with
    // "\n": Transsion shows them as a popup (title + content), Nuovo and Knox
    // have no title so get both as one text with <br> (ForNuovoKnox). Every
    // message that asks for money gives the M-Pesa menu path (HowToPay). Kept
    // free of I/O so it can be unit tested.
    public static class CustomerMessages
    {
        public const string Paybill = "4090703";
        public const string HelpLine = "0772 007 007";
        public const string SignOff = "Ranalo Credit";

        // PayTrigger's limits for a popup.
        public const int TitleLimit = 80;
        public const int ContentLimit = 500;

        // Lock dates (Devices, AutoLockDate) are UTC; M-Pesa payment times
        // (KosePayments.PaymentDateValue) are already Kenya time. Kenya has
        // no daylight saving.
        public static readonly TimeSpan KenyaOffset = TimeSpan.FromHours(3);

        public record Message(string Title, string Content)
        {
            // One text for Nuovo and Knox, which have no title: the title,
            // a blank line, then the message.
            public string Full => Title + "\n\n" + Content;
        }

        public enum LockReminder { In6Hours, In4Hours, In2Hours, In1Hour, UnderAnHour }

        // Which reminder is due for a phone locking at lockAt. The reminder
        // jobs run every 2 hours; each band is the window a run can catch
        // the phone in. null = locks later than 7 hours away, or already past.
        public static LockReminder? ReminderFor(DateTime lockAt, DateTime now)
        {
            var left = lockAt - now;
            if (left < TimeSpan.Zero) return null;
            if (left <= TimeSpan.FromHours(1)) return LockReminder.UnderAnHour;
            if (left <= TimeSpan.FromHours(2)) return LockReminder.In1Hour;
            if (left <= TimeSpan.FromHours(3)) return LockReminder.In2Hours;
            if (left <= TimeSpan.FromHours(5)) return LockReminder.In4Hours;
            if (left <= TimeSpan.FromHours(7)) return LockReminder.In6Hours;
            return null;
        }

        public static string ReminderTitle(LockReminder reminder) => reminder switch
        {
            LockReminder.In6Hours => "Reminder: your phone locks in about 6 hours",
            LockReminder.In4Hours => "Reminder: your phone locks in about 4 hours",
            LockReminder.In2Hours => "Urgent: your phone locks in about 2 hours",
            LockReminder.In1Hour => "Final notice: your phone locks in about 1 hour",
            _ => "Final notice: your phone locks in under an hour",
        };

        // Time left, not the clock time: "about 2 hours" can't be misread
        // across time zones or if the popup is opened late. amount is the
        // daily rate; weekly and monthly payers can have 0 there.
        public static Message Reminder(string? firstName, long accountNo, decimal amount, LockReminder reminder)
        {
            var ask = amount > 0
                ? $"Hi {Name(firstName)}, pay Ksh {Money(amount)} now to keep your phone working for another day. Pay more to stay unlocked longer."
                : $"Hi {Name(firstName)}, make a payment now to keep your phone working.";

            var lines = new List<string> { ask, "" };
            lines.AddRange(HowToPay(accountNo));
            lines.Add("");
            lines.Add("Already paid? You can ignore this.");
            lines.Add(Help);

            return new(ReminderTitle(reminder), Lines(lines.ToArray()));
        }

        // unlockedUntilUtc: the phone's new lock date once this payment has
        // been counted (LockDateBook), or null when that isn't known yet.
        public static Message PaymentReceived(string? firstName, decimal amount, string? accountNo, string? mpesaCode,
            DateTime paidAtKenyaTime, DateTime? unlockedUntilUtc = null)
        {
            var lines = new List<string>
            {
                $"Thank you, {Name(firstName)}. We've received your payment.",
                "",
                $"Amount: Ksh {Money(amount)}",
                $"M-Pesa code: {mpesaCode}",
                $"Account no: {accountNo}",
                $"Paid: {When(paidAtKenyaTime)}",
                "",
            };
            if (unlockedUntilUtc != null)
            {
                lines.Add($"Phone unlocked until: {When(unlockedUntilUtc.Value + KenyaOffset)}");
                lines.Add("Pay again before then to keep it working.");
            }
            else
            {
                lines.Add("Keep paying daily to keep your phone working.");
            }
            lines.Add("");
            lines.Add(Help);
            lines.Add(SignOff);

            return new($"Payment received: Ksh {Money(amount)}", Lines(lines.ToArray()));
        }

        // Once, when the contract starts and the phone is enrolled
        // (ScheduledMilestoneMessages).
        public static Message Welcome(string? firstName, long accountNo, decimal daily)
        {
            var lines = new List<string>
            {
                $"Hi {Name(firstName)}, your phone is ready to use.",
                "",
                $"Your payment: Ksh {Money(daily)} a day",
                "Pay every day to keep it working. Pay for a few days at once and it stays unlocked for longer.",
                "",
            };
            lines.AddRange(HowToPay(accountNo));
            lines.Add("");
            lines.Add("We'll remind you before it locks.");
            lines.Add(Help);

            return new("Welcome to Ranalo Credit", Lines(lines.ToArray()));
        }

        // Once, when the contract is paid off (ScheduledMilestoneMessages).
        public static Message FullyPaid(string? firstName, long accountNo) =>
            new("Congratulations! Your phone is fully paid", Lines(
                $"Hi {Name(firstName)}, you've finished paying account {accountNo}. The phone is yours.",
                "",
                "It won't lock again. We'll remove our lock app from it soon; you don't need to do anything.",
                "",
                $"Thank you for choosing {SignOff}.",
                Help));

        // The weekly update, Sunday evening (ScheduledDailyPaymentSummary).
        // Arrears below zero = behind; zero or more = up to date or ahead.
        public static Message Summary(MobileStatusReport record, DateTime? nextLockUtc, DateTime contractEnds)
        {
            var arrears = record.Arrears;
            var title = arrears < 0
                ? $"Your weekly update: Ksh {Money(Math.Abs(arrears))} behind"
                : "Your weekly update: up to date";
            var standing = arrears < 0
                ? $"Behind by: Ksh {Money(Math.Abs(arrears))}"
                : $"Paid ahead: Ksh {Money(arrears)}";

            var owed = record.TotalLoan + record.Deposit;
            var percentPaid = owed > 0 ? Math.Round(record.TotalPaid / owed * 100, 0) : 0;

            var lines = new List<string>
            {
                $"Hi {Name(record.FirstName)}, here's account {record.AccountNo} this week.",
                "",
                standing,
            };
            if (nextLockUtc != null)
            {
                lines.Add($"Phone locks: {When(nextLockUtc.Value + KenyaOffset)}");
            }
            lines.Add($"Paid this week: Ksh {Money(record.TotalWeekPaid)}");
            lines.Add($"Paid so far: {percentPaid}%");
            lines.Add($"Balance: Ksh {Money(record.LoanBalance)}");
            lines.Add($"Contract ends: {contractEnds.ToString("d MMM yyyy", CultureInfo.InvariantCulture)}");
            lines.Add("");
            if (arrears < 0)
            {
                lines.Add($"Pay Ksh {Money(Math.Abs(arrears))} to catch up.");
            }
            lines.Add($"Pay: M-Pesa Pay Bill {Paybill}, account no {record.AccountNo}");
            lines.Add(Help);

            return new(title, Lines(lines.ToArray()));
        }

        // Knox lock screen: what the customer sees on a locked phone.
        public static string LockScreenText(long accountNo) =>
            $"This phone is locked. To unlock it, pay with M-Pesa: Pay Bill {Paybill}, account no {accountNo}. " +
            $"Keep the phone on and connected to the internet so it can unlock. Help: call {HelpLine}";

        // Nuovo and Knox show <br> as a line break.
        public static string ForNuovoKnox(string text) => text.Replace("\n", "<br>");

        public static string ForNuovoKnox(Message message) => ForNuovoKnox(message.Full);

        // Clipped to PayTrigger's limit rather than refused.
        public static string Clip(string text, int limit) =>
            text.Length <= limit ? text : text[..(limit - 1)] + "…";

        // The M-Pesa menu path, in the words the menu uses.
        private static IEnumerable<string> HowToPay(long accountNo) => new[]
        {
            "How to pay:",
            "M-Pesa > Lipa na M-Pesa > Pay Bill",
            $"Business no: {Paybill}",
            $"Account no: {accountNo}",
        };

        private static string Help => $"Help: call {HelpLine}";

        private static string Lines(params string[] lines) => string.Join("\n", lines);

        // Names arrive as typed ("JANE", "jane"); show "Jane".
        private static string Name(string? firstName) =>
            string.IsNullOrWhiteSpace(firstName)
                ? "Customer"
                : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(firstName.Trim().ToLowerInvariant());

        private static string Money(decimal amount) =>
            amount.ToString("#,##0.##", CultureInfo.InvariantCulture);

        // "Tue 28 Oct, 12:31 PM"
        private static string When(DateTime at) =>
            at.ToString("ddd d MMM, h:mm tt", CultureInfo.InvariantCulture);
    }
}
