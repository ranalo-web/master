using Dapper;
using DocumentFormat.OpenXml.Drawing;
using DocumentFormat.OpenXml.Wordprocessing;
using Ranalo.Models;
using System.Data;
using System.Drawing.Printing;

namespace Ranalo.DataStore
{
    public class StatementsRepository : IStatementsRepository
    {
        private readonly IDbConnection _db;

        public StatementsRepository(IDbConnection db)
        {
            _db = db;
        }

        public async Task<int> InsertStatementAsync(BankAccountStatement statement, IDbTransaction transaction = null)
        {
            var sql = @"
        INSERT INTO BankAccountStatement
        (DealerId, AccountName, AccountNumber, AccountType, GenerationDateTime, PeriodStart, PeriodEnd,
         GeneratedBy, Currency, AvailableBalance, BalanceAtPeriodStart, BalanceAtPeriodEnd,
         TotalCredits, TotalDebits, FileName)
        VALUES (@DealerId, @AccountName, @AccountNumber, @AccountType, @GenerationDateTime, @PeriodStart, @PeriodEnd,
                @GeneratedBy, @Currency, @AvailableBalance, @BalanceAtPeriodStart, @BalanceAtPeriodEnd,
                @TotalCredits, @TotalDebits, @FileName);
        SELECT CAST(SCOPE_IDENTITY() as int);";

            return await _db.ExecuteScalarAsync<int>(sql, statement, transaction);
        }

        public async Task InsertTransactionsAsync(IEnumerable<BankTransaction> transactions, IDbTransaction transaction = null)
        {
            var sql = @"
        INSERT INTO BankTransaction
            (StatementId, PostingDate, ValueDate, BankReference, ChannelReference, TransactionType,
             TransactionDetails, DebitAmount, CreditAmount, RunningBalance)
            SELECT 
                @StatementId, @PostingDate, @ValueDate, @BankReference, @ChannelReference, @TransactionType,
                @TransactionDetails, @DebitAmount, @CreditAmount, @RunningBalance
            WHERE NOT EXISTS (
                SELECT 1
                FROM BankTransaction 
                WHERE BankReference = @BankReference
                  AND PostingDate = @PostingDate
                  AND TransactionType = @TransactionType
            );";

            await _db.ExecuteAsync(sql, transactions, transaction);
        }

        public async Task<BankAccountStatement> GetStatementWithTransactionsAsync(int statementId)
        {
            var statementSql = "SELECT TOP 1 * FROM BankAccountStatement WHERE DealerId = @Id";
            var transactionSql = "SELECT * FROM BankTransaction WHERE StatementId = @Id ORDER BY PostingDate";

            var statement = await _db.QueryFirstOrDefaultAsync<BankAccountStatement>(statementSql, new { Id = statementId });

            if (statement != null)
            {
                var transactions = await _db.QueryAsync<BankTransaction>(transactionSql, new { Id = statement.StatementId });

                statement.Transactions = transactions.ToList();
            }

            return statement;
        }

        public async Task<BankAccountStatement> GetStatementByFileNameAsync(string fileName)
        {
            var statementSql = "SELECT TOP 1 * FROM BankAccountStatement WHERE FileName = @FileName";

            var statement = await _db.QueryFirstOrDefaultAsync<BankAccountStatement>(statementSql, new { FileName = fileName });

            return statement;
        }

        public async Task<IEnumerable<BankTransaction>> GetTransactionsByDealer(string dealerReference)
        {
            var query = @";WITH SplitValues AS (
                                SELECT 
                                    t.TransactionId,
                                    s.value,
                                    ROW_NUMBER() OVER (PARTITION BY t.TransactionId ORDER BY (SELECT NULL)) AS rn
                                FROM [BankTransaction] t
                                CROSS APPLY STRING_SPLIT(t.TransactionDetails, ' ') s
                            )
                            SELECT t.TransactionId,
                            		t.BankReference,
                            		t.TransactionType,
                            		t.PostingDate,
                            		t.ChannelReference,
                            		t.DebitAmount,
                            		t.CreditAmount,
                            	    d.CompanyName,
                            		sv.value
                            FROM SplitValues sv
                            JOIN [BankTransaction] t ON t.TransactionId = sv.TransactionId
                            JOIN [DealerStatementAccount] o ON o.BankStatementRef = sv.value
                            JOIN [Dealers] d on d.DealerReference = o.DealerRefence
                            WHERE sv.rn = 4 -- 4th token = the numeric JoinId
                            AND d.DealerReference = @DealerRef
                            GROUP BY t.BankReference,
                            		t.TransactionType,
                            		t.PostingDate,
                            		t.ChannelReference,
                            		t.DebitAmount,
                            		t.CreditAmount,
                            	    d.CompanyName,
                            		sv.value,
                            		t.TransactionId"
            ;

            var records = await _db.QueryAsync<BankTransaction>(query, new { DealerRef = dealerReference});

            return records;
        }
    }
}
