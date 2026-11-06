-- Who unlocked a customer's app access, when and why (Customer App staff
-- page, Admin and Approver). The last unlock is kept on the row; every
-- unlock is also in the application log.
--
-- Run after 001. Needs an admin login. Safe to re-run.

IF COL_LENGTH('dbo.CustomerAccess', 'UnlockedAtUtc') IS NULL
    ALTER TABLE dbo.CustomerAccess ADD UnlockedAtUtc DATETIME2 NULL;
IF COL_LENGTH('dbo.CustomerAccess', 'UnlockedByUserId') IS NULL
    ALTER TABLE dbo.CustomerAccess ADD UnlockedByUserId INT NULL;
IF COL_LENGTH('dbo.CustomerAccess', 'UnlockNote') IS NULL
    ALTER TABLE dbo.CustomerAccess ADD UnlockNote NVARCHAR(400) NULL;
GO
