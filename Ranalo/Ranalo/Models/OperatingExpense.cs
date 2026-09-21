namespace Ranalo.Models
{
    // One row in the OperatingExpenses ledger -- see
    // Database/OperatingExpenses/001_create_operating_expenses.sql. A live,
    // user-editable table (Admin logs entries directly), not rollup-backed.
    public class OperatingExpense
    {
        public int Id { get; set; }
        public DateTime ExpenseDate { get; set; }
        public string Category { get; set; } = "";
        public string? Description { get; set; }
        public decimal Amount { get; set; }
        public int AddedByUserId { get; set; }
        public string AddedByName { get; set; } = "";
        public DateTime AddedAtUtc { get; set; }
    }

    // One row per calendar month -- Financials page's monthly comparison
    // chart and the "no expenses logged this month" flag both read this
    // directly (HasExpenses is just Total > 0), rather than each deriving
    // its own check.
    public class OperatingExpenseMonthlyTotal
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public decimal Total { get; set; }
        public bool HasExpenses => Total > 0;
    }

    // The six expense categories offered when logging an entry -- a fixed
    // list (not a free-text field) so category names don't drift.
    public static class OperatingExpenseCategory
    {
        public const string Salaries = "Salaries";
        public const string Marketing = "Marketing";
        public const string Rent = "Rent";
        public const string Utilities = "Utilities";
        public const string Software = "Software";
        public const string Other = "Other";

        public static readonly IReadOnlyList<string> All = new[]
        {
            Salaries, Marketing, Rent, Utilities, Software, Other,
        };
    }
}
