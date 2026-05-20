namespace Ranalo.Models.Reports
{
    public class DealerCommissionReadyToPayReport
    {
        public long AccountNo { get; set; }

        public string First_Name { get; set; }

        public decimal TotalAmount { get; set; }
        public decimal Total_Cost { get; set; }
        public decimal DealerThreshold { get; set; }
        public decimal EarnedDealerCommission { get; set; }
        public decimal AmountReadyToPay { get; set; }

        public decimal TotalPaid { get; set; }

        public decimal DealerCommission { get; set; }

        public int? DeviceGroupId { get; set; }

        public string Status { get; set; }
    }
}
