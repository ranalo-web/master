-- Statement Uploads report: every M-Pesa statement imported on All Payments
-- (Import Payments), with the period its payments cover, so we can see which
-- periods have been checked for missed payments and where the gaps are.
-- Rules live in Services/StatementUploadRules.cs.
--
-- Run before deploying: the import writes a row here after every upload
-- (it carries on without one if this table is missing, but the report needs it).
-- Safe to re-run.

IF OBJECT_ID('dbo.PaymentStatementUploads') IS NULL
BEGIN
    CREATE TABLE dbo.PaymentStatementUploads
    (
        Id               INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_PaymentStatementUploads PRIMARY KEY,
        FileName         NVARCHAR(260)  NOT NULL,
        UploadedAtUtc    DATETIME2      NOT NULL,
        UploadedByUserId INT            NULL,
        UploadedByName   NVARCHAR(100)  NULL,
        PeriodFrom       DATETIME2      NULL,   -- earliest payment in the file (M-Pesa time)
        PeriodTo         DATETIME2      NULL,   -- latest payment in the file
        RowsRead         INT            NOT NULL DEFAULT 0,
        NewPayments      INT            NOT NULL DEFAULT 0,  -- the missed ones it found
        AlreadyRecorded  INT            NOT NULL DEFAULT 0,
        Skipped          INT            NOT NULL DEFAULT 0,  -- account number not a number
        Error            NVARCHAR(1000) NULL                 -- the import failed
    );
    CREATE INDEX IX_PaymentStatementUploads_UploadedAtUtc ON dbo.PaymentStatementUploads (UploadedAtUtc DESC);
END
GO
