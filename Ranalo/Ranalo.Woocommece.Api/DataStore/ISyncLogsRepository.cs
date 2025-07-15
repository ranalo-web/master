using Ranalo.Woocommece.Api.Models;

namespace Ranalo.Woocommece.Api.DataStore
{
    public interface ISyncLogsRepository
    {
        Task<IEnumerable<DataSyncLog>> GetAllAsync();
        Task<DataSyncLog?> GetByIdAsync(int id);
        Task<SyncPaymentsLog?> GetLastPaymentLog();
        Task<DataSyncLog?> GetLastSyncLogAsync();
        Task<Device?> GetLatestDeviceAsync();
        Task<int> InsertAsync(DataSyncLog log);
        Task InsertPaymentLogAsync(SyncPaymentsLog log);
    }
}