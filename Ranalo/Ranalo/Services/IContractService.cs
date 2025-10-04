using Ranalo.Calculator.Logic.Models;
using Ranalo.Models;

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
    }
}