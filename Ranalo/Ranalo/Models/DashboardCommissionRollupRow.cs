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

        // Distinct agent-assigned accounts contributing to this dealer's
        // commission pool.
        public int CommissionAccountCount { get; set; }

        // Sum, across all this dealer's agents, of each agent's true-arrears
        // deduction (gross commission minus net, before the per-agent floor
        // at 0) -- i.e. how much of the gross commission earned is being
        // held back because of accounts in true arrears. Not the same as
        // CommissionOutstanding, which is net-of-arrears and floored.
        public decimal CommissionWithheldForArrears { get; set; }

        // Dealer Commissions card: every paid account contributing to
        // CommissionReceived, including direct dealer sales with no
        // AssignedAgentId -- a wider population than CommissionAccountCount
        // above, which is agent-only. Excludes accounts with no recorded
        // BuyingPrice -- see DealerCommissionMissingCostCount.
        public int DealerCommissionAccountCount { get; set; }

        // Accounts excluded from DealerCommissionEarned/DealerCommissionAccountCount
        // because Contract_Info.BuyingPrice was never recorded -- a data gap
        // to flag and fix, not a free device to silently assume.
        public int DealerCommissionMissingCostCount { get; set; }

        // Dealer Commissions incentive figure: how much more commission
        // would be unlocked if every true-arrears account (locked, has a
        // recorded cost) paid off its shortfall today -- see
        // ComputeCommissionSnapshotRollupAsync for the approximation this uses.
        public decimal DealerCommissionWithheldForArrears { get; set; }
    }
}
