-- Bad-debt write-offs. Policy (agreed 2026-09-28):
--   * An account is written off 360 days after its last payment (or after
--     the contract start if it never paid), dated on the day it crossed
--     that line -- so past periods on Financials show the right figures.
--   * A repossessed device's old contract is written off on the recovery
--     date, whatever its payment history.
--   * Any payment after a write-off reinstates the full balance, dated on
--     the payment day, and the 360-day clock restarts from that payment.
--   * Nothing is written off without an admin approving it: a nightly job
--     only proposes rows (Status = 'Pending').
--
-- WriteOffs is a register of dated events, not a flag on the contract, so
-- a past period can be reported as it stood then -- an account written off
-- in 2025 and reinstated in 2026 still counts as written off in 2025.
-- A contract can have several rows (written off, reinstated, written off
-- again).
--
-- Safe to re-run: every object is guarded with an existence check.

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'WriteOffs')
BEGIN
    CREATE TABLE WriteOffs
    (
        Id                  INT IDENTITY(1,1) PRIMARY KEY,
        ContractId          INT NOT NULL,            -- Contract_Info.ContractID
        AccountNo           BIGINT NOT NULL,         -- Contract_Info.ID (device) at the time
        Reason              VARCHAR(20) NOT NULL,    -- NoPayment360 | Repossessed
        WrittenOffDate      DATE NOT NULL,           -- effective date (crossed 360 days, or recovery date)
        LastPaymentDate     DATE NULL,               -- the payment the 360 days are counted from (NULL = never paid)

        -- Snapshot at the write-off date: what was owed and what had been
        -- paid. The real-loss figure (device cost + commissions +
        -- repossession cost - paid - resale) is built from these plus
        -- DeviceRecoveries when reporting.
        ContractValue       DECIMAL(18,2) NOT NULL,
        TotalPaid           DECIMAL(18,2) NOT NULL,
        OutstandingBalance  DECIMAL(18,2) NOT NULL,

        Status              VARCHAR(20) NOT NULL DEFAULT 'Pending',   -- Pending | Approved | Held
        DetectedAtUtc       DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        ReviewedByUserId    INT NULL,
        ReviewedAtUtc       DATETIME2 NULL,
        HoldReason          NVARCHAR(300) NULL,

        -- Set when a payment arrives after the write-off (any amount).
        ReinstatedDate      DATE NULL,
        ReinstatedMpesaCode VARCHAR(50) NULL
    );

    -- One row per write-off event: the nightly job and the historical
    -- backfill can both run repeatedly without duplicating.
    CREATE UNIQUE INDEX UX_WriteOffs_Contract_Date
        ON WriteOffs (ContractId, WrittenOffDate);

    CREATE INDEX IX_WriteOffs_Status
        ON WriteOffs (Status, WrittenOffDate);
END
GO

-- A device taken back from a customer and given to a new one
-- (ReportsController.RecoverAccount -> ContractRepository.CreateRecoveredAccount).
-- ResaleValue is the new customer's full contract value; RepossessionCost is
-- what it cost to get the device back (transport, agent fees, repairs).
-- Both feed the old contract's real-loss figure.
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'DeviceRecoveries')
BEGIN
    CREATE TABLE DeviceRecoveries
    (
        Id                  INT IDENTITY(1,1) PRIMARY KEY,
        AccountNo           BIGINT NOT NULL,         -- the device
        OldContractId       INT NOT NULL,            -- contract that ended on recovery
        NewContractId       INT NULL,                -- contract created for the new customer
        RecoveredDate       DATE NOT NULL,
        ResaleValue         DECIMAL(18,2) NOT NULL DEFAULT 0,
        RepossessionCost    DECIMAL(18,2) NOT NULL DEFAULT 0,
        Notes               NVARCHAR(300) NULL,
        RecordedByUserId    INT NOT NULL,
        RecordedAtUtc       DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );

    CREATE INDEX IX_DeviceRecoveries_OldContract
        ON DeviceRecoveries (OldContractId);
END
GO
