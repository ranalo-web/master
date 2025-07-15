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
        public DateTime ContractEndDate { get; set; }
    }
}
