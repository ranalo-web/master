using System.Text.Json.Serialization;

namespace Ranalo.PayTrigger.Models
{
    // POST api/partner/lock/v1/removeLock -- called once a device is fully
    // paid off, to release it from PayTrigger's device-lock management
    // entirely. This is the correct call for full payoff (unlike Knox, which
    // has no equivalent wired up -- see ScheduledLockFullyPaid.cs's note on
    // that pre-existing gap).
    public class RemoveLockRequest
    {
        [JsonPropertyName("deviceTag")]
        public string? DeviceTag { get; set; }

        [JsonPropertyName("imei")]
        public string? Imei { get; set; }

        [JsonPropertyName("apiKey")]
        public string ApiKey { get; set; } = null!;
    }
}
