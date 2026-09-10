using System.Text.Json.Serialization;

namespace Ranalo.PayTrigger.Models
{
    // POST api/partner/lock/v1/imei/input -- pre-enrol one or more IMEIs.
    // NOTE: ImeiInfo is a JSON-encoded STRING (an array serialized to text),
    // not a nested JSON array -- that's how PayTrigger's API is documented
    // and how its signing example is built. Build it by serializing a
    // List<ImeiEnrollItem> and assigning the resulting string here.
    public class PreEnrollImeiRequest
    {
        [JsonPropertyName("imeiInfo")]
        public string ImeiInfo { get; set; } = null!;

        // Whether to lock immediately on activation (true), or wait until
        // Expiration and apply the configured lock policy (false). false
        // requires Expiration to be set on every item.
        [JsonPropertyName("preLockFlag")]
        public bool PreLockFlag { get; set; }

        [JsonPropertyName("apiKey")]
        public string ApiKey { get; set; } = null!;
    }

    public class ImeiEnrollItem
    {
        [JsonPropertyName("imei")]
        public string Imei { get; set; } = null!;

        [JsonPropertyName("model")]
        public string? Model { get; set; }

        [JsonPropertyName("ram")]
        public string? Ram { get; set; }

        [JsonPropertyName("rom")]
        public string? Rom { get; set; }

        // 0-none 1000-Day 2000-Week 2100-Bi-weekly 3000-Month
        [JsonPropertyName("cycleType")]
        public int? CycleType { get; set; }

        // Required when PreLockFlag is false. Unix seconds.
        [JsonPropertyName("expiration")]
        public long? Expiration { get; set; }

        [JsonPropertyName("orderNum")]
        public string? OrderNum { get; set; }

        [JsonPropertyName("deeplink")]
        public string? Deeplink { get; set; }

        [JsonPropertyName("deeplinkPkg")]
        public string? DeeplinkPkg { get; set; }

        [JsonPropertyName("ruleNum")]
        public int? RuleNum { get; set; }

        [JsonPropertyName("nfcFlag")]
        public bool? NfcFlag { get; set; }

        [JsonPropertyName("planGaid")]
        public string? PlanGaid { get; set; }

        [JsonPropertyName("nextRepaymentTimeSwitch")]
        public bool? NextRepaymentTimeSwitch { get; set; }
    }

    public class PreEnrollImeiResultItem
    {
        [JsonPropertyName("imei")]
        public string? Imei { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("errCode")]
        public int? ErrCode { get; set; }
    }

    public class PreEnrollImeiResponse : PayTriggerApiResponse<List<PreEnrollImeiResultItem>>
    {
    }
}
