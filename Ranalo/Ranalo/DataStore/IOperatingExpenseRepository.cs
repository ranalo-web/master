using Ranalo.Models;

namespace Ranalo.DataStore
{
    public interface IOperatingExpenseRepository
    {
        Task<(List<OperatingExpense> Expenses, int TotalRecords, decimal MonthTotal)> GetPagedAsync(
            DateTime monthStart, DateTime monthEndExclusive, int page, int pageSize);

        Task AddAsync(DateTime expenseDate, string category, string? description, decimal amount, int addedByUserId);

        Task<bool> RemoveAsync(int id, int removedByUserId);

        // One row per calendar month over the trailing `months` months
        // (including the current one), oldest first -- months with no
        // logged expenses still get a row (Total = 0), so callers don't
        // have to fill gaps themselves.
        Task<List<OperatingExpenseMonthlyTotal>> GetMonthlyTotalsAsync(int months);
    }
}
