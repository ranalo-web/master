namespace Ranalo.Models.Reports
{
    // Dealer Outstanding row: dealer commission not yet fully paid, including
    // any part currently held back for arrears.
    public class OutstandingDealerCommissionReport
    {
        public long AccountNo { get; set; }
        public string First_Name { get; set; } = "";
        public string DealerName { get; set; } = "";
        public decimal TotalPaid { get; set; }
        public decimal BuyingPrice { get; set; }
        public decimal EarnedDealerCommission { get; set; }
        public decimal TotalDealerPaid { get; set; }

        // Earned - paid, before the arrears deduction.
        public decimal Outstanding { get; set; }
        public decimal ArrearsDeducted { get; set; }

        // Earned - arrears deducted - paid. Can be below 0.
        public decimal RemainingDealerBalance { get; set; }
    }
}
