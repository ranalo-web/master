-- Repo stock (admin only): phones taken back -- repossessed, or handed
-- back by a customer -- waiting to be sold again on new terms. Agreed
-- 2026-10-15; rules in Services/RepoStockRules.cs.
--
-- Add to stock: the old contract ends that day, and the old customer's
-- payments are renamed "<account>_R" (a second time on the same account
-- "_R2", then "_R3"...) so they stay with that contract. The phone keeps
-- its account number and its lock (Nuovo device numbers can't change).
-- Sell: a new contract on the same account, starting that day, credited
-- to the selling dealer/agent; the old contract's unpaid balance is
-- proposed as a Repossessed write-off.
--
-- Contract_Info gets two columns so a contract keeps its own payments and
-- dealer once its phone has moved on:
--   PaymentAccount  -- where its payments are ("8478363_R"); NULL = the
--                      contract's own account number, as before.
--   DealerReference -- its dealer of record; NULL = the device's dealer
--                      (Devices.DeviceGroupId), as before.
--
-- Safe to re-run: guarded with existence checks.

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Contract_Info') AND name = 'PaymentAccount')
BEGIN
    ALTER TABLE Contract_Info ADD PaymentAccount NVARCHAR(30) NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Contract_Info') AND name = 'DealerReference')
BEGIN
    ALTER TABLE Contract_Info ADD DealerReference INT NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'RepoStock')
BEGIN
    CREATE TABLE RepoStock
    (
        Id                INT IDENTITY(1,1) PRIMARY KEY,
        AccountNo         BIGINT        NOT NULL,   -- Devices.Id / Contract_Info.ID
        Imei              VARCHAR(20)   NULL,
        Make              NVARCHAR(50)  NULL,
        Model             NVARCHAR(100) NULL,
        LockGroup         INT           NULL,       -- locker when added (1 Nuovo, 2 Knox, 3 Transsion)
        OldContractId     INT           NOT NULL,   -- the contract that ended on intake
        OldCustomerName   NVARCHAR(200) NULL,
        PaymentSuffix     VARCHAR(5)    NOT NULL,   -- '_R', '_R2', ...
        Reason            VARCHAR(20)   NOT NULL,   -- Repossessed | Returned | Other
        Condition         NVARCHAR(500) NULL,
        Status            VARCHAR(20)   NOT NULL,   -- InStock | Sold
        IntakeDate        DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),
        IntakeByUserId    INT           NULL,
        NewContractId     INT           NULL,
        NewCustomerName   NVARCHAR(200) NULL,
        NewCustomerPhone  NVARCHAR(30)  NULL,
        NewCustomerIdNo   NVARCHAR(30)  NULL,
        SoldDate          DATETIME2     NULL,
        SoldByUserId      INT           NULL,
        SoldDealerReference INT         NULL,
        WriteOffAmount    DECIMAL(18,2) NULL
    );

    -- One phone in stock per account at a time.
    CREATE UNIQUE INDEX UQ_RepoStock_InStockAccount
        ON RepoStock (AccountNo)
        WHERE Status = 'InStock';

    CREATE INDEX IX_RepoStock_OldContract ON RepoStock (OldContractId);
END
GO
