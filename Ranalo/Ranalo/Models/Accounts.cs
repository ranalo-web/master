namespace Ranalo.Models
{
    public class AllAccounts
    {
        public int AccountNo { get; set; }
        public string CustomerName { get; set; }

        public string FirstMpesaCode { get; set; }

        public decimal TotalPaid { get; set; }

        public string? FirstPaidDate { get; set; }
        public decimal? FirstPaymentAmount { get; set; }

        public string? LastPaidDate { get; set; }
        public decimal? LastPaymentAmount { get; set; }

        public string Make { get; set; }
        public string Model { get; set; }
        public string? LastConnectedAt { get; set; }
        public bool Locked { get; set; }
        public string? EnrolledOn { get; set; }

        public int DeviceGroupId { get; set; }
        public string Name { get; set; }
        public string ImeiNo { get; set; }
        public string? NextLockDate { get; set; }

        public string Status { get; set; }
        public string LockType { get; set; }
        public decimal TotalAmount { get; set; }

        public decimal? Arrears { get; set; }

        public decimal Daily { get; set; }
        public decimal Weekly { get; set; }
        public decimal Monthly { get; set; }
        public decimal Deposit { get; set; }
        public decimal TermsInMonths { get; set; }
    }
}
