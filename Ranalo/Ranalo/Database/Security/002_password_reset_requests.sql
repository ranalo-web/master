-- "Forgot password?" on the sign-in page (2026-10-21). There's no email or
-- SMS for staff yet, so a request is shown to whoever manages that login
-- (admins; a dealer for their own agents and collectors) on the Users page,
-- and cleared when they reset the password.
--
-- RUN BEFORE DEPLOYING THE APP. Only adds a table; safe to re-run.

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PasswordResetRequests')
BEGIN
    CREATE TABLE PasswordResetRequests
    (
        Id               INT IDENTITY(1,1) PRIMARY KEY,
        UserId           INT       NOT NULL,
        RequestedAtUtc   DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        ResolvedAtUtc    DATETIME2 NULL,
        ResolvedByUserId INT       NULL
    );

    CREATE INDEX IX_PasswordResetRequests_Open ON PasswordResetRequests (UserId) WHERE ResolvedAtUtc IS NULL;
END
GO
