namespace Ranalo.Woocommece.Api.Models
{
    public class SyncPaymentsLog
    {
        public int Id { get; set; }
        public int LastPaymentId { get; set; }
        public DateTime LastPaymentDate { get; set; }
        public DateTime CreatedDate { get; set; }
    }
}
