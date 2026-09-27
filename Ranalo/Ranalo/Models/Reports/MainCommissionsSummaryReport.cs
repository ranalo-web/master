namespace Ranalo.Models.Reports
{
    // Commissions Overview row. Figures come from Services.CommissionCalculator.
    public class MainCommissionsSummaryReport
    {
        public string? ContractID { get; set; }
        public long AccountNo { get; set; }
        public string First_Name { get; set; } = "";
        public DateTime StartDate { get; set; }
        public string DealerName { get; set; } = "";
        public string? AgentName { get; set; }

        // Contract total cost.
        public decimal? TotalAmount { get; set; }
        public decimal Deposit { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal? BuyingPrice { get; set; }

        // Agent commission earned (upfront + bonus); 0 on a direct sale.
        public decimal AgentCommission { get; set; }
        public decimal AgentArrearsDeducted { get; set; }

        // Null when no buying price is recorded.
        public decimal? DealerCommission { get; set; }
        public decimal DealerArrearsDeducted { get; set; }
        public decimal? DealerBalance { get; set; }

        // Dealer commission is payable now (balance after arrears above 0).
        public bool DealerEligible { get; set; }
    }
}
