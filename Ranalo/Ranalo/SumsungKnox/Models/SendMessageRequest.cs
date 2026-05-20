using System.Text.Json.Serialization;

namespace Ranalo.SumsungKnox.Models
{
    public class SendMessageRequest
    {
        [JsonPropertyName("objectId")]
        public string? ObjectId { get; set; }
        [JsonPropertyName("deviceUid")]
        public string? DeviceUid { get; set; }
        [JsonPropertyName("approveId")]
        public string? ApproveId { get; set; }
        [JsonPropertyName("tel")]
        public string? Tel { get; set; }
        [JsonPropertyName("enableFullScreen")]
        public bool? EnableFullscreen { get; set; }
        [JsonPropertyName("message")]
        public string? Message { get; set; }
        [JsonPropertyName("messageType")]
        public long? MessageType { get; set; }
        
    }
}
