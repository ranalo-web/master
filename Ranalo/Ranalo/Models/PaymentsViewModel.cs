namespace Ranalo.Models
{
    public class PaymentsViewModel
    {
        public List<PaymentSummary>? Payments { get; set; }
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public int TotalRecords { get; set; }
        public string? SearchTerm { get; set; }
    }
}
