namespace Ranalo.Models
{
    public class AccountSummary
    {
        public long AccountId { get; set; }
        public DateTime LastPaymentDate { get; set; }
        public DateTime FirstPaymentDate { get; set; }
        public decimal LoanBalance { get; set; }
        public decimal Daily { get; set; }
        public decimal Weekly { get; set; }
        public decimal Monthly { get; set; }
        public decimal Deposit { get; set; }
        public decimal TotalPaid { get; set; }
        public string FirstName { get; set; }
        public decimal FirstAmount { get; set; }
        public decimal LastPaidAmount { get; set; }
        //LastPaidAmount
        public decimal Arrears { get; set; }
        public DateTime ContractEndDate { get; set; }
        public decimal DailyPaymentALL { get; set; }
        public decimal PaidLast24Hours { get; set; }
        public decimal UnitsLeft { get; set; }
        public decimal TermsInMonths { get; set; }
        public DateTime AutoLockDatePmtR { get; set; }
        public string? NextLockDate { get; set; }
        public string? LastConnectedAt { get; set; }
        public int? LockGroup { get; set; }

    }
}
