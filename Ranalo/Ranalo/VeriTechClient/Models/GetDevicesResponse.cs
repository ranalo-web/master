namespace Ranalo.VeriTechClient.Models
{
    public class GetDevicesResponse
    {
        public GetDevicesData Data { get; set; }
        public string Message { get; set; }
    }

    public class GetDevicesData
    {
        public string Result { get; set; }
        public int TotalCount { get; set; }
        public List<GetDeviceInfoModel> DeviceList { get; set; }
    }

    public class GetDeviceInfoModel
    {
        public string ObjectId { get; set; }
        public string ImeiNumber { get; set; }
        public string Status { get; set; }
    }
}
