namespace Ranalo.Models
{
    // Maps 1:1 onto a row in the DashboardMonthlyTrend rollup table.
    public class DashboardMonthlyTrendPoint
    {
        public string YearMonth { get; set; } = "";
        public decimal Revenue { get; set; }
        public int AccountsCount { get; set; }
    }
}
