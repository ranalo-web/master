using Ranalo.Models;
using Ranalo.Services.DeviceLock;

namespace Ranalo.Services
{
    // What the customer app shows, worked out exactly as the staff reports
    // and the weekly summary message do (CustomerMessages.Summary,
    // ScheduledDailyPaymentSummary), from the same MobileStatusReport.
    public static class CustomerAppRules
    {
        public static MyAccountView Account(MobileStatusReport r, DateTime? lockUtc, MyDevice? device, DateTime nowUtc,
            RestructuredRecord? plan = null)
        {
            var owed = r.TotalLoan + r.Deposit;
            var fullyPaid = FullyPaidPlanner.IsPaidOff(r);
            // 31/12/9999 (and Transsion's 2099) mean "never": fully paid.
            var lockAt = lockUtc is { Year: < 2099 } ? lockUtc : null;

            return new MyAccountView
            {
                AccountNo = r.AccountNo,
                FirstName = CustomerMessages.Name(r.FirstName),
                DeviceName = !string.IsNullOrWhiteSpace(device?.ProductName) ? device!.ProductName!.Trim()
                    : $"{Cap(r.Make)} {r.Model}".Trim(),
                PhotoUrl = device?.PhotoUrl,
                TotalPaid = r.TotalPaid,
                Owed = owed,
                PercentPaid = owed > 0 ? (int)Math.Min(100, Math.Round(r.TotalPaid / owed * 100, 0)) : 0,
                Balance = Math.Max(0, r.LoanBalance),
                // On a restructure, where they stand on the agreed plan (as the
                // Restructured page and its lock job count it), not the old contract.
                Behind = plan != null ? (plan.ArrearsR < 0 ? Math.Abs(plan.ArrearsR) : 0) : r.Arrears < 0 ? Math.Abs(r.Arrears) : 0,
                Ahead = plan != null ? Math.Max(0, plan.ArrearsR) : r.Arrears > 0 ? r.Arrears : 0,
                Daily = plan != null && plan.AmountRes > 0 ? plan.AmountRes : r.Daily + r.Weekly / 7 + r.Monthly / 30,
                RestructuredOn = plan?.DateAgreed,
                // The original contract, shown next to the plan.
                ContractDaily = r.Daily + r.Weekly / 7 + r.Monthly / 30,
                ContractDue = r.TotalDue,
                ContractBehind = r.Arrears < 0 ? Math.Abs(r.Arrears) : 0,
                PlanDue = plan?.TotalDueR ?? 0,
                PlanPaid = plan?.TotalPaidR ?? 0,
                Locked = !fullyPaid && (r.Locked || (lockAt.HasValue && lockAt <= nowUtc)),
                UnlockFor24h = fullyPaid ? null : plan != null ? PlanUnlockAmount(plan) : UnlockAmount(r.NewDaily, r.TotalLast24),
                LocksAtKenya = fullyPaid ? null : lockAt + CustomerMessages.KenyaOffset,
                LockPassed = !fullyPaid && lockAt <= nowUtc,
                ContractEnds = r.FirstPaymentDate?.AddDays(r.TermInMonths * 30),
                LastPaidOn = r.LastPaymentDate,
                LastPaidAmount = r.LastPaidAmt,
                FullyPaid = fullyPaid,
            };
        }

        // Auto restructure (RestructureCalculator): paying at least the new
        // daily within 24h opens the phone. What's still to pay today, in
        // whole shillings; null when there's none (no arrears to spread, or
        // past the contract end -- then only an agreed plan opens it).
        public static decimal? UnlockAmount(decimal? autoNewDaily, decimal paidLast24Hours) =>
            autoNewDaily is > 0 ? Math.Max(0, Math.Ceiling(autoNewDaily.Value - paidLast24Hours)) : null;

        // On an agreed plan the lock job (ScheduledLockRestructured) opens the
        // phone when the plan is ahead (ArrearsR > 0, a day per plan daily),
        // otherwise under the auto restructure. So: ahead -> nothing to pay;
        // behind -> the cheaper of catching up plus one plan day, or the auto
        // restructure amount when there is one.
        public static decimal? PlanUnlockAmount(RestructuredRecord plan)
        {
            if (plan.ArrearsR > 0) return 0;
            var catchUp = plan.AmountRes > 0 ? Math.Ceiling(-plan.ArrearsR + plan.AmountRes) : (decimal?)null;
            var auto = UnlockAmount(plan.AutoNewDaily, plan.PaidLast24Hours);
            return catchUp == null ? auto : auto == null ? catchUp : Math.Min(catchUp.Value, auto.Value);
        }

        // A paid-off contract (no longer in the open-contract report), from
        // its Completed Contracts record.
        public static MyAccountView PaidOff(CompletedContractRow c, MyDevice? device) => new()
        {
            AccountNo = c.AccountNo,
            FirstName = CustomerMessages.Name(c.CustomerName),
            DeviceName = !string.IsNullOrWhiteSpace(device?.ProductName) ? device!.ProductName!.Trim() : $"{Cap(c.Make)} {c.Model}".Trim(),
            PhotoUrl = device?.PhotoUrl,
            TotalPaid = c.Paid,
            Owed = c.ContractValue,
            PercentPaid = 100,
            Balance = 0,
            ContractEnds = c.EndDate,
            LastPaidOn = c.LastPaid,
            FullyPaid = true,
        };

        // Each payment with the balance left after it: what's owed less
        // everything paid up to then (the report's LoanBalance, step by step).
        public static List<MyStatementLine> Statement(IEnumerable<MyPayment> payments, decimal owed)
        {
            var paid = 0m;
            return payments
                .OrderBy(p => p.PaidAt).ThenBy(p => p.MpesaCode)
                .Select(p =>
                {
                    paid += p.Amount;
                    return new MyStatementLine(p.PaidAt, p.MpesaCode, p.Amount, paid, owed - paid);
                })
                .Reverse()
                .ToList();
        }

        // A logged message back into a title and lines for the app's inbox.
        // Nuovo's are stored with <br>, Knox's on one line with " - "
        // (CustomerMessages.ForNuovoKnox / ForKnox), Transsion's with "\n".
        public static MyInboxItem InboxItem(MessageLog log)
        {
            var text = (log.Message ?? "").Replace("<br>", "\n", StringComparison.OrdinalIgnoreCase);
            var lines = text.Contains('\n')
                ? text.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList()
                : text.Split(" - ").Select(l => l.Trim()).Where(l => l.Length > 0).ToList();

            var title = lines.FirstOrDefault() ?? "Message from Ranalo";
            return new MyInboxItem(
                (log.DateSent ?? DateTime.UtcNow) + CustomerMessages.KenyaOffset,
                log.MessageType switch
                {
                    "Payment" => "Receipt",
                    "Reminder" => "Reminder",
                    "Summary" => "Weekly update",
                    "FullyPaid" => "Fully paid",
                    "Welcome" => "Welcome",
                    _ => "Message",
                },
                title,
                lines.Skip(1).ToList(),
                string.Equals(log.MessageStatus, "sent", StringComparison.OrdinalIgnoreCase));
        }

        // The first photo of the product, from WooCommerce's comma list.
        public static string? FirstPhoto(string? images) =>
            images?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault(u => u.StartsWith("https://", StringComparison.OrdinalIgnoreCase));

        public static string? CleanEmail(string? raw)
        {
            var email = raw?.Trim();
            return !string.IsNullOrEmpty(email) && email.Length <= 200
                && System.Text.RegularExpressions.Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$") ? email : null;
        }

        public static string? CleanNationalId(string? raw)
        {
            var id = new string((raw ?? "").Where(char.IsLetterOrDigit).ToArray());
            return id.Length is >= 6 and <= 20 ? id.ToUpperInvariant() : null;
        }

        // The details every customer must have before using the app.
        public static List<string> Missing(CustomerAccess access)
        {
            var missing = new List<string>();
            if (CleanEmail(access.Email) == null) missing.Add("email");
            if (StkRules.Phone(access.Phone) == null) missing.Add("M-Pesa number");
            if (CleanNationalId(access.NationalId) == null) missing.Add("national ID");
            return missing;
        }

        private static string Cap(string? s) =>
            string.IsNullOrWhiteSpace(s) ? "" : char.ToUpperInvariant(s.Trim()[0]) + s.Trim()[1..];
    }

    public class MyDevice
    {
        public string? ProductName { get; set; }
        public string? Images { get; set; }
        public string? PhotoUrl => CustomerAppRules.FirstPhoto(Images);
    }

    public class MyPayment
    {
        public string MpesaCode { get; set; } = "";
        public decimal Amount { get; set; }
        public DateTime PaidAt { get; set; }
    }

    // Delivered: the phone's provider accepted it (it may still not have shown on the phone).
    public record MyInboxItem(DateTime SentAtKenya, string Kind, string Title, List<string> Lines, bool Delivered);

    public record MyStatementLine(DateTime PaidAt, string MpesaCode, decimal Amount, decimal PaidSoFar, decimal BalanceAfter);

    public class MyAccountView
    {
        public long AccountNo { get; set; }
        public string FirstName { get; set; } = "";
        public string DeviceName { get; set; } = "";
        public string? PhotoUrl { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal Owed { get; set; }
        public int PercentPaid { get; set; }
        public decimal Balance { get; set; }
        public decimal Behind { get; set; }
        public decimal Ahead { get; set; }
        public decimal Daily { get; set; }
        public DateTime? LocksAtKenya { get; set; }
        public bool LockPassed { get; set; }
        public DateTime? ContractEnds { get; set; }
        public DateTime? LastPaidOn { get; set; }
        public decimal LastPaidAmount { get; set; }
        public bool FullyPaid { get; set; }
        // Set when the account is on an agreed restructure plan.
        public DateTime? RestructuredOn { get; set; }
        public decimal ContractDaily { get; set; }
        public decimal ContractDue { get; set; }
        public decimal ContractBehind { get; set; }
        public decimal PlanDue { get; set; }
        public decimal PlanPaid { get; set; }
        // The phone is locked now (the provider says so, or its lock date has passed).
        public bool Locked { get; set; }
        // What still has to be paid today to open the phone under the auto
        // restructure; 0 when enough has been paid in the last 24h; null when
        // there is no auto restructure (past the contract end).
        public decimal? UnlockFor24h { get; set; }
    }
}
