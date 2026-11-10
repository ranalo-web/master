-- Restructure log (2026-11-10): one row each time an agreed restructure is
-- set, changed or undone, with who did it -- RestructuredRecords keeps only
-- the latest plan per account (a new plan overwrites the old one), so it
-- can't say who agreed it or how many times. Shown on the Agreed
-- Restructure report ("Agreed by", "Times restructured", "By staff").
--
-- Plans that exist when this runs get one "Before log" row (who agreed them
-- isn't known), dated the day they were agreed.
--
-- Needs an admin login. Safe to re-run. Until it's run the report shows "-"
-- for these columns and nothing is logged.

IF OBJECT_ID('dbo.RestructureLog', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.RestructureLog (
        Id            INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_RestructureLog PRIMARY KEY,
        AccountNo     BIGINT         NOT NULL,
        Kind          NVARCHAR(30)   NOT NULL,   -- Agreed | Changed | Self-service | Undone | Before log
        DateAgreed    DATETIME2      NULL,
        AmountRes     DECIMAL(18, 2) NULL,
        PrevAmountRes DECIMAL(18, 2) NULL,
        ByUserId      INT            NULL,       -- staff; NULL for the customer or before the log
        ByName        NVARCHAR(120)  NULL,
        Note          NVARCHAR(300)  NULL,
        AtUtc         DATETIME2      NOT NULL CONSTRAINT DF_RestructureLog_AtUtc DEFAULT SYSUTCDATETIME()
    );
    CREATE INDEX IX_RestructureLog_Account ON dbo.RestructureLog (AccountNo, AtUtc);
    CREATE INDEX IX_RestructureLog_User ON dbo.RestructureLog (ByUserId, AtUtc);
END
GO

INSERT INTO dbo.RestructureLog (AccountNo, Kind, DateAgreed, AmountRes, ByName, Note, AtUtc)
SELECT rr.AccountNo, 'Before log', rr.Date_Agreed, rr.Amount_Res, NULL,
       'Agreed before the log started (10 Nov 2026); who agreed it is not known.',
       ISNULL(DATEADD(HOUR, -3, rr.Date_Agreed), SYSUTCDATETIME())
FROM dbo.RestructuredRecords rr
WHERE NOT EXISTS (SELECT 1 FROM dbo.RestructureLog l WHERE l.AccountNo = rr.AccountNo);
GO
