using Ranalo.Models;

namespace Ranalo.DataStore
{
    public interface IPaymentsRepository
    {
        Task CreateMessageLogAsync(MessageLog record);
        Task<List<MessageLog>> GetSentMessagesSinceAsync(string messageType, DateTime sinceUtc);
        Task<Dictionary<string, string>> GetAccountNames(IEnumerable<string> accountIds);
        Task<KosePaymentsViewModel> GetAllPaymentsAsync(int page = 1, int pageSize = 10);
        Task<KosePaymentsViewModel> GetOrphanedPaymentsAsync(int page = 1, int pageSize = 10);

        Task<List<AccountSummary>> GetLiveQualifyingLockAccounts();
        Task<List<PaymentMessage>?> GetAllPaymentsForMessagesAsync();
    }
}