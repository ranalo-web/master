using Ranalo.Models;

namespace Ranalo.DataStore
{
    public interface IPaymentsRepository
    {
        Task CreateMessageLogAsync(MessageLog record);
        Task<Dictionary<string, string>> GetAccountNames(IEnumerable<string> accountIds);
        Task<KosePaymentsViewModel> GetAllPaymentsAsync(int page = 1, int pageSize = 10);
        Task<KosePaymentsViewModel> GetOrphanedPaymentsAsync(int page = 1, int pageSize = 10);

        Task<List<AccountSummary>> GetLiveQualifyingLockAccounts();
    }
}