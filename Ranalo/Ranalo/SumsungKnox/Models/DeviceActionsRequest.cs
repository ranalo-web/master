using System.Text.Json.Serialization;

namespace Ranalo.SumsungKnox.Models
{
    public class DeviceActionsRequest
    {
        [JsonPropertyName("objectId")]
        public string? ObjectId { get; set; }

        [JsonPropertyName("deviceUid")]
        public string? DeviceUid { get; set; }

        [JsonPropertyName("approveId")]
        public string? ApproveId { get; set; }

        [JsonPropertyName("actions")]
        public List<DeviceActionItem> Actions { get; set; } = new();

    }

    public class DeviceActionItem
    {
        [JsonPropertyName("action")]
        public string Action { get; set; } = null!;   // "lock" or "unLock"

        [JsonPropertyName("timestamp")]
        public long Timestamp { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }
    }
}
