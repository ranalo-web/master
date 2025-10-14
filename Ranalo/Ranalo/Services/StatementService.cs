using Ranalo.DataStore;
using Ranalo.Models;

namespace Ranalo.Services
{
    public class StatementService : IStatementService
    {
        private readonly IStatementsRepository _statementRepo;

        public StatementService(IStatementsRepository statementRepo)
        {
            _statementRepo = statementRepo;
        }

        public async Task<int> CreateNewStatementAsync(BankAccountStatement statement)
        {
            //Check if file statement already exists
            var existingFile = await _statementRepo.GetStatementByFileNameAsync(statement.FileName);
            if (existingFile != null) 
            { 
               return existingFile.StatementId;
            }

            var statementId = await _statementRepo.InsertStatementAsync(statement);
            if(statementId > 0)
            {
                statement.Transactions.ForEach(t => t.StatementId = statementId);
                await _statementRepo.InsertTransactionsAsync(statement.Transactions);
            }

            return statementId;
        }

        public async Task CreateNewTransactionsForStatementAsync(IEnumerable<BankTransaction> transactions)
        {
            await _statementRepo.InsertTransactionsAsync(transactions);
        }

        public async Task<BankAccountStatement> GetStatementForDealerWithTransactionsAsync(int statementId, long dealerId)
        {
            var bankStatement = await _statementRepo.GetStatementWithTransactionsAsync(statementId);

            bankStatement.Dealers = await _statementRepo.GetAllAvailableDealers();

            return bankStatement;
        }

        public async Task<List<Models.Dealer>?> GetStatementsDealersAsync()
        {
            return await _statementRepo.GetAllAvailableDealers();

        }

        public async Task<IEnumerable<BankTransaction>> GetTransactionsByDealerAsync(string dealerReference)
        {
            return await _statementRepo.GetTransactionsByDealer(dealerReference);
        }
    }
}
