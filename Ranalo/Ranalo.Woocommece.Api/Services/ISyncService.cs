using Ranalo.Woocommece.Api.Models;

namespace Ranalo.Woocommece.Api.Services
{
    public interface ISyncService
    {
        Task<int> CreateOrderAsync(List<WooOrder> orders);
        Task<WooOrder?> GetLastCreatedOrderAsync();
        Task<DataSyncLog?> GetLastSycnLogDetails();
        Task<SyncPaymentsLog?> GetLastTransactionDateAsync();
        Task LogLastSyncDetails(DataSyncLog log);

        Task LogLastPaymentSyncDetails(SyncPaymentsLog log);
        Task<int> CreateKosePaymentAsync(MpesaRecord record);

        Task CreateKoseBatchPaymentAsync(Dictionary<string, List<MpesaRecord>> records);

        Task CreateDevicesToDatabaseAsync(List<Device> groupedRecords);
        Task<Device?> GetLatestDeviceId();

        Task<List<int>> UpdateImagesAsync(long orderId, List<ImagesMetadata> imagesForUpdate);
    }
}