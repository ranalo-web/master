-- MessageLogs had no index but its primary key, so every "was this
-- message already sent?" check read the whole table: 242k rows on
-- 2026-11-04, most of them from the reminder flood. The fully-paid
-- milestone query took 9 s because of it.
--
--   IX_MessageLogs_Type_Status_Account: MilestoneMessagesRepository's
--     NotYetSent (welcome / fully-paid sent or failed for an account or IMEI).
--   IX_MessageLogs_Type_Status_Date: PaymentsRepository.GetSentMessagesSinceAsync
--     (reminders sent in the last 100 minutes).
--
-- Additive only, built ONLINE so sends carry on while it builds. Run before
-- or after deploying. Safe to re-run.

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.MessageLogs') AND name = 'IX_MessageLogs_Type_Status_Account')
    CREATE NONCLUSTERED INDEX IX_MessageLogs_Type_Status_Account
        ON dbo.MessageLogs (MessageType, MessageStatus, AccountNo)
        INCLUDE (DateSent)
        WITH (ONLINE = ON);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.MessageLogs') AND name = 'IX_MessageLogs_Type_Status_Date')
    CREATE NONCLUSTERED INDEX IX_MessageLogs_Type_Status_Date
        ON dbo.MessageLogs (MessageType, MessageStatus, DateSent)
        INCLUDE (AccountNo)
        WITH (ONLINE = ON);
