namespace Ranalo.Models
{
    // One row per active account for a dealer -- the single live source feeding
    // Non-Payers/Slow-Payers/Good-Payers, Agent Performance, My Contracts, and
    // Contracts Ending Soon on the Dealer Dashboard, so every one of those
    // sections classifies accounts the same way the top KPI cards already do
    // (see IDashboardReportRepository.GetDealerAccountDetailsAsync) instead of
    // each re-deriving its own rule.
    public class DashboardAccountDetailRow
    {
        public long AccountId { get; set; }
        public string CustomerName { get; set; } = "";
        public int? AssignedAgentId { get; set; }
        public string? AgentName { get; set; }
        public string DeviceName { get; set; } = "";

        // Raw Devices.NextLockDateIsoFormat -- parse with
        // DashboardReportRepository.ParseNextLockDate before use.
        public string? NextLockDateRaw { get; set; }

        // Accrual formula (Deposit + Daily/Weekly/Monthly accrual since
        // StartDate, capped at contract term) minus TotalPaid -- same as
        // GetDealerArrearsClassificationAsync. Negative = shortfall (arrears),
        // positive = surplus (paid ahead).
        public decimal ArrearsAmount { get; set; }

        // Daily + Weekly/7 + Monthly/30 -- used to express a Good account's
        // surplus as "N payments ahead".
        public decimal DailyBlendedRate { get; set; }

        // Daily*30 + Weekly*30/7 + Monthly -- the account's blended monthly
        // payment, shown on My Contracts.
        public decimal MonthlyPayment { get; set; }

        public decimal TotalPaid { get; set; }

        // Deposit + full-term accrual -- same formula as
        // RefreshCompletedContractsAsync's FullContractValue.
        public decimal FullContractValue { get; set; }

        public DateTime StartDate { get; set; }
    }
}
