using System.Text.Json.Serialization;

namespace Ranalo.SumsungKnox.Models
{
    public class ApproveDeviceRequest
    {
        [JsonPropertyName("deviceUid")]
        public string DeviceUid { get; set; } = null!;

        [JsonPropertyName("approveId")]
        public string? ApproveId { get; set; }

        [JsonPropertyName("approveComment")]
        public string? ApproveComment { get; set; }
    }
}
