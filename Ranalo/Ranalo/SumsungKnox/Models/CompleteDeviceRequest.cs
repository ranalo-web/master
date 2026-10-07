using System.Text.Json.Serialization;

namespace Ranalo.SumsungKnox.Models
{
    public class CompleteDeviceRequest
    {
        // Knox expects camelCase names, like the other Knox requests.
        [JsonPropertyName("objectId")]
        public string? ObjectId { get; set; }

        [JsonPropertyName("deviceUid")]
        public string? DeviceUid { get; set; }

        [JsonPropertyName("approveId")]
        public string? ApproveId { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }
    }
}
