-- Recovery (admin only): phones we are trying to get back under a lock,
-- e.g. a Nuovo phone whose Nuovo lock was removed (fraud, collections).
-- Samsung phones are moved to Knox Guard: uploaded through Veritech,
-- approved in Knox, then locked. Once the lock works the admin moves the
-- phone to Knox -- Devices.LockGroup = 2 plus an Enrolments row (UpdatedBy
-- 'RECOVERY') so the daily lock jobs handle it from then on. A phone that
-- can't be caught stays with its old locker. Rules: Services/RecoveryRules.cs.
-- (Not to be confused with DeviceRecoveries, which records repossessions.)
--
-- Status: Investigating -> UploadSent -> Approved -> LockSent -> MovedToKnox
--         or NotCatchable / Closed at any point.
--
-- Safe to re-run: guarded with existence checks.

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'LockRecoveries')
BEGIN
    CREATE TABLE LockRecoveries
    (
        Id                INT IDENTITY(1,1) PRIMARY KEY,
        AccountNo         BIGINT        NOT NULL,   -- Devices.Id / Contract_Info.ID
        Imei              VARCHAR(20)   NOT NULL,
        Make              NVARCHAR(50)  NULL,
        Model             NVARCHAR(100) NULL,
        CustomerName      NVARCHAR(200) NULL,
        DealerName        NVARCHAR(200) NULL,
        OriginalLockGroup INT           NULL,       -- Devices.LockGroup when added (1 Nuovo, 2 Knox, 3 Transsion)
        Reason            VARCHAR(20)   NOT NULL,   -- 'Fraud' | 'Collections' | 'Other'
        Status            VARCHAR(20)   NOT NULL,
        VeritechTransId   NVARCHAR(100) NULL,       -- Knox approveId
        Notes             NVARCHAR(1000) NULL,
        CreatedByUserId   INT           NULL,
        CreatedAtUtc      DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc      DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),
        MovedToKnoxAtUtc  DATETIME2     NULL
    );

    -- One open recovery per account.
    CREATE UNIQUE INDEX UQ_LockRecoveries_OpenAccount
        ON LockRecoveries (AccountNo)
        WHERE Status IN ('Investigating', 'UploadSent', 'Approved', 'LockSent');
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'LockRecoveryAttempts')
BEGIN
    -- Every call made for a recovery, with the provider's reply.
    CREATE TABLE LockRecoveryAttempts
    (
        Id           INT IDENTITY(1,1) PRIMARY KEY,
        RecoveryId   INT           NOT NULL,
        Step         VARCHAR(30)   NOT NULL,   -- Upload, UploadStatus, Approve, KnoxCheck, Lock, Unlock, MoveToKnox, Status, Note
        Success      BIT           NOT NULL,
        Response     NVARCHAR(MAX) NULL,
        ByUserId     INT           NULL,
        AtUtc        DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME()
    );

    CREATE INDEX IX_LockRecoveryAttempts_Recovery ON LockRecoveryAttempts (RecoveryId, AtUtc);
END
GO
