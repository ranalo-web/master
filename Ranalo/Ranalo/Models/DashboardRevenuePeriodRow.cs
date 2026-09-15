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
        public int TotalAccounts { get; set; }

        // "If everyone paid as expected": sum of each account's daily-blended
        // payment rate (Daily + Weekly/7 + Monthly/30) times the number of
        // days its accrual window (StartDate through contract completion)
        // overlaps the period -- not what was actually paid.
        public decimal TargetRevenue { get; set; }
    }
}
