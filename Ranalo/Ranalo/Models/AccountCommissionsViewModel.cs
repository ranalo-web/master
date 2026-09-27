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
        public Ranalo.Services.CommissionBreakdown Commission { get; set; } = new();
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
    }

    public class AccountCommissionsViewModel
    {
        // Agents see only their own accounts and agent commission.
        public bool IsAgentView { get; set; }
        // Admins see every dealer, so the Dealer column is shown.
        public bool ShowDealer { get; set; }

        public List<AccountCommissionRow> Rows { get; set; } = new();

        // Pooled like the dashboard cards: arrears on one account reduce what
        // is owed on the others, and Owed is never below 0.
        public Ranalo.Services.CommissionPool Agent { get; set; } = new();
        public Ranalo.Services.CommissionPool Dealer { get; set; } = new();
        public int MissingBuyingPriceCount { get; set; }
    }
}
