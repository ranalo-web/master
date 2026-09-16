-- Adds DealerCommissionMissingCostCount and DealerCommissionWithheldForArrears
-- to DashboardSnapshot: the Dealer Commissions card's "N accounts missing
-- cost data" flag and its arrears-incentive figure -- see
-- ComputeCommissionSnapshotRollupAsync.
--
-- Safe to re-run: guarded with an existence check.

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('DashboardSnapshot') AND name = 'DealerCommissionMissingCostCount')
BEGIN
    ALTER TABLE DashboardSnapshot ADD DealerCommissionMissingCostCount INT NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('DashboardSnapshot') AND name = 'DealerCommissionWithheldForArrears')
BEGIN
    ALTER TABLE DashboardSnapshot ADD DealerCommissionWithheldForArrears DECIMAL(18,2) NULL;
END
GO
