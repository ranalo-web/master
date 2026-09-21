namespace Ranalo.Models
{
    public class OperatingExpensesViewModel
    {
        public List<OperatingExpense> Expenses { get; set; } = new();
        public decimal MonthTotal { get; set; }
        public int Year { get; set; }
        public int Month { get; set; }
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; } = 1;
        public int TotalRecords { get; set; }
    }
}
