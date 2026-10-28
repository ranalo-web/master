namespace Ranalo.Services
{
    // Loss given default (2026-10-28): of the money a defaulted phone cost
    // us, the share we never got back. Defaulted = an approved write-off not
    // reinstated, or an open account more than 90 days past its lock date
    // (the dashboard's "Bad", restructured left out).
    //
    // Per account: loss = phone cost + commissions + repossession cost
    // - everything the customer paid (deposit included) - resale value,
    // never below 0. LGD = total loss / total phone cost, so a KES 30,000
    // phone weighs more than a KES 8,000 one. It can pass 100% when
    // commission was paid on a phone the customer barely paid for. Accounts
    // without a recorded phone cost are counted apart (MissingCost), not
    // guessed.
    public static class LossGivenDefault
    {
        public record Account(string? Dealer, decimal? PhoneCost, decimal Paid, decimal Commissions,
            decimal RepossessionCost, decimal Resale, bool WrittenOff);

        public record Summary(int Defaulted, int WrittenOff, int Over90Days, int MissingCost,
            decimal PhoneCost, decimal Loss)
        {
            public decimal LgdPct => PhoneCost > 0 ? Math.Round(100m * Loss / PhoneCost, 1) : 0;

            public static readonly Summary None = new(0, 0, 0, 0, 0, 0);
        }

        public static decimal? Loss(Account a) => a.PhoneCost is > 0
            ? Math.Max(0, a.PhoneCost.Value + a.Commissions + a.RepossessionCost - a.Paid - a.Resale)
            : null;

        public static Summary Summarise(IEnumerable<Account> accounts)
        {
            var list = accounts.ToList();
            var costed = list.Where(a => a.PhoneCost is > 0).ToList();
            return new Summary(
                list.Count,
                list.Count(a => a.WrittenOff),
                list.Count(a => !a.WrittenOff),
                list.Count - costed.Count,
                costed.Sum(a => a.PhoneCost!.Value),
                costed.Sum(a => Loss(a)!.Value));
        }

        public static Dictionary<string, Summary> ByDealer(IEnumerable<Account> accounts) => accounts
            .Where(a => !string.IsNullOrWhiteSpace(a.Dealer))
            .GroupBy(a => a.Dealer!)
            .ToDictionary(g => g.Key, g => Summarise(g));
    }
}
