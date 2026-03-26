namespace Ranalo.Models
{
    public class PaymentsSummaryTotals
    {
        public string Account { get; set; }
        public decimal TotalPaid { get; set; }
        public DateTime? First { get; set; }
        public DateTime? Last { get; set; }
        public decimal? FirstPayment { get; set; }
        public decimal? LastPayment { get; set; }
    }

    public class PaymentsSummaryTotalsViewModel
    {
        public List<PaymentsSummaryTotals>? Payments { get; set; }
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public int TotalRecords { get; set; }
        public string? SearchTerm { get; set; }
    }
}
