namespace Ranalo.Models
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
        public DateTime? FirstPaymentDate { get; set; }
        public string? LastConnectedAt { get; set; }
        public bool Locked { get; set; }
        public DateTime? EnrolledOn { get; set; }
        public string? DateEnrolled { get; set; }
        public decimal Comms { get; set; }
        public decimal ArrearsAmt { get; set; }
        public decimal FirstPaidAmt { get; set; }
        public decimal LastPaidAmt { get; set; }
        public DateTime? SaleWeek { get; set; }
        public string LiveFlag { get; set; }
        public int DeviceGroupId { get; set; }
        public int NotPaying7D { get; set; }
        public int LagDays { get; set; }
        public double NumberDaysLifeTime { get; set; }
        public decimal LoanBalance { get; set; }
        public decimal TotalLoan { get; set; }
        public string? NextLockDate { get; set; }
        public string? Status { get; set; }
        public string? LockType { get; set; }
        public decimal RestructuredAmnt { get; set; }
        public List<RestructuredRecord> RestructuredRecords { get; set; } = new List<RestructuredRecord>();
        public CustomerDetails? CustomerDetails { get; set; }
    }

    public class PaymentSummary
    {
        public int AccountNo { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal FirstPaymentAmount { get; set; }
        public decimal LastPaymentAmount { get; set; }
        public DateTime FirstPaidDate { get; set; }
        public DateTime? LastPaidDate { get; set; }
        public string? FirstMPesaCode { get; set; }
        public string? LastMPesaCode { get; set; }
        public string Make { get; set; }
        public string Model { get; set; }
        public string? LastConnectedAt { get; set; } //UTC date
        public bool Locked { get; set; }
        public string? EnrolledOn { get; set; }
        public string DateEnrolled { get; set; }
        public int DeviceGroupId { get; set; }
        public string Name { get; set; }
        public string ImeiNo { get; set; }
        public decimal TotalAmount { get; set; }
        public string NextLockDate { get; set; }
        public string? Status { get; set; }
        public string? LockType { get; set; }
        public string? CustomerName { get; set; }
        public decimal Daily { get; set; }
        public decimal Weekly { get; set; }
        public decimal Monthly { get; set; }
        public decimal Deposit { get; set; }
    }

}
