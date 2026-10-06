-- Device enrolment checks/approval and the fully-paid removal queue.
-- Rules live in Services/DeviceLock/DeviceLockRules.cs.
--
-- RUN THIS BEFORE DEPLOYING THE APP: Enrolments is mapped by Entity
-- Framework, so the app fails to read enrolments until these columns exist.
--
-- Safe to re-run: every change is guarded with an existence check.

-- ---------------------------------------------------------------------------
-- 1. Enrolments: columns from the first Transsion change (never scripted),
--    plus the new check/approval columns.
-- ---------------------------------------------------------------------------
IF COL_LENGTH('dbo.Enrolments', 'DeviceBrand') IS NULL
    ALTER TABLE dbo.Enrolments ADD DeviceBrand NVARCHAR(50) NULL;
IF COL_LENGTH('dbo.Enrolments', 'PayTriggerStatus') IS NULL
    ALTER TABLE dbo.Enrolments ADD PayTriggerStatus NVARCHAR(500) NULL;
IF COL_LENGTH('dbo.Enrolments', 'PayTriggerResponse') IS NULL
    ALTER TABLE dbo.Enrolments ADD PayTriggerResponse NVARCHAR(MAX) NULL;

IF COL_LENGTH('dbo.Enrolments', 'EnrolledByUserId') IS NULL
    ALTER TABLE dbo.Enrolments ADD EnrolledByUserId INT NULL;
IF COL_LENGTH('dbo.Enrolments', 'EnrolledByRole') IS NULL
    ALTER TABLE dbo.Enrolments ADD EnrolledByRole INT NULL;               -- UserRole at enrol time
IF COL_LENGTH('dbo.Enrolments', 'ProductName') IS NULL
    ALTER TABLE dbo.Enrolments ADD ProductName NVARCHAR(255) NULL;         -- WooCommerce product the brand came from
IF COL_LENGTH('dbo.Enrolments', 'DealerRef') IS NULL
    ALTER TABLE dbo.Enrolments ADD DealerRef NVARCHAR(100) NULL;           -- Woo order's dealer referral code
IF COL_LENGTH('dbo.Enrolments', 'TacBrand') IS NULL
    ALTER TABLE dbo.Enrolments ADD TacBrand NVARCHAR(50) NULL;             -- brand the IMEI's TAC is known for, if any
IF COL_LENGTH('dbo.Enrolments', 'BrandOverriddenByUserId') IS NULL
    ALTER TABLE dbo.Enrolments ADD BrandOverriddenByUserId INT NULL;       -- admin who confirmed the brand
IF COL_LENGTH('dbo.Enrolments', 'RequiredDeposit') IS NULL
    ALTER TABLE dbo.Enrolments ADD RequiredDeposit DECIMAL(18,2) NULL;
IF COL_LENGTH('dbo.Enrolments', 'DepositPaid') IS NULL
    ALTER TABLE dbo.Enrolments ADD DepositPaid DECIMAL(18,2) NULL;
IF COL_LENGTH('dbo.Enrolments', 'DepositShort') IS NULL
    ALTER TABLE dbo.Enrolments ADD DepositShort BIT NOT NULL CONSTRAINT DF_Enrolments_DepositShort DEFAULT 0;
IF COL_LENGTH('dbo.Enrolments', 'ActivatedAt') IS NULL
    ALTER TABLE dbo.Enrolments ADD ActivatedAt DATETIME2 NULL;             -- PayTrigger webhook: phone switched on
IF COL_LENGTH('dbo.Enrolments', 'CallApprovedByUserId') IS NULL
    ALTER TABLE dbo.Enrolments ADD CallApprovedByUserId INT NULL;
IF COL_LENGTH('dbo.Enrolments', 'CallApprovedAt') IS NULL
    ALTER TABLE dbo.Enrolments ADD CallApprovedAt DATETIME2 NULL;
IF COL_LENGTH('dbo.Enrolments', 'UnlockedAt') IS NULL
    ALTER TABLE dbo.Enrolments ADD UnlockedAt DATETIME2 NULL;
GO

-- ---------------------------------------------------------------------------
-- 2. Brand keywords matched against WooCommerce product names. Admin-edited
--    (SQL for now). Empty table = the app's built-in defaults.
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'DeviceBrandKeywords')
BEGIN
    CREATE TABLE dbo.DeviceBrandKeywords
    (
        Id       INT IDENTITY(1,1) PRIMARY KEY,
        Keyword  NVARCHAR(100) NOT NULL,
        Brand    NVARCHAR(50)  NOT NULL     -- Samsung | itel | TECNO | Infinix
    );

    CREATE UNIQUE INDEX UQ_DeviceBrandKeywords_Keyword ON dbo.DeviceBrandKeywords (Keyword);

    INSERT INTO dbo.DeviceBrandKeywords (Keyword, Brand) VALUES
        ('Samsung', 'Samsung'),
        ('Galaxy',  'Samsung'),
        ('itel',    'itel'),
        ('TECNO',   'TECNO'),
        ('Techno',  'TECNO'),   -- common misspelling on the store
        ('Camon',   'TECNO'),
        ('Spark',   'TECNO'),
        ('Infinix', 'Infinix');
END
GO

-- ---------------------------------------------------------------------------
-- 3. TAC (first 8 IMEI digits) -> brand, learned from devices a provider has
--    confirmed (Transsion activation webhook, Knox device lookup) or an admin
--    has confirmed. A known TAC that disagrees with the order blocks enrolment.
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'DeviceTacCodes')
BEGIN
    CREATE TABLE dbo.DeviceTacCodes
    (
        Tac          CHAR(8)       NOT NULL PRIMARY KEY,
        Brand        NVARCHAR(50)  NOT NULL,
        Source       NVARCHAR(50)  NOT NULL,   -- 'Transsion' | 'Knox' | 'Admin'
        SourceImei   NVARCHAR(20)  NULL,
        CreatedAtUtc DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME()
    );
END
GO

-- ---------------------------------------------------------------------------
-- 4. Removal queue: fully-paid devices waiting for an admin to approve
--    removing them from Nuovo / Knox / Transsion.
--    Status: Pending -> Processing -> Completed | Failed (retryable)
--            Pending | Failed -> Rejected
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'DeviceRemovalTasks')
BEGIN
    CREATE TABLE dbo.DeviceRemovalTasks
    (
        Id               INT IDENTITY(1,1) PRIMARY KEY,
        AccountId        BIGINT        NOT NULL,     -- Devices.Id / Contract_Info.ID
        Imei             NVARCHAR(20)  NULL,
        CustomerName     NVARCHAR(200) NULL,
        LockGroup        INT           NULL,         -- 1 Nuovo, 2 Knox, 3 Transsion
        Provider         VARCHAR(20)   NOT NULL,     -- 'Nuovo' | 'Knox' | 'Transsion'
        Reason           NVARCHAR(200) NOT NULL,
        Status           VARCHAR(20)   NOT NULL,
        CreatedAtUtc     DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),
        DecidedByUserId  INT           NULL,
        DecidedAtUtc     DATETIME2     NULL,
        DecisionNote     NVARCHAR(500) NULL,
        AttemptCount     INT           NOT NULL DEFAULT 0,
        LastAttemptAtUtc DATETIME2     NULL,
        LastResponse     NVARCHAR(MAX) NULL,
        CompletedAtUtc   DATETIME2     NULL
    );

    -- One live task per account: a device can't be queued twice, and once
    -- removed it isn't queued again. A rejected task doesn't count, so the
    -- account can be queued again later.
    CREATE UNIQUE INDEX UQ_DeviceRemovalTasks_LiveAccount
        ON dbo.DeviceRemovalTasks (AccountId)
        WHERE Status IN ('Pending', 'Processing', 'Failed', 'Completed');

    CREATE INDEX IX_DeviceRemovalTasks_Status ON dbo.DeviceRemovalTasks (Status, CreatedAtUtc);
END
GO
