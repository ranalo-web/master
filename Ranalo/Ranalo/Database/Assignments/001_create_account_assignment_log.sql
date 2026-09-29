-- Account Assignment audit trail: every agent or collector assigned to (or
-- removed from) an account on the admin Account Assignment page. The
-- assignment itself lives on the open contract (Contract_Info.AssignedAgentId
-- / DebtCollectorUserId); this records who changed it and when.
--
-- Safe to re-run: guarded with an existence check.

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AccountAssignmentLog')
BEGIN
    CREATE TABLE AccountAssignmentLog
    (
        Id              INT IDENTITY(1,1) PRIMARY KEY,
        AccountNo       BIGINT NOT NULL,        -- Contract_Info.ID
        ContractId      BIGINT NULL,            -- Contract_Info.ContractID
        AssignmentRole  VARCHAR(10) NOT NULL,   -- Agent | Collector
        OldUserId       INT NULL,               -- NULL = nobody before
        NewUserId       INT NULL,               -- NULL = removed
        ChangedByUserId INT NOT NULL,
        ChangedAtUtc    DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );

    CREATE INDEX IX_AccountAssignmentLog_AccountNo ON AccountAssignmentLog (AccountNo);
END
GO
