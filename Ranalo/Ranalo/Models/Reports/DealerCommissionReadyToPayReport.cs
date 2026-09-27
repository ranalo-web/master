namespace Ranalo.Models.Reports
{
    // Dealer Ready To Pay row: dealer commission payable now, after arrears.
    public class DealerCommissionReadyToPayReport
    {
        public long AccountNo { get; set; }
        public string First_Name { get; set; } = "";
        public string DealerName { get; set; } = "";
        public decimal TotalPaid { get; set; }
        public decimal BuyingPrice { get; set; }
        public decimal EarnedDealerCommission { get; set; }
        public decimal ArrearsDeducted { get; set; }
        public decimal TotalDealerPaid { get; set; }
        public decimal AmountReadyToPay { get; set; }
        public string Status { get; set; } = "";
    }
}
