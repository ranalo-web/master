using Ranalo.DataStore;
using Ranalo.Models;

namespace Ranalo.Services
{
    public interface IRepoStockService
    {
        Task<RepoStockListViewModel> GetListAsync(string status);
        Task<RepoStockDetailViewModel?> GetDetailAsync(int id);
        Task<RepoStockLookup?> LookupAsync(long accountNo);
        Task<RepoStockResult> AddAsync(long accountNo, string reason, string? condition, int userId);
        Task<RepoStockResult> SellAsync(int id, RepoSale sale, int userId);
        Task<IEnumerable<DataStore.DataModels.User>> GetAgentsAsync(int dealerReference);
    }

    // Repo stock (RepoStockRules): add a taken-back phone, sell it again.
    public class RepoStockService : IRepoStockService
    {
        private readonly IRepoStockRepository _repository;
        private readonly IRepository _dealers;
        private readonly ILogger<RepoStockService> _logger;

        public RepoStockService(IRepoStockRepository repository, IRepository dealers, ILogger<RepoStockService> logger)
        {
            _repository = repository;
            _dealers = dealers;
            _logger = logger;
        }

        public async Task<RepoStockListViewModel> GetListAsync(string status) => new()
        {
            Status = status,
            Items = await _repository.GetListAsync(status),
        };

        public async Task<RepoStockDetailViewModel?> GetDetailAsync(int id)
        {
            var item = await _repository.GetAsync(id);
            if (item == null) return null;
            return new RepoStockDetailViewModel
            {
                Item = item,
                OldContract = await _repository.GetOldContractAsync(item.OldContractId, item.OldPaymentAccount),
                PreviousPayments = await _repository.GetPaymentsAsync(item.OldPaymentAccount),
                Dealers = (await _dealers.GetAllDealersAsync()).OrderBy(d => d.CompanyName).ToList(),
            };
        }

        public Task<RepoStockLookup?> LookupAsync(long accountNo) => _repository.LookupAsync(accountNo);

        public async Task<RepoStockResult> AddAsync(long accountNo, string reason, string? condition, int userId)
        {
            var account = await _repository.LookupAsync(accountNo);
            var cannot = RepoStockRules.CannotAdd(account, reason);
            if (cannot != null)
            {
                return new(false, cannot);
            }

            var suffix = RepoStockRules.PaymentSuffix(account!.TimesInStockBefore);
            try
            {
                var id = await _repository.AddAsync(account, suffix, reason, string.IsNullOrWhiteSpace(condition) ? null : condition.Trim(), userId);
                _logger.LogInformation("Repo stock {Id}: account {Account} added by user {UserId} ({Reason}); contract {Contract} ended, payments now {Suffix}",
                    id, accountNo, userId, reason, account.ContractId, suffix);
                var ended = account.ContractEndedOn.HasValue
                    ? $"Contract {account.ContractId} (already ended {account.ContractEndedOn:dd/MM/yyyy}) is linked"
                    : $"Contract {account.ContractId} ended today";
                return new(true, $"Account {accountNo} is in repo stock. {ended}, and "
                    + $"{account.CustomerName}'s payments are now under {accountNo}{suffix}.", id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Repo stock: adding account {Account} failed", accountNo);
                return new(false, $"Couldn't add account {accountNo}: {ex.Message}");
            }
        }

        public async Task<RepoStockResult> SellAsync(int id, RepoSale sale, int userId)
        {
            var item = await _repository.GetAsync(id);
            if (item == null)
            {
                return new(false, "Not found.");
            }

            var cannot = RepoStockRules.CannotSell(item, sale);
            if (cannot != null)
            {
                return new(false, cannot, id);
            }

            var old = await _repository.GetOldContractAsync(item.OldContractId, item.OldPaymentAccount);
            if (old == null)
            {
                return new(false, $"The old contract {item.OldContractId} can't be found.", id);
            }

            var writeOff = RepoStockRules.WriteOff(old);
            try
            {
                var contractId = await _repository.SellAsync(item, sale, old, writeOff, userId);
                _logger.LogInformation("Repo stock {Id}: account {Account} sold by user {UserId}; new contract {Contract}, write-off {WriteOff}",
                    id, item.AccountNo, userId, contractId, writeOff);
                return new(true, $"Contract {contractId} created for {sale.CustomerName.Trim()} on account {item.AccountNo}, starting today. "
                    + (writeOff > 0 ? $"The old contract's unpaid {writeOff:N2} is waiting for approval in Write-offs." : "The old contract had nothing unpaid."), id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Repo stock {Id}: sale failed", id);
                return new(false, $"Couldn't create the contract: {ex.Message}", id);
            }
        }

        public async Task<IEnumerable<DataStore.DataModels.User>> GetAgentsAsync(int dealerReference)
        {
            var dealer = (await _dealers.GetAllDealersAsync()).FirstOrDefault(d => d.DealerReference == dealerReference.ToString());
            return dealer == null ? Enumerable.Empty<DataStore.DataModels.User>() : await _dealers.GetAgentsByDealerIdAsync(dealer.DealerId);
        }
    }
}
