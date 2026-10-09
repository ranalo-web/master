-- Records which user assigned an orphaned payment to an account, shown in the
-- "Assigned By" column on the Assigned Payments page.
--
-- RUN THIS BEFORE DEPLOYING THE APP: assigning a payment writes this column.
-- Assignments made before this change keep a NULL and show blank.
--
-- Safe to re-run: the change is guarded with an existence check.

IF COL_LENGTH('dbo.OrphanedPayments', 'AssignedByUserId') IS NULL
    ALTER TABLE dbo.OrphanedPayments ADD AssignedByUserId INT NULL;
