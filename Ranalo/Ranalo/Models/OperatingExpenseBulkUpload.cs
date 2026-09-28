namespace Ranalo.Models
{
    // One row of a bulk Operating Expenses upload, as shown on the review
    // screen -- the same four fields as the single "Log an expense" form.
    // Category may be blank after parsing: it's assigned on the review
    // screen before anything is saved. Also the POST model for saving.
    public class OperatingExpenseUploadRow
    {
        public int RowNumber { get; set; }
        public bool Include { get; set; } = true;
        public DateTime? ExpenseDate { get; set; }
        public string? Category { get; set; }
        public string? Description { get; set; }
        public decimal? Amount { get; set; }
        public List<string> Errors { get; set; } = new();
    }

    public class OperatingExpenseBulkReviewViewModel
    {
        public string FileName { get; set; } = "";
        public List<OperatingExpenseUploadRow> Rows { get; set; } = new();
    }
}
