namespace Ranalo.Models
{
    // JSON payload returned by DealerDashboardController's date-range filter
    // endpoint -- everything the Revenue card needs to re-render itself for
    // the selected period without a full page reload.
    public class DealerRevenuePeriodResult
    {
        public decimal Revenue { get; set; }

        // Null when the prior comparison window has no revenue to compare
        // against -- see ScheduledDashboardRollup.CalculateGrowthPct.
        public decimal? GrowthPct { get; set; }

        public decimal AvgPerAccount { get; set; }

        // Expected revenue for the period if every account paid exactly on
        // schedule -- see DashboardRevenuePeriodRow.TargetRevenue.
        public decimal TargetRevenue { get; set; }

        // e.g. "this week", "this month" -- goes directly into the card's
        // "<span>X%</span> {label}" sub-title.
        public string Label { get; set; } = "";
    }
}
