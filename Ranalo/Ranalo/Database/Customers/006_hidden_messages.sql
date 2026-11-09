-- Customer app: notifications a customer deleted from their app.
-- Only hides them in the app. MessageLogs is never changed, so staff still
-- see every message that was sent.
--
-- Run after 001. Needs an admin login. Safe to re-run.
-- Until it's run, the app works as before but Delete does nothing.

IF OBJECT_ID('dbo.CustomerHiddenMessages', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.CustomerHiddenMessages (
        AccountNo    BIGINT           NOT NULL,
        MessageLogId UNIQUEIDENTIFIER NOT NULL,
        HiddenAtUtc  DATETIME2        NOT NULL CONSTRAINT DF_CustomerHiddenMessages_HiddenAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_CustomerHiddenMessages PRIMARY KEY (AccountNo, MessageLogId)
    );
END
GO
