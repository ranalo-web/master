using System.Globalization;
using Ranalo.Models;

namespace Ranalo.Services
{
    // Every message we send to a customer's phone, in one place (2026-10-25).
    // Text is built with "\n" line breaks: Transsion popups take it as it is,
    // Nuovo and Knox get <br> instead (ForNuovoKnox). Kept free of I/O so it
    // can be unit tested.
    public static class CustomerMessages
    {
        public const string Paybill = "4090703";
        public const string HelpLine = "0772 007 007";
        public const string SignOff = "Ranalo Credit";

        // PayTrigger's limits for a popup.
        public const int TitleLimit = 80;
        public const int ContentLimit = 500;

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
            LockReminder.In6Hours => "Payment reminder",
            LockReminder.In4Hours => "Payment reminder: phone locks in 4 hours",
            LockReminder.In2Hours => "Urgent: phone locks in 2 hours",
            LockReminder.In1Hour => "Final notice: phone locks in 1 hour",
            _ => "Final notice: phone locks soon",
        };

        private static string ReminderOpening(LockReminder reminder) => reminder switch
        {
            LockReminder.In6Hours => "Friendly reminder: your phone will lock in about 6 hours unless we receive your payment.",
            LockReminder.In4Hours => "Reminder: your phone will lock in about 4 hours unless we receive your payment.",
            LockReminder.In2Hours => "Urgent: your phone will lock in about 2 hours unless we receive your payment.",
            LockReminder.In1Hour => "Final notice: your phone will lock in about 1 hour unless we receive your payment.",
            _ => "Final notice: your phone will lock in less than an hour unless we receive your payment.",
        };

        public static string LockReminderText(string? firstName, long accountNo, decimal amount, LockReminder reminder) =>
            Lines(
                $"Hi {Name(firstName)}, {ReminderOpening(reminder)}",
                "Pay with M-Pesa:",
                $"Paybill: {Paybill}",
                $"Account: {accountNo}",
                $"Amount: Ksh {Money(amount)}",
                "Already paid? No need to do anything.",
                $"Help: {HelpLine} - {SignOff}");

        public const string PaymentReceivedTitle = "Payment received";

        public static string PaymentReceivedText(string? firstName, decimal amount, string? accountNo, string? mpesaCode, DateTime paidAt) =>
            Lines(
                $"Hi {Name(firstName)}, thank you! We've received Ksh {Money(amount)} for account {accountNo}.",
                $"M-Pesa code: {mpesaCode}",
                $"Date: {paidAt.ToString("dd/MM/yyyy h:mm tt", CultureInfo.InvariantCulture)}",
                SignOff);

        public const string SummaryTitle = "Your account summary";

        public static string SummaryText(MobileStatusReport record, DateTime contractEnds, DateTime now)
        {
            // Arrears below zero = behind; zero or more = up to date or ahead.
            var arrears = record.Arrears;
            var standing = arrears < 0
                ? $"Arrears: Ksh {Money(Math.Abs(arrears))}"
                : $"Paid ahead: Ksh {Money(arrears)}";

            var owed = record.TotalLoan + record.Deposit;
            var percentPaid = owed > 0 ? Math.Round(record.TotalPaid / owed * 100, 0) : 0;

            return Lines(
                $"Hi {Name(record.FirstName)}, your summary for account {record.AccountNo} at {now.ToString("dd/MM/yyyy h:mm tt", CultureInfo.InvariantCulture)}:",
                $"Paid this week: Ksh {Money(record.TotalWeekPaid)}",
                standing,
                $"Next lock date: {record.NextLockDate?.Replace("T", " ")}",
                $"Total paid: Ksh {Money(record.TotalPaid)} ({percentPaid}%)",
                $"Loan balance: Ksh {Money(record.LoanBalance)}",
                $"Contract ends: {contractEnds.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}",
                $"Last payment: {record.LastPaymentDate?.ToString("dd/MM/yyyy h:mm tt", CultureInfo.InvariantCulture)} ({record.LastPaidMpesa})",
                $"Pay: Paybill {Paybill}, account {record.AccountNo}. Help: {HelpLine}");
        }

        // Knox lock screen: what the customer sees on a locked phone.
        public static string LockScreenText(long accountNo) =>
            $"Your phone is locked. Pay with M-Pesa Paybill {Paybill}, account {accountNo}, to unlock it. Help: {HelpLine}";

        // Nuovo and Knox show <br> as a line break.
        public static string ForNuovoKnox(string text) => text.Replace("\n", "<br>");

        // Clipped to PayTrigger's limit rather than refused.
        public static string Clip(string text, int limit) =>
            text.Length <= limit ? text : text[..(limit - 1)] + "…";

        private static string Lines(params string[] lines) => string.Join("\n", lines);

        private static string Name(string? firstName) =>
            string.IsNullOrWhiteSpace(firstName) ? "Customer" : firstName.Trim();

        private static string Money(decimal amount) =>
            amount.ToString("#,##0.##", CultureInfo.InvariantCulture);
    }
}
