namespace Ranalo.Models
{
    // One account with its full commission breakdown, from
    // DashboardReportRepository.GetCommissionAccountsAsync.
    public class CommissionAccount
    {
        public long AccountId { get; set; }
        public string? ContractId { get; set; }
        public string CustomerName { get; set; } = "";
        public int? AgentId { get; set; }
        public string? AgentName { get; set; }
        public int DealerId { get; set; }
        public string DealerName { get; set; } = "";
        public DateTime StartDate { get; set; }
        public int DaysSinceStart { get; set; }
        public decimal Deposit { get; set; }
        public decimal? TotalCost { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal? BuyingPrice { get; set; }

        // Days past Devices.NextLockDateIsoFormat (0 or less when not yet
        // due); over 7 counts toward the payee's default rate.
        public double DaysPastLock { get; set; }
        public string? ProductName { get; set; }
        public string? Imei { get; set; }

        // Of Commission.AgentPaid, what was paid against the bonus; the rest was upfront.
        public decimal AgentBonusPaid { get; set; }
        public Ranalo.Services.CommissionBreakdown Commission { get; set; } = new();

        // Set when the account earns no commission (legacy etc.).
        public CommissionExclusion? Exclusion { get; set; }
    }

    // One row of the Account Commissions page. The figures come from
    // Services.CommissionCalculator; see its header for the formulas.
    public class AccountCommissionRow
    {
        public long AccountId { get; set; }
        public string CustomerName { get; set; } = "";
        public string? AgentName { get; set; }
        public string DealerName { get; set; } = "";
        public int DaysSinceStart { get; set; }
        public decimal Deposit { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal? BuyingPrice { get; set; }
        public Ranalo.Services.CommissionBreakdown Commission { get; set; } = new();

        // Expandable "how it's worked out" panel.
        public string? ContractId { get; set; }
        public string? ProductName { get; set; }
        public string? Imei { get; set; }
        public DateTime StartDate { get; set; }
        public decimal? ContractValue { get; set; }
        public int DaysPastLock { get; set; }
        public decimal AgentBonusPaid { get; set; }
        public CommissionExclusion? Exclusion { get; set; }
    }

    public class AccountCommissionsViewModel
    {
        // Agents see only their own accounts and agent commission.
        public bool IsAgentView { get; set; }
        // Admins see every dealer, so the Dealer column is shown.
        public bool ShowDealer { get; set; }

        public List<AccountCommissionRow> Rows { get; set; } = new();

        // Status filter (CommissionStatus.*, null = all) and search, admin only.
        public string? StatusFilter { get; set; }
        public string? Search { get; set; }
        public int TotalAccounts { get; set; }
        public List<string> Messages { get; set; } = new();
        public List<string> Errors { get; set; } = new();

        // Pooled like the dashboard cards: arrears on one account reduce what
        // is owed on the others, and Owed is never below 0.
        public Ranalo.Services.CommissionPool Agent { get; set; } = new();
        public Ranalo.Services.CommissionPool Dealer { get; set; } = new();
        public int MissingBuyingPriceCount { get; set; }

        // Legacy etc.: earn no commission; Recovering = arrears still deducted from the dealer.
        public int NoCommissionCount { get; set; }
        public int RecoveringCount { get; set; }

        // Admins can change an account's commission status from the page.
        public bool CanEditStatus { get; set; }
    }
}
