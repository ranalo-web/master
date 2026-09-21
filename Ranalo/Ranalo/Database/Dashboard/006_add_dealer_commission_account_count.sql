-- Adds DealerCommissionAccountCount to DashboardSnapshot: the Dealer
-- Commissions card's "N accounts" line -- every paid account contributing to
-- CommissionReceived, including direct dealer sales with no AssignedAgentId
-- (a wider population than the existing CommissionAccountCount, which is
-- agent-only -- see ComputeCommissionSnapshotRollupAsync).
--
-- Safe to re-run: guarded with an existence check.

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('DashboardSnapshot') AND name = 'DealerCommissionAccountCount')
BEGIN
    ALTER TABLE DashboardSnapshot ADD DealerCommissionAccountCount INT NULL;
END
GO
