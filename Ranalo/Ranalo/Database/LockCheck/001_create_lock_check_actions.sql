-- Lock check (admin only): Nuovo phones that need attention -- unlocked
-- while the customer is behind, lost from Nuovo, or fully paid with the
-- contract still open. Rules: Services/LockCheckRules.cs.
--
-- Every action taken from the page (send to Recovery, send a Nuovo lock,
-- close and release) is logged here, so the page can show what was last
-- done to each phone (e.g. "Lock sent") and when.
--
-- Safe to re-run: guarded with existence checks.

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'LockCheckActions')
BEGIN
    CREATE TABLE LockCheckActions
    (
        Id          INT IDENTITY(1,1) PRIMARY KEY,
        AccountNo   BIGINT         NOT NULL,
        Action      VARCHAR(20)    NOT NULL,   -- ToRecovery | NuovoLock | CloseRelease
        Success     BIT            NOT NULL,
        Result      NVARCHAR(1000) NULL,
        ByUserId    INT            NULL,
        AtUtc       DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME()
    );

    CREATE INDEX IX_LockCheckActions_Account ON LockCheckActions (AccountNo, AtUtc);
END
GO
