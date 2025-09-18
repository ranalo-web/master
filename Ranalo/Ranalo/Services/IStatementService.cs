using Ranalo.Models;

namespace Ranalo.Services
{
    public interface IStatementService
    {
        Task<int> CreateNewStatementAsync(BankAccountStatement statement);
        Task CreateNewTransactionsForStatementAsync(IEnumerable<BankTransaction> transactions);
        Task<BankAccountStatement> GetStatementForDealerWithTransactionsAsync(int statementId, long dealerId);
    }
}