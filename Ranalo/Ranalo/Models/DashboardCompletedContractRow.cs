namespace Ranalo.Models
{
    // Maps 1:1 onto a row in the DashboardCompletedContract rollup table.
    // Covers both fully-paid-off accounts (Status = Completed) and "upsell
    // target" accounts that have paid 80%+ but aren't done yet (Status =
    // UpsellTarget) -- see DashboardCompletedContractStatus.
    public class DashboardCompletedContractRow
    {
        public long? AccountId { get; set; }
        public string CustomerName { get; set; } = "";
        public string? DealerName { get; set; }
        public string ProductName { get; set; } = "";

        // Only set for Status == Completed. Approximated as the account's
        // last payment date -- there's no stored "date it was paid off".
        public DateTime? CompletedDate { get; set; }

        public decimal TotalPaid { get; set; }
        public int DurationMonths { get; set; }
        public string Status { get; set; } = DashboardCompletedContractStatus.Completed;
        public decimal? PctComplete { get; set; }
    }

    public static class DashboardCompletedContractStatus
    {
        public const string Completed = "Completed";
        public const string UpsellTarget = "UpsellTarget";
    }
}
