namespace Ranalo.Models
{
    public class KosePaymentsViewModel
    {
        public List<KosePayments>? Payments { get; set; }
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public string? SearchTerm { get; set; }
    }
}
