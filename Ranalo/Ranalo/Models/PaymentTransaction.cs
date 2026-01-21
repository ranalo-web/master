namespace Ranalo.Models
{
    public class PaymentTransaction
    {
        public string ReceiptNo { get; set; }
        public string CompletionTime { get; set; }
        public string InitiationTime { get; set; }
        public string Details { get; set; }
        public string Status { get; set; }
        public decimal PaidIn { get; set; }
        public decimal Balance { get; set; }
        public string BalanceConfirmed { get; set; }
        public string Reason { get; set; }
        public string OtherPartyInfo { get; set; }
        public string AccountNumber { get; set; }
        public string Currency { get; set; }
    }
}
