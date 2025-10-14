using Ranalo.Models;
using System.Data;

namespace Ranalo.DataStore
{
    public interface IStatementsRepository
    {
        Task<BankAccountStatement> GetStatementWithTransactionsAsync(int statementId);
        Task<int> InsertStatementAsync(BankAccountStatement statement, IDbTransaction transaction = null);
        Task InsertTransactionsAsync(IEnumerable<BankTransaction> transactions, IDbTransaction transaction = null);
        Task<BankAccountStatement> GetStatementByFileNameAsync(string fileName);

        Task<IEnumerable<BankTransaction>> GetTransactionsByDealer(string dealerReference);
        Task<List<Models.Dealer>?> GetAllAvailableDealers();
    }
}