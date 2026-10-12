-- Lost and stolen phones (Lost & Stolen page).
--
-- Anyone who can see the account (Admin, Approver, Dealer, Agent, or the
-- collector holding it) reports the phone lost or stolen and uploads the
-- police abstract or other evidence. An admin then confirms it -- which
-- locks the phone straight away -- or rejects it.
--
-- The customer still owes the balance: nothing here pauses the contract,
-- arrears or collections. While a report is Confirmed the nightly lock jobs
-- leave the phone locked even when the customer pays (otherwise a payment
-- would unlock it for whoever has it). The admin closes the report as
-- Found, Paid off or Written off, and the jobs take the phone back from
-- the next run.
--
-- LostStolenEvidence holds the uploaded files themselves (images/PDF, max
-- 0.25 MB each, checked by the app).
--
-- Safe to re-run: every step is guarded.

-- The filtered index below needs this (sqlcmd defaults it to OFF).
SET QUOTED_IDENTIFIER ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'LostStolenReports')
BEGIN
    CREATE TABLE LostStolenReports
    (
        Id               INT IDENTITY(1,1) PRIMARY KEY,
        AccountNo        BIGINT NOT NULL,              -- Contract_Info.ID / Devices.Id
        Kind             VARCHAR(10) NOT NULL,         -- 'Lost' | 'Stolen'
        IncidentDate     DATE NOT NULL,
        PoliceRef        NVARCHAR(100) NULL,           -- police abstract / OB number
        Description      NVARCHAR(1000) NOT NULL,
        ReportedByUserId INT NOT NULL,
        ReportedByRole   VARCHAR(20) NOT NULL,         -- role at the time of reporting
        ReportedAtUtc    DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        Status           VARCHAR(20) NOT NULL DEFAULT 'Pending',  -- Pending | Confirmed | Rejected | Closed
        DecidedByUserId  INT NULL,                     -- admin who confirmed / rejected
        DecidedAtUtc     DATETIME2 NULL,
        DecisionNote     NVARCHAR(500) NULL,
        LockSentAtUtc    DATETIME2 NULL,
        LockResult       NVARCHAR(500) NULL,
        Outcome          VARCHAR(20) NULL,             -- when Closed: 'Found' | 'PaidOff' | 'WrittenOff'
        ClosedByUserId   INT NULL,
        ClosedAtUtc      DATETIME2 NULL,
        ClosedNote       NVARCHAR(500) NULL
    );

    -- One open (Pending or Confirmed) report per account at a time.
    CREATE UNIQUE INDEX UQ_LostStolenReports_OpenAccount
        ON LostStolenReports (AccountNo)
        WHERE Status IN ('Pending', 'Confirmed');

    CREATE INDEX IX_LostStolenReports_Status ON LostStolenReports (Status, AccountNo);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'LostStolenEvidence')
BEGIN
    CREATE TABLE LostStolenEvidence
    (
        Id               INT IDENTITY(1,1) PRIMARY KEY,
        ReportId         INT NOT NULL REFERENCES LostStolenReports (Id),
        FileName         NVARCHAR(200) NOT NULL,
        ContentType      VARCHAR(50) NOT NULL,
        SizeBytes        INT NOT NULL,
        Content          VARBINARY(MAX) NOT NULL,
        UploadedByUserId INT NOT NULL,
        UploadedAtUtc    DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );

    CREATE INDEX IX_LostStolenEvidence_Report ON LostStolenEvidence (ReportId);
END
GO
