namespace Ranalo.Models
{
    // Raw inputs per account, from DashboardReportRepository.GetCommissionAccountInputsAsync.
    public class CommissionAccountInputRow
    {
        public long AccountId { get; set; }
        public string CustomerName { get; set; } = "";
        public int? AgentId { get; set; }
        public string? AgentName { get; set; }
        public string DealerName { get; set; } = "";
        public decimal Deposit { get; set; }
        public int DaysSinceStart { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal? BuyingPrice { get; set; }
        public decimal AgentGrossCommission { get; set; }
        public decimal ArrearsDeducted { get; set; }
        public decimal AgentPaid { get; set; }
        public decimal DealerPaid { get; set; }
    }

    // One row of the Account Commissions page, with each step of the
    // calculation broken out. Same formulas as the dashboard cards:
    //   Agent upfront  = 50% of deposit
    //   Agent bonus    = 25% of deposit once the contract is 90+ days old
    //   Agent net      = upfront + bonus - arrears deducted - paid to agent
    //   Dealer base    = total paid - buying price - agent commission (upfront + bonus)
    //   Dealer comm.   = 30% of dealer base (never below 0; blank without a buying price)
    //   Dealer balance = dealer commission - paid to dealer
    public class AccountCommissionRow
    {
        public long AccountId { get; set; }
        public string CustomerName { get; set; } = "";
        public string? AgentName { get; set; }
        public string DealerName { get; set; } = "";
        public int DaysSinceStart { get; set; }

        public decimal Deposit { get; set; }
        public decimal AgentUpfront { get; set; }
        public decimal AgentBonus { get; set; }
        public bool AgentBonusEarned { get; set; }
        public int DaysToBonus { get; set; }
        public decimal ArrearsDeducted { get; set; }
        public decimal AgentPaid { get; set; }
        public decimal AgentNet { get; set; }

        public decimal TotalPaid { get; set; }
        public decimal? BuyingPrice { get; set; }
        public decimal? DealerBase { get; set; }
        public decimal? DealerCommission { get; set; }
        public decimal DealerPaid { get; set; }
        public decimal? DealerBalance { get; set; }
    }

    public class AccountCommissionsViewModel
    {
        // Agents see only their own accounts and agent commission.
        public bool IsAgentView { get; set; }
        // Admins see every dealer, so the Dealer column is shown.
        public bool ShowDealer { get; set; }

        public List<AccountCommissionRow> Rows { get; set; } = new();

        public decimal AgentEarnedTotal { get; set; }
        public decimal AgentDeductedTotal { get; set; }
        public decimal AgentPaidTotal { get; set; }
        public decimal AgentNetTotal { get; set; }
        public decimal DealerCommissionTotal { get; set; }
        public decimal DealerPaidTotal { get; set; }
        public decimal DealerBalanceTotal { get; set; }
        public int MissingBuyingPriceCount { get; set; }
    }
}
