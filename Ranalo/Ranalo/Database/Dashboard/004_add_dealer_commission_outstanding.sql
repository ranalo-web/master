-- Adds DealerCommissionOutstanding to DashboardSnapshot: what Ranalo still
-- owes the dealer (CommissionReceived minus DealerCommissionPayments
-- actually paid, floored at 0). Distinct from the existing
-- CommissionOutstanding column, which is agent-facing (what the dealer owes
-- their own agents) -- see ComputeCommissionSnapshotRollupAsync.
--
-- Safe to re-run: guarded with an existence check.

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('DashboardSnapshot') AND name = 'DealerCommissionOutstanding')
BEGIN
    ALTER TABLE DashboardSnapshot ADD DealerCommissionOutstanding DECIMAL(18,2) NULL;
END
GO
