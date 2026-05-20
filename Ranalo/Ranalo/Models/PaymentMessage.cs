namespace Ranalo.Models
{
    public class PaymentMessage
    {
        public int Id { get; set; }
        public string? AccountNo { get; set; }
        public string? FirstName { get; set; }
        public decimal AmountValue { get; set; }
        public DateTime PaymentDateValue { get; set; }
        public string? MpesaCode { get; set; }
        public string? Imei { get; set; }
        public int LockGroup { get; set; }
    }
}
