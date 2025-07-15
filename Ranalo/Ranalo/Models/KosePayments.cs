namespace Ranalo.Models
{
    public class KosePayments
    {
        public string? MpesaCode { get; set; }
        public string? AccountNo { get; set; }
        public decimal AmountValue { get; set; }
        public DateTime PaymentDateValue { get; set; }
    }
}
