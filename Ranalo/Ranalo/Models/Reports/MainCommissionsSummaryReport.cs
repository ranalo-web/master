namespace Ranalo.Models.Reports
{
    public class MainCommissionsSummaryReport
    {
        public string ContractID { get; set; }

        public long AccountNo { get; set; }

        public string First_Name { get; set; }

        public decimal Deposit { get; set; }

        public decimal TotalAmount { get; set; }

        public decimal TotalPaid { get; set; }

        public decimal DealerThreshold { get; set; }

        public decimal AgentCommission { get; set; }

        public decimal DealerCommission { get; set; }

        public bool DealerEligible { get; set; }

        public DateTime? LastPaymentDate { get; set; }

        public string DeviceName { get; set; }

        public string CustomerPhoneNumber { get; set; }

        public int? DeviceGroupId { get; set; }

        public DateTime Created { get; set; }
    }
}
