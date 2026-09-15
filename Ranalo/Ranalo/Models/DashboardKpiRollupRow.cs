namespace Ranalo.Models
{
    // One row per dealer, plus one row with DealerId == null for the
    // company-wide (Admin) total -- produced by
    // IDashboardReportRepository.ComputeKpiRollupAsync() in a single
    // GROUPING SETS pass over Contract_Info/KosePayments/Devices/Dealers.
    public class DashboardKpiRollupRow
    {
        public int? DealerId { get; set; }
        public decimal RevenueThisMonth { get; set; }
        public decimal RevenueLastMonth { get; set; }
        public int TotalAccounts { get; set; }
        public int NewThisMonth { get; set; }
    }
}
