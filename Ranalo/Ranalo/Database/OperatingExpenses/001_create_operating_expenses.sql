-- Operating Expenses ledger. Unlike Database/Dashboard/*, this is a live,
-- user-editable table -- Admin logs an expense (salaries, ad costs, rent,
-- etc.) directly, no nightly job involved. Feeds the Financials page's
-- Income Statement ("Less: Operating expenses") and monthly comparison
-- chart, replacing what used to be a flat mock constant.
--
-- Soft-deleted (RemovedByUserId/RemovedAtUtc) to keep an audit trail rather
-- than hard-deleting, same pattern as AccountWatchlist.
--
-- Safe to re-run: guarded with an existence check.

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'OperatingExpenses')
BEGIN
    CREATE TABLE OperatingExpenses
    (
        Id              INT IDENTITY(1,1) PRIMARY KEY,
        ExpenseDate     DATE NOT NULL,
        Category        VARCHAR(50) NOT NULL,   -- Salaries | Marketing | Rent | Utilities | Software | Other
        Description     NVARCHAR(300) NULL,
        Amount          DECIMAL(18,2) NOT NULL,
        AddedByUserId   INT NOT NULL,
        AddedAtUtc      DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        RemovedByUserId INT NULL,
        RemovedAtUtc    DATETIME2 NULL
    );

    CREATE INDEX IX_OperatingExpenses_ExpenseDate
        ON OperatingExpenses (ExpenseDate)
        WHERE RemovedAtUtc IS NULL;
END
GO
