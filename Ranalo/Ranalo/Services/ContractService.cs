using Ranalo.Calculator.Logic.Models;
using Ranalo.DataStore;
using Ranalo.Models;

namespace Ranalo.Services
{
    public class ContractService : IContractService
    {
        private readonly IContractRepository _contractRepository;
        public ContractService(IContractRepository contractRepository)
        {
            _contractRepository = contractRepository;
        }
        public async Task<int> AddContractAsync(ContractInfo contract)
        {
            return await _contractRepository.AddContractAsync(contract);
        }
        public async Task<int> DeleteContractAsync(int contractId)
        {
            return await _contractRepository.DeleteContractAsync(contractId);
        }
        public async Task<ContractViewModel> GetAllContractsAsync(int page, int pageSize, string searchParam = "")
        {
            return await _contractRepository.GetAllContractsAsync(page, pageSize, searchParam);
        }

       public async Task<ContractInfo?> GetContractByDeviceIdAsync(int deviceId)
        {
            return await _contractRepository.GetContractByDeviceIdAsync(deviceId);
        }
        public async Task<ContractInfo?> GetContractByIdAsync(int contractId)
        {
            return await _contractRepository.GetContractByIdAsync(contractId);
        }
        public async Task<int> UpdateContractAsync(ContractInfo contract)
        {
            return await _contractRepository.UpdateContractAsync(contract);
        }
    }
}
