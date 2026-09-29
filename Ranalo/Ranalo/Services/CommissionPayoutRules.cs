namespace Ranalo.Services
{
    // Rules for paying commissions out, on top of CommissionCalculator's
    // per-account figures. Kept free of I/O so they can be unit tested.
    //
    // Suspension
    //   Default rate = accounts more than 7 days past their lock date
    //   (the dashboards' Arrears tier) / all of the payee's accounts.
    //   Above 30% nothing is payable. Commission keeps accruing and becomes
    //   payable again once the rate is back at 30% or below. A dealer and each
    //   agent are judged on their own accounts only.
    //
    // Payable
    //   Arrears on every one of the payee's accounts are deducted, not just
    //   the ones being paid: payable = min(the payee's pooled Owed, the
    //   unpaid commission on the accounts selected).
    //
    // Allocation
    //   A payout is recorded against the individual accounts it covers, so
    //   each account keeps its own paid total (needed when a device is
    //   moved, repossessed or written off). It's split in two passes, each
    //   oldest account first: every account's unpaid upfront, then any
    //   remaining commission (the agent bonus; for dealers the whole
    //   commission is one pass). Money already paid on an account counts
    //   toward its upfront first. Anything still left (an override, e.g.
    //   recording a payment made earlier) lands on the newest account.
    public static class CommissionPayoutRules
    {
        public const decimal SuspensionThresholdPct = 30m;

        public static decimal DefaultRatePct(int accounts, int accountsInArrears) =>
            accounts > 0 ? Math.Round(accountsInArrears * 100m / accounts, 1) : 0m;

        public static bool IsSuspended(decimal defaultRatePct) => defaultRatePct > SuspensionThresholdPct;

        public static decimal Unpaid(PayoutAccount a) => Math.Max(0, a.Earned - a.Paid);

        public static decimal OwnNet(PayoutAccount a) => Math.Max(0, a.Earned - a.Deducted - a.Paid);

        // The upfront part still unpaid; what's already been paid covers the upfront first.
        public static decimal UnpaidUpfront(PayoutAccount a) => Math.Max(0, Math.Min(a.Upfront, a.Earned) - a.Paid);

        public static decimal Payable(IReadOnlyCollection<PayoutAccount> allAccounts, IEnumerable<long> selectedContractIds, bool suspended)
        {
            if (suspended)
            {
                return 0;
            }

            var selected = selectedContractIds.ToHashSet();
            var owedOverall = CommissionCalculator.Pool(allAccounts.Select(a => (a.Earned, a.Deducted, a.Paid))).Owed;
            var selectedUnpaid = allAccounts.Where(a => selected.Contains(a.ContractId)).Sum(Unpaid);
            return Math.Min(owedOverall, selectedUnpaid);
        }

        public static List<(long ContractId, decimal Amount)> Allocate(IEnumerable<PayoutAccount> selectedAccounts, decimal amount)
        {
            var ordered = selectedAccounts.OrderBy(a => a.StartDate).ThenBy(a => a.ContractId).ToList();
            var allocated = ordered.ToDictionary(a => a.ContractId, _ => 0m);
            var remaining = Math.Round(amount, 2);

            void Fill(Func<PayoutAccount, decimal> cap)
            {
                foreach (var a in ordered)
                {
                    if (remaining <= 0)
                    {
                        return;
                    }

                    var room = Math.Round(cap(a) - allocated[a.ContractId], 2);
                    if (room > 0)
                    {
                        var take = Math.Min(room, remaining);
                        allocated[a.ContractId] += take;
                        remaining -= take;
                    }
                }
            }

            Fill(UnpaidUpfront);
            Fill(Unpaid);
            if (remaining > 0 && ordered.Count > 0)
            {
                allocated[ordered[^1].ContractId] += remaining;
            }

            return ordered
                .Where(a => allocated[a.ContractId] > 0)
                .Select(a => (a.ContractId, allocated[a.ContractId]))
                .ToList();
        }
    }

    // One account's commission from the payee's side (agent or dealer).
    public class PayoutAccount
    {
        public long ContractId { get; set; }
        public DateTime StartDate { get; set; }
        public decimal Earned { get; set; }

        // Paid ahead of anything else: the agent's 50% upfront. For dealers
        // it equals Earned, so their commission is allocated in one pass.
        public decimal Upfront { get; set; }
        public decimal Deducted { get; set; }
        public decimal Paid { get; set; }
    }
}
