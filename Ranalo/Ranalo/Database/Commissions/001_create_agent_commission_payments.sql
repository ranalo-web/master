-- Mirrors the existing DealerCommissionPayments table (read-only from this
-- codebase -- nothing here writes to DealerCommissionPayments either, so it's
-- assumed to be populated by an external/ops process). Records actual
-- commission payouts made to agents, keyed by Contract_Info.ContractID (same
-- join key DealerCommissionPayments uses, confirmed via
-- "dp.ContractId = c.ContractID" in DataStore/CommissionsRepository.cs).
--
-- NOTE: DealerCommissionPayments' exact full schema isn't visible from this
-- codebase (created directly on the DB, never scripted here) -- only
-- ContractId and AmountPaid are confirmed via usage. This mirrors those plus
-- sensible audit columns.
--
-- Safe to re-run: guarded with an existence check.

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AgentCommissionPayments')
BEGIN
    CREATE TABLE AgentCommissionPayments
    (
        Id           INT IDENTITY(1,1) PRIMARY KEY,
        ContractId   INT NOT NULL,       -- Contract_Info.ContractID
        AmountPaid   DECIMAL(18,2) NOT NULL,
        PaymentDate  DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );

    CREATE INDEX IX_AgentCommissionPayments_ContractId ON AgentCommissionPayments (ContractId);
END
GO
