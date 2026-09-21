-- Manual account watchlist. Unlike Database/Dashboard/*, this is a live,
-- user-editable table -- the web app reads AND writes it directly, no
-- nightly job involved. A user (Admin/Dealer/Agent) explicitly flags an
-- account; there's no automatic classification here.
--
-- Removal permission rule (enforced in AccountWatchlistService, not here):
-- an entry can be removed by whoever added it, or by a strictly higher rank
-- (Admin > Dealer > Agent). Soft-deleted (RemovedByUserId/RemovedAtUtc) to
-- keep an audit trail rather than hard-deleting.
--
-- Safe to re-run: guarded with an existence check.

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AccountWatchlist')
BEGIN
    CREATE TABLE AccountWatchlist
    (
        Id              INT IDENTITY(1,1) PRIMARY KEY,
        AccountId       BIGINT NOT NULL,        -- Contract_Info.ID
        AddedByUserId   INT NOT NULL,
        AddedByRole     VARCHAR(20) NOT NULL,   -- 'Admin' | 'Dealer' | 'Agent' (role snapshot at add time)
        AddedAtUtc      DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        RemovedByUserId INT NULL,
        RemovedAtUtc    DATETIME2 NULL
    );

    -- Only one active (not-yet-removed) watchlist entry per account at a time.
    CREATE UNIQUE INDEX UQ_AccountWatchlist_ActiveAccount
        ON AccountWatchlist (AccountId)
        WHERE RemovedAtUtc IS NULL;

    CREATE INDEX IX_AccountWatchlist_AccountId ON AccountWatchlist (AccountId);
END
GO
