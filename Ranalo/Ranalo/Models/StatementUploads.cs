namespace Ranalo.Models
{
    // One M-Pesa statement imported on All Payments (Database/StatementUploads/001).
    public class StatementUpload
    {
        public int Id { get; set; }
        public string FileName { get; set; } = "";
        public DateTime UploadedAtUtc { get; set; }
        public int? UploadedByUserId { get; set; }
        public string? UploadedByName { get; set; }
        public DateTime? PeriodFrom { get; set; }
        public DateTime? PeriodTo { get; set; }
        public int RowsRead { get; set; }
        public int NewPayments { get; set; }
        public int AlreadyRecorded { get; set; }
        public int Skipped { get; set; }
        public string? Error { get; set; }
    }

    // Payments added by imports before uploads were recorded, by day.
    public class EarlierImport
    {
        public DateTime ImportedOn { get; set; }
        public int Added { get; set; }
        public DateTime FirstPayment { get; set; }
        public DateTime LastPayment { get; set; }
    }

    public record DateSpan(DateTime From, DateTime To)
    {
        public int Days => (To - From).Days + 1;
    }

    // A month on the coverage strip: how many of its days are covered.
    public record CoverageMonth(DateTime Month, int DaysCovered, int DaysInMonth)
    {
        public bool Full => DaysCovered >= DaysInMonth;
        public bool None => DaysCovered == 0;
    }

    public class StatementUploadsViewModel
    {
        public List<StatementUpload> Uploads { get; set; } = new();
        public List<DateSpan> Covered { get; set; } = new();
        public List<DateSpan> Gaps { get; set; } = new();
        public List<CoverageMonth> Months { get; set; } = new();
        public DateTime? CoveredTo { get; set; }
        public int DaysSinceCovered { get; set; }
        public int MissedFound { get; set; }
        public List<EarlierImport> Earlier { get; set; } = new();
    }
}
