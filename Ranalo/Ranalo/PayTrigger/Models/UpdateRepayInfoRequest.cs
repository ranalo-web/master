using System.Text.Json.Serialization;

namespace Ranalo.PayTrigger.Models
{
    // POST api/partner/lock/v1/updateRepayInfo -- called after each payment
    // to unlock the device and push out the next lock/expiration date. This
    // is the real equivalent of Knox's ExecuteDeviceActionsAsync
    // unlock-then-lock pair for payment-driven lock extension.
    //
    // NOTE: unlike every other PayTrigger endpoint, the apiKey here is sent
    // as "relatedMerchant", not "apiKey" -- confirmed against the docs'
    // worked signing example, where relatedMerchant's value is literally the
    // signing key used.
    public class UpdateRepayInfoRequest
    {
        [JsonPropertyName("deviceTag")]
        public string? DeviceTag { get; set; }

        [JsonPropertyName("imei")]
        public string? Imei { get; set; }

        [JsonPropertyName("repayedAmt")]
        public decimal? RepayedAmt { get; set; }

        [JsonPropertyName("totalAmt")]
        public decimal? TotalAmt { get; set; }

        // Required. Unix seconds, must be greater than "now".
        [JsonPropertyName("nextRepayTime")]
        public long NextRepayTime { get; set; }

        [JsonPropertyName("nextRepayAmt")]
        public decimal? NextRepayAmt { get; set; }

        [JsonPropertyName("currencyType")]
        public string? CurrencyType { get; set; }

        [JsonPropertyName("currentTerm")]
        public int? CurrentTerm { get; set; }

        [JsonPropertyName("totalTerm")]
        public int? TotalTerm { get; set; }

        // Required. This is the apiKey, despite the field name.
        [JsonPropertyName("relatedMerchant")]
        public string RelatedMerchant { get; set; } = null!;

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("phoneNum")]
        public string? PhoneNum { get; set; }

        [JsonPropertyName("orderNum")]
        public string? OrderNum { get; set; }

        [JsonPropertyName("deeplink")]
        public string? Deeplink { get; set; }

        [JsonPropertyName("deeplinkPkg")]
        public string? DeeplinkPkg { get; set; }

        // Lock policy rule 0-5. Omit to leave unchanged.
        [JsonPropertyName("ruleNum")]
        public int? RuleNum { get; set; }
    }
}
