using Ranalo.DataStore;
using Ranalo.Models;

namespace Ranalo.Services
{
    public class OperatingExpenseService : IOperatingExpenseService
    {
        private readonly IOperatingExpenseRepository _repository;

        public OperatingExpenseService(IOperatingExpenseRepository repository)
        {
            _repository = repository;
        }

        public Task<(List<OperatingExpense> Expenses, int TotalRecords, decimal MonthTotal)> GetForMonthAsync(
            int year, int month, int page, int pageSize)
        {
            var monthStart = new DateTime(year, month, 1);
            return _repository.GetPagedAsync(monthStart, monthStart.AddMonths(1), page, pageSize);
        }

        public Task AddAsync(DateTime expenseDate, string category, string? description, decimal amount, int addedByUserId) =>
            _repository.AddAsync(expenseDate, category, description, amount, addedByUserId);

        public Task<bool> RemoveAsync(int id, int removedByUserId) =>
            _repository.RemoveAsync(id, removedByUserId);

        public Task<List<OperatingExpenseMonthlyTotal>> GetMonthlyTotalsAsync(int months) =>
            _repository.GetMonthlyTotalsAsync(months);
    }
}
