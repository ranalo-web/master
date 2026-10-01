-- Collections. Rules (agreed with the business 2026-10-01):
--   * Admin hands an account to a collector from the pool of accounts in
--     arrears with nothing paid for 90 days. A collector can't take an
--     account where they are the agent or the dealer.
--   * At handover the agent's and dealer's arrears deduction on that
--     contract is FROZEN at the shortfall on the day; money the collector
--     recovers never reduces it. Their unpaid bonus on it is cancelled and
--     the account counts toward their default rate for good.
--   * The collector assigned at the time earns 20% of every payment on the
--     device -- including a new customer's payments after a repossession and
--     resale -- until the account is reassigned (the new collector earns from
--     then on), returned, or the device's newer contract goes to collections.
--   * Returned to the agent/dealer: their deduction becomes the frozen amount
--     + what collectors earned on it + repossession cost + any new shortfall
--     after the return. Sent back to collections: re-frozen at that figure.
--
-- CollectionCases: one row per spell in collections of one contract.
-- CollectionAssignments: which collector held a case, from when to when.
-- CollectionFlags: a collector's "can't reach this customer" notes.
-- CollectorCommissionPayments: collector payouts per case (the payout
-- header is CommissionPayouts, PayeeType 'Collector').
--
-- The last step is the go-live backfill: every open contract that already
-- has a collector (Contract_Info.DebtCollectorUserId) gets a case handed
-- over NOW, frozen at today's shortfall, so collectors earn only from
-- today. Run this script on the day the feature is deployed.
--
-- Safe to re-run: every step is guarded.

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CollectionCases')
BEGIN
    CREATE TABLE CollectionCases
    (
        Id                   INT IDENTITY(1,1) PRIMARY KEY,
        AccountNo            BIGINT NOT NULL,          -- Contract_Info.ID (the device)
        ContractId           BIGINT NOT NULL,          -- Contract_Info.ContractID handed over
        DealerId             INT NULL,                 -- at handover
        AgentUserId          INT NULL,                 -- at handover
        HandoverAt           DATETIME2 NOT NULL,
        ShortfallAtHandover  DECIMAL(18,2) NOT NULL,   -- the customer's shortfall on the day
        FrozenDeduction      DECIMAL(18,2) NOT NULL,   -- agent/dealer deduction from now on
        TotalPaidAtHandover  DECIMAL(18,2) NOT NULL,
        -- Customer payments that count toward the dealer's commission
        -- (payments while in collections never do).
        CommissionPaidBasis  DECIMAL(18,2) NOT NULL,
        Status               VARCHAR(12) NOT NULL DEFAULT 'Open',   -- Open | Returned | Superseded
        ClosedAt             DATETIME2 NULL,
        ShortfallAtReturn    DECIMAL(18,2) NULL,
        TotalPaidAtReturn    DECIMAL(18,2) NULL,
        RecoveredOnCase      DECIMAL(18,2) NULL,       -- snapshot when closed
        CollectorEarnedOnCase DECIMAL(18,2) NULL,      -- snapshot when closed
        OpenedByUserId       INT NOT NULL,
        ClosedByUserId       INT NULL,
        IsGoLiveBackfill     BIT NOT NULL DEFAULT 0,
        Notes                NVARCHAR(500) NULL
    );

    CREATE INDEX IX_CollectionCases_Contract ON CollectionCases (ContractId, Id);
    CREATE INDEX IX_CollectionCases_Account ON CollectionCases (AccountNo, Status);
    -- A device is with at most one collections case at a time.
    CREATE UNIQUE INDEX UX_CollectionCases_OpenAccount ON CollectionCases (AccountNo) WHERE Status = 'Open';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CollectionAssignments')
BEGIN
    CREATE TABLE CollectionAssignments
    (
        Id               INT IDENTITY(1,1) PRIMARY KEY,
        CaseId           INT NOT NULL,
        CollectorUserId  INT NOT NULL,
        StartAt          DATETIME2 NOT NULL,
        EndAt            DATETIME2 NULL,              -- NULL = holds the case now
        AssignedByUserId INT NOT NULL,
        EndedByUserId    INT NULL,
        EndReason        VARCHAR(20) NULL             -- Reassigned | Returned | Superseded
    );

    CREATE INDEX IX_CollectionAssignments_Case ON CollectionAssignments (CaseId);
    CREATE INDEX IX_CollectionAssignments_Collector ON CollectionAssignments (CollectorUserId);
    CREATE UNIQUE INDEX UX_CollectionAssignments_OpenCase ON CollectionAssignments (CaseId) WHERE EndAt IS NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CollectionFlags')
BEGIN
    CREATE TABLE CollectionFlags
    (
        Id               INT IDENTITY(1,1) PRIMARY KEY,
        CaseId           INT NOT NULL,
        CollectorUserId  INT NOT NULL,
        Note             NVARCHAR(500) NOT NULL,
        CreatedAt        DATETIME2 NOT NULL DEFAULT GETDATE(),
        ResolvedAt       DATETIME2 NULL,
        ResolvedByUserId INT NULL
    );

    CREATE INDEX IX_CollectionFlags_Case ON CollectionFlags (CaseId);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CommissionPayouts') AND name = 'CollectorUserId')
BEGIN
    ALTER TABLE CommissionPayouts ADD CollectorUserId INT NULL;   -- Users.UserId (collector payouts)
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CollectorCommissionPayments')
BEGIN
    CREATE TABLE CollectorCommissionPayments
    (
        Id              INT IDENTITY(1,1) PRIMARY KEY,
        PayoutId        INT NOT NULL,                 -- CommissionPayouts.Id
        CaseId          INT NOT NULL,                 -- CollectionCases.Id
        CollectorUserId INT NOT NULL,
        Amount          DECIMAL(18,2) NOT NULL,
        PaidDate        DATE NOT NULL
    );

    CREATE INDEX IX_CollectorCommissionPayments_Collector ON CollectorCommissionPayments (CollectorUserId, CaseId);
    CREATE INDEX IX_CollectorCommissionPayments_Payout ON CollectorCommissionPayments (PayoutId);
END
GO

-- Go-live backfill: accounts that already have a collector.
-- Shortfall uses the same formula as the commission calculator
-- (DashboardReportRepository.FetchAgentCommissionAccountRowsAsync).
IF NOT EXISTS (SELECT 1 FROM CollectionCases WHERE IsGoLiveBackfill = 1)
BEGIN
    DECLARE @Now DATETIME2 = GETDATE();

    ;WITH ValidPayments AS (
        SELECT COALESCE(op.AccountNoBigint, kp.AccountNoBigint) AS AccountNo, kp.AmountValue
        FROM KosePayments kp
        LEFT JOIN OrphanedPayments op ON op.MpesaCode = kp.MpesaCode
    ),
    PaymentTotals AS (
        SELECT AccountNo, SUM(AmountValue) AS TotalPaid
        FROM ValidPayments
        GROUP BY AccountNo
    ),
    Assigned AS (
        SELECT
            ci.ID AS AccountNo,
            ci.ContractID AS ContractId,
            ci.DebtCollectorUserId AS CollectorUserId,
            dl.DealerId,
            ci.AssignedAgentId AS AgentUserId,
            ISNULL(pt.TotalPaid, 0) AS TotalPaid,
            ISNULL(pt.TotalPaid, 0)
                - (ci.Deposit
                   + ci.Daily * DaysAccrued.Days
                   + ci.Weekly * (DaysAccrued.Days / 7.0)
                   + ci.Monthly * (DaysAccrued.Days / 30.0)) AS Arrears,
            ROW_NUMBER() OVER (PARTITION BY ci.ID ORDER BY ci.ContractID DESC) AS rn
        FROM Contract_Info ci
        LEFT JOIN Devices d ON d.Id = ci.ID
        LEFT JOIN Dealers dl ON dl.DealerReference = d.DeviceGroupId
        LEFT JOIN PaymentTotals pt ON pt.AccountNo = ci.ID
        CROSS APPLY (
            SELECT CASE
                WHEN DATEDIFF(DAY, ci.StartDate, GETDATE()) < CAST(ci.Term_in_Months * 30 AS INT)
                    THEN DATEDIFF(DAY, ci.StartDate, GETDATE())
                ELSE CAST(ci.Term_in_Months * 30 AS INT)
            END AS Days
        ) DaysAccrued
        WHERE ci.DebtCollectorUserId IS NOT NULL
          AND ci.EndDate IS NULL
          AND ci.StartDate IS NOT NULL
    )
    INSERT INTO CollectionCases
        (AccountNo, ContractId, DealerId, AgentUserId, HandoverAt, ShortfallAtHandover, FrozenDeduction,
         TotalPaidAtHandover, CommissionPaidBasis, Status, OpenedByUserId, IsGoLiveBackfill, Notes)
    SELECT a.AccountNo, a.ContractId, a.DealerId, a.AgentUserId, @Now,
           CAST(CASE WHEN a.Arrears < 0 THEN -a.Arrears ELSE 0 END AS DECIMAL(18,2)),
           CAST(CASE WHEN a.Arrears < 0 THEN -a.Arrears ELSE 0 END AS DECIMAL(18,2)),
           a.TotalPaid, a.TotalPaid, 'Open', 0, 1,
           'Already with collector ' + CAST(a.CollectorUserId AS VARCHAR(12)) + ' at go-live'
    FROM Assigned a
    WHERE a.rn = 1
      AND NOT EXISTS (SELECT 1 FROM CollectionCases c WHERE c.AccountNo = a.AccountNo AND c.Status = 'Open');

    INSERT INTO CollectionAssignments (CaseId, CollectorUserId, StartAt, AssignedByUserId)
    SELECT c.Id, ci.DebtCollectorUserId, c.HandoverAt, 0
    FROM CollectionCases c
    INNER JOIN Contract_Info ci ON ci.ContractID = c.ContractId AND ci.ID = c.AccountNo
    WHERE c.IsGoLiveBackfill = 1
      AND ci.DebtCollectorUserId IS NOT NULL
      AND NOT EXISTS (SELECT 1 FROM CollectionAssignments x WHERE x.CaseId = c.Id);
END
GO
