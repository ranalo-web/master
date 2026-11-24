using Ranalo.Models;

namespace Ranalo.Services
{
    // How an agent or a dealer is doing, for the Agents and Dealers pages.
    // Built from the same per-account commission figures as Pay Commissions
    // (CommissionPayees), so arrears, default rate and commission owed always
    // match what would be paid.
    public class SalesTeamStats
    {
        public int Sales { get; set; }              // every account they sold
        public int SalesThisMonth { get; set; }     // started this month (Kenya time)
        public int InArrears { get; set; }          // of the accounts that earn commission
        public decimal DefaultRatePct { get; set; }
        public bool CommissionSuspended { get; set; }
        public decimal Earned { get; set; }
        public decimal Paid { get; set; }
        public decimal Owed { get; set; }
    }

    public static class SalesTeamRules
    {
        public static Dictionary<int, SalesTeamStats> ByAgent(IEnumerable<CommissionAccount> accounts, DateTime kenyaToday) =>
            Build(CommissionPayeeType.Agent, accounts.Where(a => a.AgentId.HasValue).GroupBy(a => a.AgentId!.Value), kenyaToday);

        public static Dictionary<int, SalesTeamStats> ByDealer(IEnumerable<CommissionAccount> accounts, DateTime kenyaToday) =>
            Build(CommissionPayeeType.Dealer, accounts.GroupBy(a => a.DealerId), kenyaToday);

        private static Dictionary<int, SalesTeamStats> Build(string payeeType, IEnumerable<IGrouping<int, CommissionAccount>> groups, DateTime kenyaToday)
        {
            var result = new Dictionary<int, SalesTeamStats>();
            foreach (var g in groups)
            {
                var stats = new SalesTeamStats
                {
                    Sales = g.Count(),
                    SalesThisMonth = g.Count(a => a.StartDate.Year == kenyaToday.Year && a.StartDate.Month == kenyaToday.Month),
                };
                var counted = g.Where(a => CommissionPayees.CountsFor(payeeType, a)).ToList();
                if (counted.Count > 0)
                {
                    var s = CommissionPayees.Summarize(payeeType, g.Key, counted);
                    stats.InArrears = s.AccountsInArrears;
                    stats.DefaultRatePct = s.DefaultRatePct;
                    stats.CommissionSuspended = s.IsSuspended;
                    stats.Earned = s.Pool.Earned;
                    stats.Paid = s.Pool.Paid;
                    stats.Owed = s.Pool.Owed;
                }
                result[g.Key] = stats;
            }
            return result;
        }
    }
}
