using System.Text.Json.Serialization;

namespace Ranalo.PayTrigger.Models
{
    // POST api/partner/lock/v1/findLockState -- looks up a single device's
    // current lock/activation state. This is the real equivalent of Knox's
    // ListDevicesAsync device lookup used right after enrolment approval to
    // populate our own Device row.
    public class FindLockStateRequest
    {
        [JsonPropertyName("deviceTag")]
        public string? DeviceTag { get; set; }

        [JsonPropertyName("imei")]
        public string? Imei { get; set; }

        // Can look up an already-removed device by order number.
        [JsonPropertyName("orderNum")]
        public string? OrderNum { get; set; }

        [JsonPropertyName("apiKey")]
        public string ApiKey { get; set; } = null!;
    }

    public class FindLockStateData
    {
        [JsonPropertyName("deviceTag")]
        public string? DeviceTag { get; set; }

        // Client state: 0-unregistered 1000-registered 2000-ready_to_activate
        // 3000-active 5000-removable
        [JsonPropertyName("lockState")]
        public int LockState { get; set; }

        // 1000-locked 2000-unlock
        [JsonPropertyName("mobileStatus")]
        public int MobileStatus { get; set; }

        [JsonPropertyName("serverState")]
        public int ServerState { get; set; }

        [JsonPropertyName("serverLockStatus")]
        public int ServerLockStatus { get; set; }

        [JsonPropertyName("activeTime")]
        public long? ActiveTime { get; set; }

        [JsonPropertyName("expiration")]
        public long? Expiration { get; set; }

        [JsonPropertyName("lastConnectTime")]
        public long? LastConnectTime { get; set; }

        [JsonPropertyName("orderNum")]
        public string? OrderNum { get; set; }

        [JsonPropertyName("imei")]
        public string? Imei { get; set; }

        [JsonPropertyName("model")]
        public string? Model { get; set; }

        [JsonPropertyName("ram")]
        public int? Ram { get; set; }

        [JsonPropertyName("rom")]
        public int? Rom { get; set; }

        [JsonPropertyName("frameworkVersion")]
        public string? FrameworkVersion { get; set; }

        [JsonPropertyName("apkVersion")]
        public string? ApkVersion { get; set; }

        [JsonPropertyName("buildNumber")]
        public string? BuildNumber { get; set; }

        [JsonPropertyName("clientRemoveTime")]
        public long? ClientRemoveTime { get; set; }

        [JsonPropertyName("serverRemoveTime")]
        public long? ServerRemoveTime { get; set; }
    }

    public class FindLockStateResponse : PayTriggerApiResponse<FindLockStateData>
    {
    }
}
