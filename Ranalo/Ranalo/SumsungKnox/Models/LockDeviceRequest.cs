namespace Ranalo.SumsungKnox.Models
{
    public class LockDeviceRequest
    {
        public string? ObjectId { get; set; }
        public string? DeviceUid { get; set; }
        public string? ApproveId { get; set; }

        public string? Email { get; set; }
        public string? Tel { get; set; }

        public string Message { get; set; } = null!;
    }
}
