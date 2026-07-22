using Ranalo.Calculator.Logic.Models;
using Ranalo.Models;
using Ranalo.Woocommece.Api.Models;

namespace Ranalo.Services
{
    public interface IContractService
    {
        Task<int> AddContractAsync(ContractInfo contract);
        Task<int> DeleteContractAsync(int contractId);
        Task<ContractViewModel> GetAllContractsAsync(int page, int pageSize, string searchParam = "");
        Task<ContractInfo?> GetContractByDeviceIdAsync(int deviceId);
        Task<ContractInfo?> GetContractByIdAsync(int contractId);
        Task<int> UpdateContractAsync(ContractInfo contract);
        Task<int> CreateRecoveredAccountAsync(ContractCreateDto newContract);
        Task AssignContractToCollector(int contractId, int collectorUserId);

        Task AssignAccountToAgent(int contractId, int agentId);

        Task<StatusReportViewModel> GetCollectorsContractSummaryAsync(int userId, int? accountId, int deviceGroupId = 0, int page = 1, int pageSize = 10, string searchTerm = "");
        Task<ContractViewModel> GetAccountsByDealer(int dealerId, int page, int pageSize, string searchTerm);

        Task<ContractViewModel> GetAssignedAccountsByDealer(int dealerId, int page, int pageSize, string searchTerm);
    }
}