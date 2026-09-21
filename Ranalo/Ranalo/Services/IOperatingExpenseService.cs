using Ranalo.Models;

namespace Ranalo.Services
{
    public interface IOperatingExpenseService
    {
        Task<(List<OperatingExpense> Expenses, int TotalRecords, decimal MonthTotal)> GetForMonthAsync(
            int year, int month, int page, int pageSize);

        Task AddAsync(DateTime expenseDate, string category, string? description, decimal amount, int addedByUserId);

        Task<bool> RemoveAsync(int id, int removedByUserId);

        Task<List<OperatingExpenseMonthlyTotal>> GetMonthlyTotalsAsync(int months);
    }
}
