namespace Ranalo.Models
{
    public class RestructuredViewModel
    {
        public List<RestructuredRecord> Records { get; set; }
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public string? SearchTerm { get; set; }
        public int TotalRecords { get; internal set; }
    }
}
