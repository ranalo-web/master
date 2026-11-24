-- Users.LastSeen: the last time each person used the app (UTC), stamped by
-- UserSettingsMiddleware at most every few minutes (LastSeenRules). The
-- Users page shows it beside Last Login, which only changes when someone
-- types their password -- and people stay logged in for days.
--
-- RUN THIS BEFORE DEPLOYING THE APP: the User model reads this column, so
-- without it nobody can log in. Safe to re-run.

IF COL_LENGTH('dbo.Users', 'LastSeen') IS NULL
    ALTER TABLE dbo.Users ADD LastSeen DATETIME2 NULL;
GO

-- Start everyone from their last login.
UPDATE dbo.Users SET LastSeen = LastLogin WHERE LastSeen IS NULL AND LastLogin IS NOT NULL;
GO
