-- Extends DashboardCompletedContract (created empty in 001, never populated)
-- to also carry "upsell target" rows -- accounts that have paid 80%+ of the
-- full contract value but aren't fully paid off yet (LoanBalance > 0).
-- Status distinguishes the two: 'Completed' (LoanBalance <= 0) vs
-- 'UpsellTarget' (PctComplete >= 80 AND not yet Completed). CompletedDate is
-- only meaningful for 'Completed' rows (approximated as LastPaidDate -- there
-- is no stored "date it was paid off"), so it becomes nullable.
--
-- Safe to re-run: every change is guarded with an existence/nullability check.

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('DashboardCompletedContract') AND name = 'Status')
BEGIN
    ALTER TABLE DashboardCompletedContract ADD Status VARCHAR(20) NOT NULL DEFAULT 'Completed';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('DashboardCompletedContract') AND name = 'PctComplete')
BEGIN
    ALTER TABLE DashboardCompletedContract ADD PctComplete DECIMAL(9,2) NULL;
END
GO

IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('DashboardCompletedContract') AND name = 'CompletedDate' AND is_nullable = 0
)
BEGIN
    ALTER TABLE DashboardCompletedContract ALTER COLUMN CompletedDate DATE NULL;
END
GO
