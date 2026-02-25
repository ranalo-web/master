namespace Ranalo.VeriTechClient.Models
{
    public class UnlockDeviceResponse
    {
        public UnlockDeviceResultModel Data { get; set; }
        public string Message { get; set; }
    }

    public class UnlockDeviceResultModel
    {
        public string Status { get; set; }
        public string Result { get; set; }
        public string Message { get; set; }
        public int Code { get; set; }
    }
}
