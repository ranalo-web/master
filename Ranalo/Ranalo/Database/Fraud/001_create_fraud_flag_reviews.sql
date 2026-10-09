-- Fraud section (admin only). The fraud checks themselves are computed live
-- from contracts, devices, payments and WooCommerce orders every time the
-- page opens (Services/FraudRules.cs) -- nothing is stored for them. This
-- table only holds what an admin decided about a flag:
--   Investigating  being looked into
--   Cleared        false alarm -- hidden from the list unless asked for
--   Confirmed      fraud confirmed
-- A flag with no row here is Open.
--
-- A flag is identified by its check and its subject: the account number
-- (account checks), "order:<WooCommerce order id>" (order IMEI check), or
-- the shared IMEI / phone / ID number / M-Pesa code (shared-detail checks).
--
-- Safe to re-run: guarded with an existence check.

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'FraudFlagReviews')
BEGIN
    CREATE TABLE FraudFlagReviews
    (
        Id               INT IDENTITY(1,1) PRIMARY KEY,
        CheckCode        VARCHAR(40)   NOT NULL,
        SubjectKey       NVARCHAR(100) NOT NULL,
        Status           VARCHAR(20)   NOT NULL,   -- 'Investigating' | 'Cleared' | 'Confirmed'
        Notes            NVARCHAR(1000) NULL,
        ReviewedByUserId INT           NOT NULL,
        ReviewedAtUtc    DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME()
    );

    CREATE UNIQUE INDEX UQ_FraudFlagReviews_Flag ON FraudFlagReviews (CheckCode, SubjectKey);
END
GO
