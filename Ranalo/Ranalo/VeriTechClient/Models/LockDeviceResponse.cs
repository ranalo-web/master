namespace Ranalo.VeriTechClient.Models
{
    public class LockDeviceResponse
    {
        public LockDeviceResultModel Data { get; set; }
        public string Message { get; set; }
    }

    public class LockDeviceResultModel
    {
        public string Status { get; set; }
        public string Result { get; set; }
        public string Message { get; set; }
        public int Code { get; set; }
    }
}
