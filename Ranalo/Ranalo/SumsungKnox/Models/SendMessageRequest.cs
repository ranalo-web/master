namespace Ranalo.SumsungKnox.Models
{
    public class SendMessageRequest
    {
        public string? ObjectId { get; set; }
        public string? DeviceUid { get; set; }
        public string? ApproveId { get; set; }
        public string? RequestId { get; set; }

        public string? Tel { get; set; }
        public bool? EnableFullscreen { get; set; }

        public string? Message { get; set; }
        public long? MessageType { get; set; }
    }
}
