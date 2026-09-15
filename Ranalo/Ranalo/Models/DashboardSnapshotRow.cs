namespace Ranalo.Models
{
    // Maps 1:1 onto a row in the DashboardSnapshot rollup table
    // (Database/Dashboard/001_create_dashboard_tables.sql). Populated by
    // ScheduledDashboardRollup, read by DashboardReportRepository.
    //
    // Every metric is nullable: the refresh job may only know how to compute
    // a subset of these correctly at any given time, and DashboardReportService
    // applies each field independently with a "keep sample data" fallback
    // when null, rather than gating on "does a row exist at all."
    public class DashboardSnapshotRow
    {
        public int? DealerId { get; set; }
        public int? AgentId { get; set; }

        public decimal? RevenueThisMonth { get; set; }
        public decimal? RevenueGrowthPct { get; set; }
        public decimal? AvgPerAccount { get; set; }

        public int? TotalAccounts { get; set; }
        public decimal? ActivePct { get; set; }
        public int? NewThisMonth { get; set; }
        public int? InDefault { get; set; }
        public decimal? DefaultRatePct { get; set; }
        public int? NonPayingChange { get; set; }

        public decimal? ArrearsTotal { get; set; }
        public decimal? ArrearsChangePct { get; set; }

        public decimal? BadDebtThisMonth { get; set; }
        public decimal? BadDebtChangePct { get; set; }

        public decimal? ActiveRateVsTargetPct { get; set; }

        public decimal? PortfolioGoodPct { get; set; }
        public decimal? PortfolioSlowPct { get; set; }
        public decimal? PortfolioArrearsPct { get; set; }
        public decimal? PortfolioNonPayingPct { get; set; }
        public decimal? PortfolioGoodPctChange { get; set; }

        public decimal? CollectionRatePct { get; set; }
        public decimal? CollectionRateChangePct { get; set; }
        public decimal? PortfolioAtRiskPct { get; set; }
        public decimal? PortfolioAtRiskChangePct { get; set; }

        public decimal? RepeatCustomerRatePct { get; set; }
        public decimal? AvgCustomerLifetimeValue { get; set; }
        public decimal? ChurnRatePct { get; set; }

        public int? CompletedContractsThisMonth { get; set; }
        public decimal? CompletedContractsChangePct { get; set; }
        public decimal? ContractCompletionRatePct { get; set; }
        public decimal? ContractCompletionRateChangePct { get; set; }
        public decimal? AvgTimeToCompletionMonths { get; set; }
        public decimal? TotalValueCompletedThisMonth { get; set; }

        // Admin-only fields (NULL for dealer/agent-scoped rows).
        public decimal? RevenueTargetThisMonth { get; set; }
        public int? GoodAccounts { get; set; }
        public int? BadAccounts { get; set; }
        public int? PayingAccounts { get; set; }
        public int? NonPayingAccounts { get; set; }
        public decimal? CostOfDevicesThisMonth { get; set; }
        public decimal? NetProfitChangePct { get; set; }
        public decimal? ProfitMarginChangePct { get; set; }
        public decimal? ProfitMarginTargetPct { get; set; }
        public decimal? OperatingExpensesThisMonth { get; set; }
        public decimal? TaxRatePct { get; set; }
        public decimal? DividendsPaidThisMonth { get; set; }
        public int? TotalCustomers { get; set; }
        public int? NewCustomersThisMonth { get; set; }

        // Dealer-only commission fields (no Admin equivalent).
        public decimal? CommissionReceived { get; set; }
        public decimal? CommissionPaidToAgents { get; set; }
        public decimal? CommissionOutstanding { get; set; }

        public DateTime RefreshedAtUtc { get; set; }
    }
}
