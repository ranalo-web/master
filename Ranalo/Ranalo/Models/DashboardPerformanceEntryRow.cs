namespace Ranalo.Models
{
    // Maps 1:1 onto a row in the DashboardPerformanceEntry rollup table.
    public class DashboardPerformanceEntryRow
    {
        public int Rank { get; set; }
        public int SubjectId { get; set; }
        public string SubjectName { get; set; } = "";
        public string? ParentName { get; set; }
        public int Accounts { get; set; }
        public decimal ActivePct { get; set; }
        public decimal? Revenue { get; set; }
        public decimal? CommissionPaid { get; set; }
        public decimal? CommissionDue { get; set; }
        public decimal PctOfTarget { get; set; }
    }

    // EntryType column values (Database/Dashboard/001_create_dashboard_tables.sql).
    public static class DashboardPerformanceEntryType
    {
        public const string Dealer = "Dealer";
        public const string Agent = "Agent";

        // Deliberately distinct from "Agent" -- the CommissionsPaid list is
        // computed independently of (and before) the still-deferred Agent
        // Performance ranking feature. Sharing "Agent" would make a
        // commissions-only refresh silently populate ActivePct=0/PctOfTarget=0
        // rows that the ranking feature would then display as real data.
        public const string AgentCommission = "AgentCommission";
    }
}
