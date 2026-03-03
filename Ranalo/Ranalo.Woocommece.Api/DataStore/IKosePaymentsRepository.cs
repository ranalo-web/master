using Ranalo.Calculator.Logic.Models;
using Ranalo.Woocommece.Api.Models;

namespace Ranalo.Woocommece.Api.DataStore
{
    public interface IKosePaymentsRepository
    {
        Task<IEnumerable<MpesaRecord>> GetAllAsync();
        Task<MpesaRecord?> GetByIdAsync(int id);
        Task<int> InsertAsync(MpesaRecord record);

        Task<List<string>> SaveToDatabaseAsync(Dictionary<string, List<MpesaRecord>> groupedRecords);

        Task SaveDevicesToDatabaseAsync(List<Device> groupedRecords);
        Task UpdateDevicesToDatabaseAsync(List<Device> groupedRecords);

        Task<int> AddContractAsync(ContractInfo contract);
        Task UpdateOrderContract(long orderId, int contractId);
        Task SaveDeviceToDatabaseAsync(Device device);
        Task UpdateDeviceToDatabaseAsync(Device device);

        Task<Device?> GetDeviceByAccountId(long accountId);
    }
}