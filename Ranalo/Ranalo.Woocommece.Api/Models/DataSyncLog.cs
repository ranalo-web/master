namespace Ranalo.Woocommece.Api.Models
{
    public class DataSyncLog
    {
        public int Id { get; set; }
        public long LastSyncedOrderId { get; set; }
        public DateTime LastOrderCreatedDate { get; set; }
        public DateTime SyncDate { get; set; }
        public SyncStatus Status { get; set; }
        public SyncType Type { get; set; }

    }
}
