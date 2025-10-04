namespace Ranalo.Models
{
    public class StatusReportViewModel
    {
        public List<MobileStatusReport>? StatusReports { get; set; }
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public string? SearchTerm { get; set; }
        public int TotalRecords { get; internal set; }
    }
}
