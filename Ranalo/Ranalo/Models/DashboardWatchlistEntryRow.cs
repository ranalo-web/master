namespace Ranalo.Models
{
    // Maps 1:1 onto a row in the DashboardWatchlistEntry rollup table.
    public class DashboardWatchlistEntryRow
    {
        public int Rank { get; set; }
        public long? AccountId { get; set; }
        public string CustomerName { get; set; } = "";
        public string? AgentName { get; set; }
        public string? DealerName { get; set; }
        public string? Phone { get; set; }
        public string Detail { get; set; } = "";
    }

    // WatchlistType column values (Database/Dashboard/001_create_dashboard_tables.sql).
    public static class DashboardWatchlistType
    {
        public const string NonPayer = "NonPayer";
        public const string SlowPayer = "SlowPayer";
        public const string GoodPayer = "GoodPayer";
    }
}
