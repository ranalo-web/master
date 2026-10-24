using Ranalo.Models;

namespace Ranalo.Services
{
    // Each dealer's and agent's commission position, from the per-account
    // figures CommissionCalculator produces: default rate and suspension,
    // earned / withheld / paid / owed, what is payable now, and the agent
    // upfront and bonus still due. The one place these are worked out, so
    // Pay Commissions, the Admin / Dealer / Agent dashboards and the admin
    // Commissions pages all show the same numbers. No I/O.
    public static class CommissionPayees
    {
        // The dashboards' Arrears tier: more than 7 days past the lock date.
        // An account that has been in collections counts for good.
        public static bool InArrears(CommissionAccount a) =>
            a.Commission.InCollections || CommissionPayoutRules.IsInDefault(a.DaysPastLock);

        // Whether an account belongs on this payee's commission at all. A
        // no-commission (legacy) account is left out, except on the dealer's
        // side while its arrears are being recovered from the dealer.
        public static bool CountsFor(string payeeType, CommissionAccount a) =>
            !a.Commission.NoCommission || (payeeType == CommissionPayeeType.Dealer && a.Commission.RecoverArrears);

        public static PayoutAccount ToPayoutAccount(string payeeType, CommissionAccount a)
        {
            var c = a.Commission;
            var isAgent = payeeType == CommissionPayeeType.Agent;
            return new PayoutAccount
            {
                ContractId = long.TryParse(a.ContractId, out var id) ? id : 0,
                StartDate = a.StartDate,
                Earned = isAgent ? c.AgentEarned : c.DealerCommission ?? 0,
                Upfront = isAgent ? c.AgentUpfront : c.DealerCommission ?? 0,
                Deducted = isAgent ? c.AgentArrearsDeducted : c.DealerArrearsDeducted,
                Paid = isAgent ? c.AgentPaid : c.DealerPaid,
                BonusPaid = isAgent && !c.NoCommission ? a.AgentBonusPaid : 0,
            };
        }

        public static CommissionPayeeSummary Summarize(string payeeType, int payeeId, IReadOnlyCollection<CommissionAccount> accounts)
        {
            var isAgent = payeeType == CommissionPayeeType.Agent;
            // The default rate is judged on accounts that earn commission
            // only: a dealer's legacy book (even while its arrears are being
            // recovered) never suspends them.
            var earning = accounts.Where(a => !a.Commission.NoCommission).ToList();
            var inArrears = earning.Count(InArrears);
            var rate = CommissionPayoutRules.DefaultRatePct(earning.Count, inArrears);
            var suspended = CommissionPayoutRules.IsSuspended(rate);
            var pool = isAgent
                ? CommissionCalculator.PoolAgent(accounts.Select(a => a.Commission))
                : CommissionCalculator.PoolDealer(accounts.Select(a => a.Commission));
            var payout = accounts.Select(a => ToPayoutAccount(payeeType, a)).ToList();

            var dealerNames = accounts.Select(a => a.DealerName).Distinct().ToList();
            return new CommissionPayeeSummary
            {
                PayeeType = payeeType,
                PayeeId = payeeId,
                Name = isAgent
                    ? accounts.Select(a => a.AgentName).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)) ?? $"Agent {payeeId}"
                    : dealerNames.FirstOrDefault() ?? $"Dealer {payeeId}",
                DealerName = isAgent ? string.Join(", ", dealerNames) : null,
                Accounts = earning.Count,
                AccountsInArrears = inArrears,
                DefaultRatePct = rate,
                IsSuspended = suspended,
                Pool = pool,
                Payable = suspended ? 0 : pool.Owed,
                UpfrontDue = isAgent ? payout.Sum(CommissionPayoutRules.UnpaidUpfront) : 0,
                BonusDue = isAgent ? payout.Sum(CommissionPayoutRules.UnpaidBonus) : 0,
                BonusHeldCount = isAgent ? accounts.Count(a => a.Commission.BonusAtRisk) : 0,
                BonusHeldNoWooOrderCount = isAgent ? accounts.Count(a => a.Commission.BonusHeldNoWooOrder) : 0,
                MissingBuyingPriceCount = isAgent ? 0 : earning.Count(a => !a.BuyingPrice.HasValue),
            };
        }

        // One summary per agent (accounts with no agent are direct sales and
        // skipped, as are no-commission accounts).
        public static List<CommissionPayeeSummary> Agents(IEnumerable<CommissionAccount> accounts) => accounts
            .Where(a => a.AgentId.HasValue && CountsFor(CommissionPayeeType.Agent, a))
            .GroupBy(a => a.AgentId!.Value)
            .Select(g => Summarize(CommissionPayeeType.Agent, g.Key, g.ToList()))
            .ToList();

        public static List<CommissionPayeeSummary> Dealers(IEnumerable<CommissionAccount> accounts) => accounts
            .Where(a => CountsFor(CommissionPayeeType.Dealer, a))
            .GroupBy(a => a.DealerId)
            .Select(g => Summarize(CommissionPayeeType.Dealer, g.Key, g.ToList()))
            .ToList();
    }
}
