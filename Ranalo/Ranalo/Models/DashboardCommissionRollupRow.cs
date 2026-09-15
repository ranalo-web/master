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

        // Floored per-agent before summing (see
        // ComputeCommissionSnapshotRollupAsync) -- an agent whose arrears
        // wiped out their earned commission contributes 0, not a negative
        // offset against other agents' outstanding balances.
        public decimal CommissionOutstanding { get; set; }

        // CommissionReceived minus DealerCommissionPayments actually paid to
        // this dealer, floored at 0. Distinct from CommissionOutstanding
        // above, which is agent-facing.
        public decimal DealerCommissionOutstanding { get; set; }
    }
}
