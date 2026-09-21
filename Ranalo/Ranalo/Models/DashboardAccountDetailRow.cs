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

        // Populated for the Approver Dashboard's system-wide (all-dealer)
        // use of this method -- which dealer this account belongs to, and a
        // contact number for the "Customers to Contact" list. Devices.
        // CustomerPhoneNumber is never actually populated in this dataset;
        // Woo_Orders.Phone (joined via KosePayments.MpesaCode, most recent
        // order) is the only reliably-populated phone source. Both null for
        // a single-dealer call where the caller doesn't need them.
        public string? DealerName { get; set; }
        public string? CustomerPhone { get; set; }

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

        // Contract_Info.BuyingPrice -- manually entered per-contract device
        // cost (not synced from WooCommerce; see ContractController's
        // "Update Buying Price" action). Null when never recorded, same
        // population DealerCommissionMissingCostCount already tracks. Used
        // for the Admin Dashboard's live Cost of Devices figure.
        public decimal? BuyingPrice { get; set; }
    }
}
