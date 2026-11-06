-- Self-service restructure from the customer app (SelfRestructureRules).
-- One row per plan a customer chose:
--   Pending   chosen, waiting for the first day's payment (24 hours)
--   Expired   not paid in time -- doesn't use up their one go
--   Active    started: saved to RestructuredRecords like a staff plan
--   Reverted  undone by an admin: the previous plan put back (still used)
--   Cancelled no longer allowed when the payment came (e.g. contract ended)
-- PrevHadPlan / PrevDateAgreed / PrevAmountRes: the account's plan before,
-- so an undo restores it exactly.
--
-- The indexes make the rules hold even if two requests race: at most one
-- Pending, and at most one Active or Reverted, per contract.
--
-- Needs an admin login. Safe to re-run.

IF OBJECT_ID('dbo.SelfRestructures') IS NULL
BEGIN
    CREATE TABLE dbo.SelfRestructures
    (
        Id               INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_SelfRestructures PRIMARY KEY,
        AccountNo        BIGINT        NOT NULL,
        ContractId       INT           NOT NULL,
        Status           NVARCHAR(20)  NOT NULL,
        OptionName       NVARCHAR(20)  NOT NULL,
        Daily            DECIMAL(18,2) NOT NULL,
        BalanceAtChoice  DECIMAL(18,2) NOT NULL,
        BehindAtChoice   DECIMAL(18,2) NOT NULL,
        ChosenAtUtc      DATETIME2     NOT NULL,
        ExpiresAtUtc     DATETIME2     NOT NULL,
        StartedAtUtc     DATETIME2     NULL,
        PrevHadPlan      BIT           NOT NULL,
        PrevDateAgreed   DATETIME      NULL,
        PrevAmountRes    DECIMAL(18,2) NULL,
        ReviewedAtUtc    DATETIME2     NULL,
        ReviewedByUserId INT           NULL,
        RevertedAtUtc    DATETIME2     NULL,
        RevertedByUserId INT           NULL,
        Note             NVARCHAR(400) NULL
    );
    CREATE INDEX IX_SelfRestructures_Account ON dbo.SelfRestructures (AccountNo, ChosenAtUtc DESC);
    CREATE UNIQUE INDEX UX_SelfRestructures_OnePending ON dbo.SelfRestructures (ContractId) WHERE Status = 'Pending';
    CREATE UNIQUE INDEX UX_SelfRestructures_OneUsed ON dbo.SelfRestructures (ContractId) WHERE Status IN ('Active', 'Reverted');
END
GO
