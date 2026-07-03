using Ranalo.Calculator.Logic.Models;
using Ranalo.Models;

namespace Ranalo.DataStore
{
    public interface IContractRepository
    {
        Task<int> AddContractAsync(ContractInfo contract);
        Task<int> DeleteContractAsync(int contractId);
        Task<ContractViewModel> GetAllContractsAsync(int page, int pageSize, string searchParam = "");
        Task<ContractInfo?> GetContractByDeviceIdAsync(int deviceId);
        Task<ContractInfo?> GetContractByIdAsync(int contractId);
        Task<int> UpdateContractAsync(ContractInfo contract);
        Task<int> CreateRecoveredAccount(ContractInfo newContract);
        Task AssignContractToCollector(int contractId, int collectorUserId);
        Task<PaymentsViewModel> GetCollectorsContractSummaryAsync(int userId, int? accountId, int deviceGroupId = 0, int page = 1, int pageSize = 10, string searchTerm = "");
        Task<ContractViewModel> GetAccountsByDealerAsync(int dealerId, int page, int pageSize, string searchTerm);
    }
}