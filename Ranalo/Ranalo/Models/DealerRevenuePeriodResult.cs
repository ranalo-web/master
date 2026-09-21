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

        // Total Accounts card: accounts that existed as of the end of the
        // selected period.
        public int TotalAccounts { get; set; }

        // New accounts started within the selected period.
        public int NewInPeriod { get; set; }

        // Period-over-period change in NewInPeriod vs. the immediately
        // preceding window of the same length -- null when the prior window
        // had zero new accounts to compare against (see
        // ScheduledDashboardRollup.CalculateGrowthPct).
        public decimal? NewInPeriodChangePct { get; set; }

        // Agent Commissions card: commission paid to agents within the
        // selected period, live from AgentCommissionPayments.
        public decimal CommissionPaidThisPeriod { get; set; }

        // Dealer Commissions card: commission paid to this dealer within the
        // selected period, live from DealerCommissionPayments.
        public decimal DealerCommissionPaidThisPeriod { get; set; }

        // Completed Contracts summary card: how many of the dealer's
        // contracts finished (fully paid off) within the selected period --
        // filtered in DashboardReportService from the same
        // GetCompletedContractsAsync rows the Completed Contracts report page
        // uses, not a separate query.
        public int CompletedContractsInPeriod { get; set; }

        // My Portfolio card: of the accounts that STARTED within the selected
        // period, % currently in good standing (CollectionRatePct) vs 30+
        // days past their lock date (PortfolioAtRiskPct) -- count-based, not
        // value-based. See DashboardReportService.ComputeCohortRates.
        public decimal CollectionRatePct { get; set; }
        public decimal PortfolioAtRiskPct { get; set; }
    }
}
