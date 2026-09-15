namespace Ranalo.Models
{
    // One row per dealer -- produced by
    // IDashboardReportRepository.ComputeCommissionSnapshotRollupAsync(). No
    // company-wide row: AdminDashboardViewModel has no commission-received
    // equivalent field.
    public class DashboardCommissionRollupRow
    {
        public int DealerId { get; set; }
        public decimal CommissionReceived { get; set; }
        public decimal CommissionPaidToAgents { get; set; }
        public decimal CommissionOutstanding { get; set; }
    }
}
