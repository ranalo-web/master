using Ranalo.Woocommece.Api.Models;

namespace Ranalo.Woocommece.Api.DataStore
{
    public interface IKosePaymentsRepository
    {
        Task<IEnumerable<MpesaRecord>> GetAllAsync();
        Task<MpesaRecord?> GetByIdAsync(int id);
        Task<int> InsertAsync(MpesaRecord record);

        Task SaveToDatabaseAsync(Dictionary<string, List<MpesaRecord>> groupedRecords);

        Task SaveDevicesToDatabaseAsync(List<Device> groupedRecords);
    }
}