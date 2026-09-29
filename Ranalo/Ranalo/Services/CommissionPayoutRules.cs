namespace Ranalo.Services
{
    // Rules for paying commissions out, on top of CommissionCalculator's
    // per-account figures. Kept free of I/O so they can be unit tested.
    //
    // Suspension
    //   Default rate = accounts more than 7 days past their lock date
    //   (the dashboards' Arrears tier) / all of the payee's accounts.
    //   (Arrears deductions, by contrast, use the payment shortfall.)
    //   Above 30% nothing is payable. Commission keeps accruing and becomes
    //   payable again once the rate is back at 30% or below. A dealer and each
    //   agent are judged on their own accounts only.
    //
    // Payable
    //   Arrears on every one of the payee's accounts are deducted, not just
    //   the ones being paid: payable = min(the payee's pooled Owed, what is
    //   still unpaid on the accounts selected for the chosen payment type).
    //
    // Payment type (agents)
    //   Upfront: pays only the 50% upfront still unpaid on each account.
    //   Bonus:   pays only the 25% bonus earned and still unpaid.
    //   Auto:    upfronts first (oldest account first), then bonuses.
    //   Each payment line records which part it paid (CommissionPart), so
    //   every account keeps its own upfront-paid and bonus-paid totals --
    //   needed when a device is moved, repossessed or written off. Older
    //   lines with no part count toward the upfront first. Dealer commission
    //   has no parts and is paid oldest account first. Anything left over (an
    //   override, e.g. recording a payment made earlier) lands on the newest
    //   account, as the chosen part.
    public static class CommissionPayoutRules
    {
        public const decimal SuspensionThresholdPct = 30m;

        public const int LockToleranceDays = 7;

        // Counts toward the default rate: more than 7 days past the lock date.
        public static bool IsInDefault(double daysPastLock) => daysPastLock > LockToleranceDays;

        public static decimal DefaultRatePct(int accounts, int accountsInArrears) =>
            accounts > 0 ? Math.Round(accountsInArrears * 100m / accounts, 1) : 0m;

        public static bool IsSuspended(decimal defaultRatePct) => defaultRatePct > SuspensionThresholdPct;

        public static decimal Unpaid(PayoutAccount a) => Math.Max(0, a.Earned - a.Paid);

        public static decimal OwnNet(PayoutAccount a) => Math.Max(0, a.Earned - a.Deducted - a.Paid);

        public static decimal UnpaidUpfront(PayoutAccount a) => Math.Max(0, Math.Min(a.Upfront, a.Earned) - a.UpfrontPaid);

        public static decimal UnpaidBonus(PayoutAccount a) => Math.Max(0, Math.Max(0, a.Earned - a.Upfront) - a.BonusPaid);

        // What the chosen payment type can still pay on one account.
        public static decimal Room(PayoutAccount a, string paymentType) => paymentType switch
        {
            CommissionPart.Upfront => UnpaidUpfront(a),
            CommissionPart.Bonus => UnpaidBonus(a),
            CommissionPart.Auto => Math.Min(Unpaid(a), UnpaidUpfront(a) + UnpaidBonus(a)),
            _ => Unpaid(a),
        };

        public static decimal Payable(IReadOnlyCollection<PayoutAccount> allAccounts, IEnumerable<long> selectedContractIds, bool suspended,
            string paymentType = CommissionPart.Commission)
        {
            if (suspended)
            {
                return 0;
            }

            var selected = selectedContractIds.ToHashSet();
            var owedOverall = CommissionCalculator.Pool(allAccounts.Select(a => (a.Earned, a.Deducted, a.Paid))).Owed;
            var selectedRoom = allAccounts.Where(a => selected.Contains(a.ContractId)).Sum(a => Room(a, paymentType));
            return Math.Min(owedOverall, selectedRoom);
        }

        public static List<PayoutLine> Allocate(IEnumerable<PayoutAccount> selectedAccounts, decimal amount,
            string paymentType = CommissionPart.Commission)
        {
            var ordered = selectedAccounts.OrderBy(a => a.StartDate).ThenBy(a => a.ContractId).ToList();
            var lines = new List<PayoutLine>();
            var remaining = Math.Round(amount, 2);

            void Fill(string? part, Func<PayoutAccount, decimal> cap)
            {
                foreach (var a in ordered)
                {
                    if (remaining <= 0)
                    {
                        return;
                    }

                    var take = Math.Min(Math.Round(cap(a), 2), remaining);
                    if (take > 0)
                    {
                        lines.Add(new PayoutLine(a.ContractId, part, take));
                        remaining -= take;
                    }
                }
            }

            switch (paymentType)
            {
                case CommissionPart.Upfront:
                    Fill(CommissionPart.Upfront, UnpaidUpfront);
                    break;
                case CommissionPart.Bonus:
                    Fill(CommissionPart.Bonus, UnpaidBonus);
                    break;
                case CommissionPart.Auto:
                    Fill(CommissionPart.Upfront, UnpaidUpfront);
                    Fill(CommissionPart.Bonus, UnpaidBonus);
                    break;
                default:
                    Fill(null, Unpaid);
                    break;
            }

            if (remaining > 0 && ordered.Count > 0)
            {
                var overflowPart = paymentType switch
                {
                    CommissionPart.Bonus => CommissionPart.Bonus,
                    CommissionPart.Upfront or CommissionPart.Auto => CommissionPart.Upfront,
                    _ => null,
                };
                lines.Add(new PayoutLine(ordered[^1].ContractId, overflowPart, remaining));
            }

            // One line per account and part.
            return lines
                .GroupBy(l => (l.ContractId, l.Part))
                .Select(g => new PayoutLine(g.Key.ContractId, g.Key.Part, g.Sum(l => l.Amount)))
                .ToList();
        }
    }

    public static class CommissionPart
    {
        public const string Upfront = "Upfront";
        public const string Bonus = "Bonus";
        public const string Auto = "Auto";

        // Dealer payments: no upfront/bonus split.
        public const string Commission = "Commission";

        public static readonly string[] AgentTypes = { Upfront, Bonus, Auto };
    }

    // One payment line: this much paid on this account, for this part (null for dealers).
    public record PayoutLine(long ContractId, string? Part, decimal Amount);

    // One account's commission from the payee's side (agent or dealer).
    public class PayoutAccount
    {
        public long ContractId { get; set; }
        public DateTime StartDate { get; set; }
        public decimal Earned { get; set; }

        // The agent's 50% upfront. For dealers it equals Earned.
        public decimal Upfront { get; set; }
        public decimal Deducted { get; set; }
        public decimal Paid { get; set; }

        // Of Paid, what was recorded against the bonus; the rest counts
        // toward the upfront (older lines have no part).
        public decimal BonusPaid { get; set; }
        public decimal UpfrontPaid => Paid - BonusPaid;
    }
}
