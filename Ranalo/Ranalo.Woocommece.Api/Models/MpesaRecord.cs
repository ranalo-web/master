using Newtonsoft.Json;

namespace Ranalo.Woocommece.Api.Models
{
    public class MpesaRecord
    {
        public int Id { get; set; }

        [JsonProperty("account_no")] 
        public required string AccountNo { get; set; }
        [JsonProperty("mpesa_code")]
        public required string MpesaCode { get; set; }
        [JsonProperty("amount")]
        public required string Amount { get; set; }
        [JsonProperty("payment_date")]
        public required string PaymentDate { get; set; }

        public decimal AmountValue => decimal.Parse(Amount);
        public DateTime PaymentDateValue => DateTime.Parse(PaymentDate);
    }
}
