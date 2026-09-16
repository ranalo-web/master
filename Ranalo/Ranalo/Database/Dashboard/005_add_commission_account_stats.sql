-- Adds CommissionAccountCount and CommissionWithheldForArrears to
-- DashboardSnapshot: the Agent Commissions card's "N accounts" and "KES X
-- withheld due to arrears" lines -- see
-- ComputeCommissionSnapshotRollupAsync's per-agent NetCommission
-- calculation (CommissionWithheldForArrears is the sum of each agent's
-- true-arrears deduction, i.e. gross commission minus net).
--
-- Safe to re-run: guarded with an existence check.

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('DashboardSnapshot') AND name = 'CommissionAccountCount')
BEGIN
    ALTER TABLE DashboardSnapshot ADD CommissionAccountCount INT NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('DashboardSnapshot') AND name = 'CommissionWithheldForArrears')
BEGIN
    ALTER TABLE DashboardSnapshot ADD CommissionWithheldForArrears DECIMAL(18,2) NULL;
END
GO
