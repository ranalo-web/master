namespace Ranalo.Services
{
    // Loss given default (2026-10-28): how much we really lost on defaulted
    // accounts. Defaulted = an approved write-off not reinstated, or an open
    // account more than 90 days past its lock date, restructured or not.
    // Anyone who paid in the last PayingDays is still paying, so not a loss
    // yet, and is left out (DashboardReportService.DefaultedAccounts).
    //
    // Per account: what we put in (phone cost + commissions + repossession
    // cost) - what came back (everything the customer paid, deposit
    // included, + resale value), never below 0. A customer who paid back
    // our cost has cost us nothing. LGD = total loss / total put in, so a
    // KES 30,000 phone weighs more than a KES 8,000 one. Accounts without a
    // recorded phone cost are counted apart (MissingCost), not guessed.
    public static class LossGivenDefault
    {
        // A payment this recent means the customer is still paying.
        public const int PayingDays = 30;

        public record Account(string? Dealer, decimal? PhoneCost, decimal Paid, decimal Resale,
            bool WrittenOff, decimal ContractValue = 0, decimal Commissions = 0, decimal RepossessionCost = 0)
        {
            public decimal? Cost => PhoneCost is > 0 ? PhoneCost.Value + Commissions + RepossessionCost : null;
        }

        public record Summary(int Defaulted, int WrittenOff, int Over90Days, int MissingCost,
            decimal Cost, decimal Loss, int LossMaking = 0, decimal Paid = 0, decimal ContractValue = 0)
        {
            public decimal LgdPct => Cost > 0 ? Math.Round(100m * Loss / Cost, 1) : 0;

            // Defaulted accounts that had already paid back our cost.
            public int Covered => Defaulted - MissingCost - LossMaking;

            // What the defaulted customers paid, as a share of their
            // contracts: compare with the break-even share.
            public decimal PaidPctOfContract => ContractValue > 0 ? Math.Round(100m * Paid / ContractValue, 1) : 0;

            public static readonly Summary None = new(0, 0, 0, 0, 0, 0);
        }

        // Accounts left out of the defaulted set because they paid in the
        // last PayingDays; RestructuredPaying of them are restructured.
        public record LeftOut(int RestructuredPaying, int Paying)
        {
            public static readonly LeftOut None = new(0, 0);
        }

        public static decimal? Loss(Account a) => a.Cost is decimal cost
            ? Math.Max(0, cost - a.Paid - a.Resale)
            : null;

        public static Summary Summarise(IEnumerable<Account> accounts)
        {
            var list = accounts.ToList();
            var costed = list.Where(a => a.Cost != null).ToList();
            return new Summary(
                list.Count,
                list.Count(a => a.WrittenOff),
                list.Count(a => !a.WrittenOff),
                list.Count - costed.Count,
                costed.Sum(a => a.Cost!.Value),
                costed.Sum(a => Loss(a)!.Value),
                costed.Count(a => Loss(a) > 0),
                costed.Sum(a => a.Paid),
                costed.Sum(a => a.ContractValue));
        }

        public static Dictionary<string, Summary> ByDealer(IEnumerable<Account> accounts) => accounts
            .Where(a => !string.IsNullOrWhiteSpace(a.Dealer))
            .GroupBy(a => a.Dealer!)
            .ToDictionary(g => g.Key, g => Summarise(g));
    }
}
