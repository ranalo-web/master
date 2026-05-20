namespace Ranalo.Models.Reports
{
    public class OutstandingDealerCommissionReport
    {
        public long AccountNo { get; set; }

        public string First_Name { get; set; }

        public decimal TotalAmount { get; set; }

        public decimal TotalPaid { get; set; }

        public decimal RemainingDealerBalance { get; set; }
        public decimal EarnedDealerCommission { get; set; }
        public decimal TotalDealerPaid { get; set; }
        public decimal DealerCommission { get; set; }
        public decimal DealerThreshold { get; set; }
        public decimal DeviceAmount { get; set; }

        public int? DeviceGroupId { get; set; }
    }
}
