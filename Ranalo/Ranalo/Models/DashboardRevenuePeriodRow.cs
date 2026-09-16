namespace Ranalo.Models
{
    // Result of a live (non-rollup) revenue query for an arbitrary date window,
    // scoped to a single dealer -- see
    // IDashboardReportRepository.GetDealerRevenueForPeriodAsync. Used by the
    // Dealer Dashboard's date-range filter, which needs figures for windows
    // the nightly ScheduledDashboardRollup doesn't precompute (it only knows
    // "this month" / "last month").
    public class DashboardRevenuePeriodRow
    {
        public decimal RevenueThisPeriod { get; set; }
        public decimal RevenueLastPeriod { get; set; }

        // Lifetime account count (StartDate not null), NOT period-filtered --
        // used only to normalize AvgPerAccount against the period's revenue.
        // Distinct from TotalAccountsAsOfPeriod below.
        public int TotalAccounts { get; set; }

        // "If everyone paid as expected": sum of each account's daily-blended
        // payment rate (Daily + Weekly/7 + Monthly/30) times the number of
        // days its accrual window (StartDate through contract completion)
        // overlaps the period -- not what was actually paid.
        public decimal TargetRevenue { get; set; }

        // Total Accounts card fields (period-scoped, unlike TotalAccounts
        // above): how many accounts existed as of the end of the selected
        // period, how many were newly started within it, and how many were
        // newly started in the immediately preceding window of the same
        // length -- for the period-over-period "new" comparison.
        public int TotalAccountsAsOfPeriod { get; set; }
        public int NewAccountsInPeriod { get; set; }
        public int NewAccountsPriorPeriod { get; set; }
    }
}
