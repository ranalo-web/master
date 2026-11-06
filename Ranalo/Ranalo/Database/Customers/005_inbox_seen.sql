-- Customer app notifications: when the customer last opened their Inbox.
-- Messages sent to their phone after this are "new": a count on the Inbox
-- tab and a pop-up in the app.
--
-- Run after 001. Needs an admin login. Safe to re-run.

IF COL_LENGTH('dbo.CustomerAccess', 'InboxSeenUtc') IS NULL
    ALTER TABLE dbo.CustomerAccess ADD InboxSeenUtc DATETIME2 NULL;
GO
