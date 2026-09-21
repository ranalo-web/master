using System.Data;
using System.Data.SqlClient;
using Dapper;
using Ranalo.Models;

namespace Ranalo.DataStore
{
    public class OperatingExpenseRepository : IOperatingExpenseRepository
    {
        private readonly IDbConnection _db;
        private readonly ILogger<OperatingExpenseRepository> _logger;

        public OperatingExpenseRepository(IDbConnection db, ILogger<OperatingExpenseRepository> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<(List<OperatingExpense> Expenses, int TotalRecords, decimal MonthTotal)> GetPagedAsync(
            DateTime monthStart, DateTime monthEndExclusive, int page, int pageSize)
        {
            const string countSql = @"
                SELECT COUNT(*), ISNULL(SUM(Amount), 0)
                FROM OperatingExpenses
                WHERE RemovedAtUtc IS NULL
                  AND ExpenseDate >= @MonthStart AND ExpenseDate < @MonthEnd";

            const string sql = @"
                SELECT
                    oe.Id, oe.ExpenseDate, oe.Category, oe.Description, oe.Amount,
                    oe.AddedByUserId, u.[Name] + ' ' + u.[LastName] AS AddedByName, oe.AddedAtUtc
                FROM OperatingExpenses oe
                INNER JOIN Users u ON u.UserId = oe.AddedByUserId
                WHERE oe.RemovedAtUtc IS NULL
                  AND oe.ExpenseDate >= @MonthStart AND oe.ExpenseDate < @MonthEnd
                ORDER BY oe.ExpenseDate DESC, oe.Id DESC
                OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

            try
            {
                var parameters = new { MonthStart = monthStart, MonthEnd = monthEndExclusive };
                var (total, monthTotal) = await _db.QuerySingleAsync<(int, decimal)>(countSql, parameters);
                var rows = await _db.QueryAsync<OperatingExpense>(sql, new
                {
                    MonthStart = monthStart,
                    MonthEnd = monthEndExclusive,
                    Offset = (page - 1) * pageSize,
                    PageSize = pageSize,
                });

                return (rows.ToList(), total, monthTotal);
            }
            catch (SqlException ex) when (IsMissingTable(ex))
            {
                _logger.LogWarning(ex, "OperatingExpenses table not found; see Database/OperatingExpenses/001_create_operating_expenses.sql.");
                return (new List<OperatingExpense>(), 0, 0);
            }
        }

        public async Task AddAsync(DateTime expenseDate, string category, string? description, decimal amount, int addedByUserId)
        {
            const string sql = @"
                INSERT INTO OperatingExpenses (ExpenseDate, Category, Description, Amount, AddedByUserId)
                VALUES (@ExpenseDate, @Category, @Description, @Amount, @AddedByUserId)";

            await _db.ExecuteAsync(sql, new { ExpenseDate = expenseDate, Category = category, Description = description, Amount = amount, AddedByUserId = addedByUserId });
        }

        public async Task<bool> RemoveAsync(int id, int removedByUserId)
        {
            const string sql = @"
                UPDATE OperatingExpenses
                SET RemovedByUserId = @RemovedByUserId, RemovedAtUtc = SYSUTCDATETIME()
                WHERE Id = @Id AND RemovedAtUtc IS NULL";

            var rowsAffected = await _db.ExecuteAsync(sql, new { Id = id, RemovedByUserId = removedByUserId });
            return rowsAffected > 0;
        }

        public async Task<List<OperatingExpenseMonthlyTotal>> GetMonthlyTotalsAsync(int months)
        {
            var rangeStart = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).AddMonths(-(months - 1));

            const string sql = @"
                SELECT YEAR(ExpenseDate) AS Year, MONTH(ExpenseDate) AS Month, SUM(Amount) AS Total
                FROM OperatingExpenses
                WHERE RemovedAtUtc IS NULL AND ExpenseDate >= @RangeStart
                GROUP BY YEAR(ExpenseDate), MONTH(ExpenseDate)";

            List<OperatingExpenseMonthlyTotal> rows;
            try
            {
                rows = (await _db.QueryAsync<OperatingExpenseMonthlyTotal>(sql, new { RangeStart = rangeStart })).ToList();
            }
            catch (SqlException ex) when (IsMissingTable(ex))
            {
                _logger.LogWarning(ex, "OperatingExpenses table not found; see Database/OperatingExpenses/001_create_operating_expenses.sql.");
                rows = new List<OperatingExpenseMonthlyTotal>();
            }

            // Fill every month in the window, including ones with no rows at
            // all (Total = 0), so the caller never has to reconcile a sparse
            // list against the calendar itself.
            var byKey = rows.ToDictionary(r => (r.Year, r.Month), r => r.Total);
            var result = new List<OperatingExpenseMonthlyTotal>();
            for (var i = 0; i < months; i++)
            {
                var month = rangeStart.AddMonths(i);
                result.Add(new OperatingExpenseMonthlyTotal
                {
                    Year = month.Year,
                    Month = month.Month,
                    Total = byKey.TryGetValue((month.Year, month.Month), out var total) ? total : 0,
                });
            }

            return result;
        }

        private static bool IsMissingTable(SqlException ex) => ex.Number is 208 or 207;
    }
}
