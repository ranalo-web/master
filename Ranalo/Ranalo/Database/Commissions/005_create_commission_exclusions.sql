-- Contracts that earn no commission for anyone. Legacy = sold before the
-- current commission rules (before 1 Oct 2026, except Mali Mali, whose
-- accounts all qualify); their commission was paid under the old
-- calculators, so nothing more is due. Marked once by
-- Database/AdHoc/mark_legacy_commission_accounts_2026-10-24.sql; after that
-- only an admin changes them, from the Account Commissions page.
--
-- RecoverArrears = 1: still no commission, but the customer's shortfall is
-- deducted from the dealer's commission on their other accounts (how a
-- dealer's legacy arrears are recovered, one account at a time).
--
-- Every change is kept in CommissionExclusionLog.
--
-- Safe to re-run: guarded with existence checks.

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CommissionExclusions')
BEGIN
    CREATE TABLE CommissionExclusions
    (
        ContractId     INT NOT NULL PRIMARY KEY,   -- Contract_Info.ContractID
        Reason         VARCHAR(20) NOT NULL,       -- Legacy | Fraud | Other
        RecoverArrears BIT NOT NULL DEFAULT 0,
        Notes          NVARCHAR(500) NULL,
        SetByUserId    INT NULL,                   -- Users.UserId; NULL = set by script
        SetAtUtc       DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CommissionExclusionLog')
BEGIN
    CREATE TABLE CommissionExclusionLog
    (
        Id              INT IDENTITY(1,1) PRIMARY KEY,
        ContractId      INT NOT NULL,
        OldStatus       VARCHAR(20) NOT NULL,      -- Qualifies | NoCommission | Recover
        NewStatus       VARCHAR(20) NOT NULL,
        Reason          VARCHAR(20) NULL,
        Notes           NVARCHAR(500) NULL,
        ChangedByUserId INT NULL,                  -- NULL = script
        ChangedAtUtc    DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );

    CREATE INDEX IX_CommissionExclusionLog_ContractId ON CommissionExclusionLog (ContractId);
END
GO
