using System.Text.Json.Serialization;

namespace Ranalo.PayTrigger.Models
{
    // Body PayTrigger POSTs to our webhook when a device's real-world status
    // changes (activation, lock/unlock, removal), or when a strong-restriction
    // lock instruction couldn't take effect. See PayTriggerWebhookController.
    public class DeviceStatusCallback
    {
        [JsonPropertyName("productModel")]
        public string? ProductModel { get; set; }

        [JsonPropertyName("deviceTag")]
        public string? DeviceTag { get; set; }

        [JsonPropertyName("imei")]
        public string? Imei { get; set; }

        [JsonPropertyName("orderNum")]
        public string? OrderNum { get; set; }

        [JsonPropertyName("expiration")]
        public long? Expiration { get; set; }

        [JsonPropertyName("activeTime")]
        public long? ActiveTime { get; set; }

        // Client state: 0-unregistered 1000-registered 2000-ready_to_activate
        // 3000-active 5000-removable
        [JsonPropertyName("state")]
        public int? State { get; set; }

        // 1000-locked 2000-unlock
        [JsonPropertyName("mobileStatus")]
        public int? MobileStatus { get; set; }

        // Server state, same enum as State.
        [JsonPropertyName("serverState")]
        public int? ServerState { get; set; }

        [JsonPropertyName("clientRemoveTime")]
        public long? ClientRemoveTime { get; set; }

        [JsonPropertyName("serverRemoveTime")]
        public long? ServerRemoveTime { get; set; }

        // 1000-activation status changed, 2000-removal status changed,
        // 4000-strong-restriction lock instruction exceeded/blocked (device
        // state unchanged in this case -- see Tip).
        [JsonPropertyName("notifyType")]
        public int NotifyType { get; set; }

        // Only present when NotifyType == 4000.
        [JsonPropertyName("tip")]
        public string? Tip { get; set; }
    }
}
