namespace Ranalo.Woocommece.Api.Models
{
    public class MobileStatusReport
    {
        public string DeviceGroup { get; set; }
        public int AccountNo { get; set; }
        public string ImeiNo { get; set; }
        public string FirstName { get; set; }
        public decimal Deposit { get; set; }
        public string RePaymentIntervals { get; set; } = "Daily";
        public decimal Daily { get; set; }
        public decimal Weekly { get; set; }
        public decimal Monthly { get; set; }
        public int TermInMonths { get; set; } = 12;
        public decimal TotalPaid { get; set; }
        public decimal TotalDue { get; set; }
        public decimal Arrears { get; set; }
        public string Make { get; set; }
        public string Model { get; set; }
        public DateTime? LastPaymentDate { get; set; }
        public DateTime? LastConnectedAt { get; set; }
        public bool Locked { get; set; }
        public DateTime? EnrolledOn { get; set; }
        public DateTime? DateEnrolled { get; set; }
        public decimal Comms { get; set; }
        public decimal ArrearsAmt { get; set; }
        public DateTime? SaleWeek { get; set; }
        public string LiveFlag { get; set; }
        public int DeviceGroupId { get; set; }
        public int NotPaying7D { get; set; }
        public int LagDays { get; set; }
    }
}
