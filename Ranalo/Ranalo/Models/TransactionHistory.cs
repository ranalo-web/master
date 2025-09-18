namespace Ranalo.Models
{
    public class TransactionHistory
    {
        public long AccountNo { get; set; }
        public string Status { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public DateTime DateCreated { get; set; }
        public string MpesaDepositRef { get; set; }
        public string DealerRef { get; set; }
        public decimal Amount { get; set; }
    }
}
