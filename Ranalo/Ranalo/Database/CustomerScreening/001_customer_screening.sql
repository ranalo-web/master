-- Customer screening and welfare calls (2026-10-21).
--
-- Before an order can be approved, an approver or admin calls the customer
-- and one next of kin and logs each call here (CustomerScreeningCalls). An
-- order passes screening with one customer call that confirmed identity,
-- terms and the lock, and one next of kin call that confirmed they know the
-- customer and the relationship. Unanswered calls are logged too; the order
-- stays pending until it passes or is rejected.
--
-- Once enrolled, the customer gets a welfare call between day 7 and day 30
-- (CustomerWelfareCalls). A call that wasn't answered leaves it due.
--
-- Screening applies to orders created from CustomerScreeningStart.StartsAtUtc
-- (set when this script first runs), and welfare calls to enrolments approved
-- from then -- everything before carries on as it was.
--
-- RUN THIS BEFORE DEPLOYING THE APP. Only adds tables; safe to re-run.

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CustomerScreeningStart')
BEGIN
    CREATE TABLE CustomerScreeningStart
    (
        Id          INT       NOT NULL PRIMARY KEY CHECK (Id = 1),
        StartsAtUtc DATETIME2 NOT NULL
    );

    INSERT INTO CustomerScreeningStart (Id, StartsAtUtc) VALUES (1, SYSUTCDATETIME());
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CustomerScreeningCalls')
BEGIN
    CREATE TABLE CustomerScreeningCalls
    (
        Id                    INT IDENTITY(1,1) PRIMARY KEY,
        OrderId               BIGINT         NOT NULL,
        CallType              VARCHAR(20)    NOT NULL,   -- 'Customer' | 'NextOfKin'
        NextOfKinNo           TINYINT        NULL,       -- 1 or 2 for a next of kin call
        Reached               BIT            NOT NULL,
        -- Customer call
        IdentityConfirmed     BIT            NOT NULL DEFAULT 0,
        TermsConfirmed        BIT            NOT NULL DEFAULT 0,
        LockUnderstood        BIT            NOT NULL DEFAULT 0,
        -- Next of kin call
        KnowsCustomer         BIT            NOT NULL DEFAULT 0,
        RelationshipConfirmed BIT            NOT NULL DEFAULT 0,
        Relationship          NVARCHAR(100)  NULL,
        Notes                 NVARCHAR(1000) NULL,
        CalledByUserId        INT            NOT NULL,
        CalledAtUtc           DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME()
    );

    CREATE INDEX IX_CustomerScreeningCalls_Order ON CustomerScreeningCalls (OrderId);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CustomerWelfareCalls')
BEGIN
    CREATE TABLE CustomerWelfareCalls
    (
        Id             INT IDENTITY(1,1) PRIMARY KEY,
        AccountNo      BIGINT         NOT NULL,
        Outcome        VARCHAR(20)    NOT NULL,   -- 'AllGood' | 'Issue' | 'NoAnswer'
        Notes          NVARCHAR(1000) NULL,
        CalledByUserId INT            NOT NULL,
        CalledAtUtc    DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME()
    );

    CREATE INDEX IX_CustomerWelfareCalls_Account ON CustomerWelfareCalls (AccountNo);
END
GO
