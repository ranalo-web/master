-- Customer app (my.ranalocredit.com) login: account number + a one-time
-- code sent to the financed phone. Rules in Services/CustomerLoginRules.cs.
--
-- CustomerAccess: one row per account that has used the app -- the details
--   the customer keeps up to date, wrong codes in a row, and the lock
--   (set after too many; cleared by staff after the customer emails in).
--   SessionStamp changes on lock or "sign out everywhere", ending every
--   signed-in phone.
-- CustomerLoginCodes: each code sent, hashed (never stored as sent).
--
-- Kept apart from staff Users on purpose. Run before deploying the app
-- pages. Needs an admin login (the app's login can't create tables).
-- Safe to re-run.

IF OBJECT_ID('dbo.CustomerAccess') IS NULL
BEGIN
    CREATE TABLE dbo.CustomerAccess
    (
        AccountNo      BIGINT         NOT NULL CONSTRAINT PK_CustomerAccess PRIMARY KEY,
        Email          NVARCHAR(200)  NULL,
        Phone          NVARCHAR(20)   NULL,   -- M-Pesa number, 2547XXXXXXXX
        NationalId     NVARCHAR(30)   NULL,
        FailedCodes    INT            NOT NULL CONSTRAINT DF_CustomerAccess_Failed DEFAULT 0,
        LastFailedUtc  DATETIME2      NULL,
        LockedAtUtc    DATETIME2      NULL,
        LockedReason   NVARCHAR(200)  NULL,
        SessionStamp   NVARCHAR(40)   NOT NULL CONSTRAINT DF_CustomerAccess_Stamp DEFAULT CONVERT(NVARCHAR(40), NEWID()),
        LastLoginUtc   DATETIME2      NULL,
        CreatedAtUtc   DATETIME2      NOT NULL CONSTRAINT DF_CustomerAccess_Created DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc   DATETIME2      NULL
    );
END

IF OBJECT_ID('dbo.CustomerLoginCodes') IS NULL
BEGIN
    CREATE TABLE dbo.CustomerLoginCodes
    (
        Id           INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CustomerLoginCodes PRIMARY KEY,
        AccountNo    BIGINT         NOT NULL,
        CodeHash     NVARCHAR(200)  NOT NULL,
        SentAtUtc    DATETIME2      NOT NULL,
        ExpiresAtUtc DATETIME2      NOT NULL,
        Sent         BIT            NOT NULL,  -- the provider accepted it
        UsedAtUtc    DATETIME2      NULL,
        RequestIp    NVARCHAR(64)   NULL
    );
    CREATE INDEX IX_CustomerLoginCodes_Account ON dbo.CustomerLoginCodes (AccountNo, SentAtUtc DESC);
END
GO
