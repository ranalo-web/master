namespace Ranalo.Models
{
    public class DevicesWithDealerViewModel
    {
        public List<DeviceWithDealerDto>? Devices { get; set; }
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public string? SearchTerm { get; set; }
        public int TotalRecords { get; set; }
        public int PageSize { get; set; } = 10;

        public List<string>? Errors { get; set; } = new List<string>();
    }
}
