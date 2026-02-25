namespace Ranalo.VeriTechClient.Models
{
    public class CreateDeviceResponse
    {
        public UploadDeviceResultModel Data { get; set; }
        public string Message { get; set; }
    }

    public class UploadDeviceResultModel
    {
        public string Status { get; set; }
        public string Transaction_Id { get; set; }
        public string Data { get; set; }
        public string Message { get; set; }
        public int Code { get; set; }
    }
}
