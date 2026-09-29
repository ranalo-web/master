-- Commission payouts: one row per payment made to a dealer or an agent,
-- holding the payment details (date, method, reference, notes) and the
-- optional receipt confirmation from the payee. The per-account split of
-- each payout is written into the existing AgentCommissionPayments /
-- DealerCommissionPayments tables (every "paid" figure in the app already
-- reads those), linked back here by their new PayoutId column.
--
-- Neither existing table's Id is an IDENTITY column (confirmed from the
-- live schema), so CommissionPayoutRepository allocates Ids itself under a
-- lock. Nothing here changes existing rows.
--
-- Safe to re-run: every step is guarded with an existence check.

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CommissionPayouts')
BEGIN
    CREATE TABLE CommissionPayouts
    (
        Id                       INT IDENTITY(1,1) PRIMARY KEY,
        PayeeType                VARCHAR(10) NOT NULL,   -- Agent | Dealer
        DealerId                 INT NULL,               -- Dealers.DealerId (dealer payouts)
        AgentUserId              INT NULL,               -- Users.UserId (agent payouts)
        Amount                   DECIMAL(18,2) NOT NULL,
        PaidDate                 DATE NOT NULL,
        Method                   VARCHAR(20) NOT NULL,   -- M-Pesa | Bank | Cash | Other
        Reference                NVARCHAR(100) NULL,     -- M-Pesa code, bank ref, etc.
        Notes                    NVARCHAR(500) NULL,
        RecordedByUserId         INT NOT NULL,
        RecordedAtUtc            DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        ReceiptConfirmedByUserId INT NULL,
        ReceiptConfirmedAtUtc    DATETIME2 NULL
    );

    CREATE INDEX IX_CommissionPayouts_Dealer ON CommissionPayouts (DealerId) WHERE DealerId IS NOT NULL;
    CREATE INDEX IX_CommissionPayouts_Agent ON CommissionPayouts (AgentUserId) WHERE AgentUserId IS NOT NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AgentCommissionPayments') AND name = 'PayoutId')
BEGIN
    ALTER TABLE AgentCommissionPayments ADD PayoutId INT NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('DealerCommissionPayments') AND name = 'PayoutId')
BEGIN
    ALTER TABLE DealerCommissionPayments ADD PayoutId INT NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('AgentCommissionPayments') AND name = 'IX_AgentCommissionPayments_PayoutId')
BEGIN
    CREATE INDEX IX_AgentCommissionPayments_PayoutId ON AgentCommissionPayments (PayoutId);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('DealerCommissionPayments') AND name = 'IX_DealerCommissionPayments_PayoutId')
BEGIN
    CREATE INDEX IX_DealerCommissionPayments_PayoutId ON DealerCommissionPayments (PayoutId);
END
GO
