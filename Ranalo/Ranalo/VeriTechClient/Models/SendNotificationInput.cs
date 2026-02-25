namespace Ranalo.VeriTechClient.Models
{
    public class SendNotificationInput
    {
        public string ImeiNumber { get; set; }
        public string Contact { get; set; }
        public bool EnableFullScreen { get; set; }
        public string Message { get; set; }
    }

    public class LockDeviceInput
    {
        public string ImeiNumber { get; set; }
        public string LockScreenMessage { get; set; }
    }

    public class UnlockDeviceInput
    {
        public string ImeiNumber { get; set; }
        public long RelockTimestamp { get; set; }
        public string LockScreenMessage { get; set; }
    }
}
