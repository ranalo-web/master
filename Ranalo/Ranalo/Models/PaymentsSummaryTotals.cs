namespace Ranalo.Models
{
    public class PaymentsSummaryTotals
    {
        public string Account { get; set; }
        public decimal TotalPaid { get; set; }
        public DateTime First { get; set; }
        public DateTime Last { get; set; }
        public KosePayments FirstPayment { get; set; }
        public KosePayments LastPayment { get; set; }
    }
}
