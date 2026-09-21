-- Full one-off manual populate for staging verification -- mirrors every
-- refresh ScheduledDashboardRollup performs, in the same order, but as one
-- runnable script (the job itself is still disabled pending your review).
-- Safe to re-run: every section is either an upsert (MERGE) or a full
-- delete+replace of its own table. Nothing existing is modified except the
-- six DashboardX tables and DashboardSnapshot's new columns.
--
-- Requires: Database/Dashboard/001_create_dashboard_tables.sql,
--           Database/Dashboard/002_extend_completed_contracts.sql,
--           Database/Dashboard/003_add_commission_snapshot_fields.sql,
--           Database/Commissions/001_create_agent_commission_payments.sql
-- (you said these have already been applied).

-- ===== 1. KPI: revenue this/last month, total accounts, new accounts =====
;WITH RevenueByDealer AS (
    SELECT
        dl.DealerId,
        SUM(CASE WHEN MONTH(kp.PaymentDateValue) = MONTH(GETDATE()) AND YEAR(kp.PaymentDateValue) = YEAR(GETDATE())
                 THEN kp.AmountValue ELSE 0 END) AS RevenueThisMonth,
        SUM(CASE WHEN MONTH(kp.PaymentDateValue) = MONTH(DATEADD(MONTH, -1, GETDATE())) AND YEAR(kp.PaymentDateValue) = YEAR(DATEADD(MONTH, -1, GETDATE()))
                 THEN kp.AmountValue ELSE 0 END) AS RevenueLastMonth
    FROM KosePayments kp
    INNER JOIN Devices d ON d.Id = kp.AccountNoBigint
    INNER JOIN Dealers dl ON dl.DealerReference = d.DeviceGroupId
    GROUP BY GROUPING SETS ((dl.DealerId), ())
),
AccountsByDealer AS (
    SELECT
        dl.DealerId,
        COUNT(*) AS TotalAccounts,
        SUM(CASE WHEN MONTH(ci.StartDate) = MONTH(GETDATE()) AND YEAR(ci.StartDate) = YEAR(GETDATE()) THEN 1 ELSE 0 END) AS NewThisMonth
    FROM Contract_Info ci
    INNER JOIN Devices d ON d.Id = ci.ID
    INNER JOIN Dealers dl ON dl.DealerReference = d.DeviceGroupId
    WHERE ci.StartDate IS NOT NULL
    GROUP BY GROUPING SETS ((dl.DealerId), ())
),
Kpi AS (
    SELECT
        COALESCE(r.DealerId, a.DealerId) AS DealerId,
        ISNULL(r.RevenueThisMonth, 0) AS RevenueThisMonth,
        ISNULL(r.RevenueLastMonth, 0) AS RevenueLastMonth,
        ISNULL(a.TotalAccounts, 0) AS TotalAccounts,
        ISNULL(a.NewThisMonth, 0) AS NewThisMonth
    FROM RevenueByDealer r
    FULL OUTER JOIN AccountsByDealer a
        ON a.DealerId = r.DealerId OR (a.DealerId IS NULL AND r.DealerId IS NULL)
)
MERGE DashboardSnapshot AS target
USING Kpi AS source
    ON (target.DealerId = source.DealerId OR (target.DealerId IS NULL AND source.DealerId IS NULL))
    AND target.AgentId IS NULL
WHEN MATCHED THEN
    UPDATE SET
        RevenueThisMonth = source.RevenueThisMonth,
        RevenueGrowthPct = CASE WHEN source.RevenueLastMonth = 0 THEN NULL
                                ELSE ROUND((source.RevenueThisMonth - source.RevenueLastMonth) / source.RevenueLastMonth * 100.0, 2) END,
        NewThisMonth = source.NewThisMonth,
        TotalAccounts = source.TotalAccounts,
        RefreshedAtUtc = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
    INSERT (DealerId, AgentId, RevenueThisMonth, RevenueGrowthPct, NewThisMonth, TotalAccounts, RefreshedAtUtc)
    VALUES (source.DealerId, NULL, source.RevenueThisMonth,
            CASE WHEN source.RevenueLastMonth = 0 THEN NULL
                 ELSE ROUND((source.RevenueThisMonth - source.RevenueLastMonth) / source.RevenueLastMonth * 100.0, 2) END,
            source.NewThisMonth, source.TotalAccounts, SYSUTCDATETIME());
GO

-- ===== 2. Arrears/portfolio classification =====
;WITH ValidPayments AS (
    SELECT COALESCE(op.AccountNoBigint, kp.AccountNoBigint) AS AccountNo, kp.AmountValue
    FROM KosePayments kp
    LEFT JOIN OrphanedPayments op ON op.MpesaCode = kp.MpesaCode
),
PaymentTotals AS (
    SELECT AccountNo, SUM(AmountValue) AS TotalPaid FROM ValidPayments GROUP BY AccountNo
),
AccountClassification AS (
    SELECT
        dl.DealerId,
        ISNULL(pt.TotalPaid, 0)
            - (ci.Deposit + ci.Daily * DaysAccrued.Days + ci.Weekly * (DaysAccrued.Days / 7.0) + ci.Monthly * (DaysAccrued.Days / 30.0)) AS Arrears,
        (ci.Daily + (ci.Weekly / 7.0) + (ci.Monthly / 30.0)) AS DailyBlendedRate
    FROM Contract_Info ci
    INNER JOIN Devices d ON d.Id = ci.ID
    INNER JOIN Dealers dl ON dl.DealerReference = d.DeviceGroupId
    LEFT JOIN PaymentTotals pt ON pt.AccountNo = ci.ID
    CROSS APPLY (
        SELECT CASE WHEN DATEDIFF(DAY, ci.StartDate, GETDATE()) < CAST(ci.Term_in_Months * 30 AS INT)
                    THEN DATEDIFF(DAY, ci.StartDate, GETDATE())
                    ELSE CAST(ci.Term_in_Months * 30 AS INT) END AS Days
    ) DaysAccrued
    WHERE ci.StartDate IS NOT NULL
),
Tiered AS (
    SELECT DealerId, Arrears,
        CASE WHEN DailyBlendedRate = 0 THEN NULL ELSE -(Arrears / DailyBlendedRate) END AS DaysOverdue
    FROM AccountClassification
    WHERE DailyBlendedRate <> 0
),
Portfolio AS (
    SELECT
        DealerId,
        100.0 * SUM(CASE WHEN DaysOverdue <= 0 THEN 1 ELSE 0 END) / COUNT(*) AS GoodPct,
        100.0 * SUM(CASE WHEN DaysOverdue > 0 AND DaysOverdue <= 7 THEN 1 ELSE 0 END) / COUNT(*) AS SlowPct,
        100.0 * SUM(CASE WHEN DaysOverdue > 7 THEN 1 ELSE 0 END) / COUNT(*) AS ArrearsPct,
        100.0 * SUM(CASE WHEN DaysOverdue > 90 THEN 1 ELSE 0 END) / COUNT(*) AS NonPayingPct,
        SUM(CASE WHEN Arrears < 0 THEN -Arrears ELSE 0 END) AS ArrearsTotal
    FROM Tiered
    GROUP BY GROUPING SETS ((DealerId), ())
)
MERGE DashboardSnapshot AS target
USING Portfolio AS source
    ON (target.DealerId = source.DealerId OR (target.DealerId IS NULL AND source.DealerId IS NULL))
    AND target.AgentId IS NULL
WHEN MATCHED THEN
    UPDATE SET
        PortfolioGoodPct = source.GoodPct,
        PortfolioSlowPct = source.SlowPct,
        PortfolioArrearsPct = source.ArrearsPct,
        PortfolioNonPayingPct = source.NonPayingPct,
        ArrearsTotal = source.ArrearsTotal,
        RefreshedAtUtc = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
    INSERT (DealerId, AgentId, PortfolioGoodPct, PortfolioSlowPct, PortfolioArrearsPct, PortfolioNonPayingPct, ArrearsTotal, RefreshedAtUtc)
    VALUES (source.DealerId, NULL, source.GoodPct, source.SlowPct, source.ArrearsPct, source.NonPayingPct, source.ArrearsTotal, SYSUTCDATETIME());
GO

-- ===== 3. Completed contracts / upsell targets (full delete+replace) =====
DELETE FROM DashboardCompletedContract;

;WITH ValidPayments AS (
    SELECT COALESCE(op.AccountNoBigint, kp.AccountNoBigint) AS AccountNo, kp.AmountValue, kp.PaymentDateValue
    FROM KosePayments kp
    LEFT JOIN OrphanedPayments op ON op.MpesaCode = kp.MpesaCode
),
PaymentTotals AS (
    SELECT AccountNo, SUM(AmountValue) AS TotalPaid, MAX(PaymentDateValue) AS LastPaidDate
    FROM ValidPayments
    GROUP BY AccountNo
),
Classified AS (
    SELECT
        dl.DealerId,
        ci.ID AS AccountId,
        ci.First_Name AS CustomerName,
        dl.CompanyName AS DealerName,
        ISNULL(NULLIF(LTRIM(RTRIM(ISNULL(d.Make, '') + ' ' + ISNULL(d.Model, ''))), ''), 'Unknown Device') AS ProductName,
        CAST(ci.Term_in_Months AS INT) AS DurationMonths,
        ISNULL(pt.TotalPaid, 0) AS TotalPaid,
        pt.LastPaidDate,
        (ci.Deposit + ci.Daily * 30 * ci.Term_in_Months + ci.Weekly * (30.0 / 7.0) * ci.Term_in_Months + ci.Monthly * ci.Term_in_Months) AS FullContractValue
    FROM Contract_Info ci
    INNER JOIN Devices d ON d.Id = ci.ID
    INNER JOIN Dealers dl ON dl.DealerReference = d.DeviceGroupId
    LEFT JOIN PaymentTotals pt ON pt.AccountNo = ci.ID
    WHERE ci.StartDate IS NOT NULL
),
Tiered AS (
    SELECT
        DealerId, AccountId, CustomerName, DealerName, ProductName, DurationMonths, TotalPaid, LastPaidDate,
        (FullContractValue - TotalPaid) AS LoanBalance,
        (TotalPaid / FullContractValue) * 100.0 AS RawPctComplete
    FROM Classified
    WHERE FullContractValue <> 0
),
Qualifying AS (
    SELECT
        DealerId, AccountId, CustomerName, DealerName, ProductName, DurationMonths, TotalPaid, LastPaidDate,
        CASE WHEN LoanBalance <= 0 THEN 'Completed' ELSE 'UpsellTarget' END AS Status,
        CASE WHEN RawPctComplete > 100 THEN 100 ELSE RawPctComplete END AS PctComplete
    FROM Tiered
    WHERE LoanBalance <= 0 OR RawPctComplete >= 80
),
RankedPerDealer AS (
    SELECT *, ROW_NUMBER() OVER (
        PARTITION BY DealerId
        ORDER BY CASE WHEN Status = 'Completed' THEN 0 ELSE 1 END, LastPaidDate DESC, PctComplete DESC
    ) AS Rn
    FROM Qualifying
),
RankedGlobal AS (
    SELECT *, ROW_NUMBER() OVER (
        ORDER BY CASE WHEN Status = 'Completed' THEN 0 ELSE 1 END, LastPaidDate DESC, PctComplete DESC
    ) AS Rn
    FROM Qualifying
)
INSERT INTO DashboardCompletedContract
    (ScopeDealerId, ScopeAgentId, AccountId, CustomerName, DealerName, ProductName, CompletedDate, TotalPaid, DurationMonths, Status, PctComplete)
SELECT DealerId, NULL, AccountId, CustomerName, DealerName, ProductName,
       CASE WHEN Status = 'Completed' THEN CAST(LastPaidDate AS DATE) ELSE NULL END,
       TotalPaid, DurationMonths, Status, PctComplete
FROM RankedPerDealer WHERE Rn <= 20
UNION ALL
SELECT NULL, NULL, AccountId, CustomerName, DealerName, ProductName,
       CASE WHEN Status = 'Completed' THEN CAST(LastPaidDate AS DATE) ELSE NULL END,
       TotalPaid, DurationMonths, Status, PctComplete
FROM RankedGlobal WHERE Rn <= 20;
GO

-- ===== 4. Device stock (Dealer-only, full delete+replace) =====
DELETE FROM DashboardDeviceStock;

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
AccountClassification AS (
    SELECT
        dl.DealerId,
        ISNULL(NULLIF(LTRIM(RTRIM(ISNULL(d.Make, '') + ' ' + ISNULL(d.Model, ''))), ''), 'Unknown Device') AS DeviceName,
        (ci.Deposit + ci.Daily * 30 * ci.Term_in_Months + ci.Weekly * (30.0 / 7.0) * ci.Term_in_Months + ci.Monthly * ci.Term_in_Months) AS FullContractValue,
        ISNULL(pt.TotalPaid, 0)
            - (ci.Deposit + ci.Daily * DaysAccrued.Days + ci.Weekly * (DaysAccrued.Days / 7.0) + ci.Monthly * (DaysAccrued.Days / 30.0)) AS Arrears,
        (ci.Daily + (ci.Weekly / 7.0) + (ci.Monthly / 30.0)) AS DailyBlendedRate
    FROM Contract_Info ci
    INNER JOIN Devices d ON d.Id = ci.ID
    INNER JOIN Dealers dl ON dl.DealerReference = d.DeviceGroupId
    LEFT JOIN PaymentTotals pt ON pt.AccountNo = ci.ID
    CROSS APPLY (
        SELECT CASE WHEN DATEDIFF(DAY, ci.StartDate, GETDATE()) < CAST(ci.Term_in_Months * 30 AS INT)
                    THEN DATEDIFF(DAY, ci.StartDate, GETDATE())
                    ELSE CAST(ci.Term_in_Months * 30 AS INT) END AS Days
    ) DaysAccrued
    WHERE ci.StartDate IS NOT NULL
),
Tiered AS (
    SELECT
        DealerId, DeviceName, FullContractValue,
        CASE WHEN DailyBlendedRate = 0 THEN NULL ELSE -(Arrears / DailyBlendedRate) END AS DaysOverdue
    FROM AccountClassification
    WHERE DailyBlendedRate <> 0
),
ByDevice AS (
    SELECT
        DealerId, DeviceName,
        COUNT(*) AS Units,
        AVG(FullContractValue) AS AvgValue,
        100.0 * SUM(CASE WHEN DaysOverdue <= 0 THEN 1 ELSE 0 END) / COUNT(*) AS GoodPct,
        100.0 * SUM(CASE WHEN DaysOverdue > 7 THEN 1 ELSE 0 END) / COUNT(*) AS ArrearsPct
    FROM Tiered
    GROUP BY DealerId, DeviceName
),
Ranked AS (
    SELECT *, ROW_NUMBER() OVER (PARTITION BY DealerId ORDER BY Units DESC) AS Rn
    FROM ByDevice
)
INSERT INTO DashboardDeviceStock (ScopeDealerId, ScopeAgentId, DeviceGroupId, DeviceName, Units, AvgValue, GoodPct, ArrearsPct)
SELECT DealerId, NULL, NULL, DeviceName, Units, AvgValue, GoodPct, ArrearsPct
FROM Ranked WHERE Rn <= 20;
GO

-- ===== 5. Commission scalars (CommissionReceived / PaidToAgents / Outstanding) =====
;WITH ValidPayments AS (
    SELECT COALESCE(op.AccountNoBigint, kp.AccountNoBigint) AS AccountNo, kp.AmountValue
    FROM KosePayments kp
    LEFT JOIN OrphanedPayments op ON op.MpesaCode = kp.MpesaCode
),
PaymentTotals AS (
    SELECT AccountNo, SUM(AmountValue) AS TotalPaid FROM ValidPayments GROUP BY AccountNo
),
AccountCommission AS (
    SELECT
        dl.DealerId,
        ci.AssignedAgentId,
        ci.ContractID,
        ISNULL(pt.TotalPaid, 0) AS TotalPaid,
        ISNULL(ci.BuyingPrice, 0) AS BuyingPrice,
        (ci.Deposit * 0.50)
            + (CASE WHEN DATEDIFF(DAY, ci.StartDate, GETDATE()) >= 90 THEN ci.Deposit * 0.25 ELSE 0 END) AS AgentGrossCommission,
        ISNULL(pt.TotalPaid, 0)
            - (ci.Deposit + ci.Daily * DaysAccrued.Days + ci.Weekly * (DaysAccrued.Days / 7.0) + ci.Monthly * (DaysAccrued.Days / 30.0)) AS Arrears
    FROM Contract_Info ci
    INNER JOIN Devices d ON d.Id = ci.ID
    INNER JOIN Dealers dl ON dl.DealerReference = d.DeviceGroupId
    LEFT JOIN PaymentTotals pt ON pt.AccountNo = ci.ID
    CROSS APPLY (
        SELECT CASE WHEN DATEDIFF(DAY, ci.StartDate, GETDATE()) < CAST(ci.Term_in_Months * 30 AS INT)
                    THEN DATEDIFF(DAY, ci.StartDate, GETDATE())
                    ELSE CAST(ci.Term_in_Months * 30 AS INT) END AS Days
    ) DaysAccrued
    WHERE ci.StartDate IS NOT NULL AND ci.AssignedAgentId IS NOT NULL
),
WithDealerCommission AS (
    SELECT *,
        CASE WHEN (TotalPaid - BuyingPrice - AgentGrossCommission) > 0
             THEN (TotalPaid - BuyingPrice - AgentGrossCommission) * 0.30
             ELSE 0 END AS DealerCommissionEarned
    FROM AccountCommission
),
AgentPaymentsAgg AS (
    SELECT ContractId, SUM(ISNULL(AmountPaid, 0)) AS TotalAgentPaid
    FROM AgentCommissionPayments
    GROUP BY ContractId
),
PerAgent AS (
    SELECT
        wdc.DealerId, wdc.AssignedAgentId,
        SUM(wdc.AgentGrossCommission) - SUM(CASE WHEN wdc.Arrears < 0 THEN -wdc.Arrears ELSE 0 END) AS NetCommission,
        SUM(ISNULL(ap.TotalAgentPaid, 0)) AS Paid
    FROM WithDealerCommission wdc
    LEFT JOIN AgentPaymentsAgg ap ON ap.ContractId = wdc.ContractID
    GROUP BY wdc.DealerId, wdc.AssignedAgentId
),
PerDealerFromAgents AS (
    SELECT DealerId, SUM(Paid) AS CommissionPaidToAgents, SUM(NetCommission - Paid) AS CommissionOutstanding
    FROM PerAgent
    GROUP BY DealerId
),
PerDealerOwnCommission AS (
    SELECT DealerId, SUM(DealerCommissionEarned) AS CommissionReceived
    FROM WithDealerCommission
    GROUP BY DealerId
),
Commission AS (
    SELECT
        o.DealerId,
        o.CommissionReceived,
        ISNULL(a.CommissionPaidToAgents, 0) AS CommissionPaidToAgents,
        ISNULL(a.CommissionOutstanding, 0) AS CommissionOutstanding
    FROM PerDealerOwnCommission o
    LEFT JOIN PerDealerFromAgents a ON a.DealerId = o.DealerId
)
MERGE DashboardSnapshot AS target
USING Commission AS source
    ON target.DealerId = source.DealerId AND target.AgentId IS NULL
WHEN MATCHED THEN
    UPDATE SET
        CommissionReceived = source.CommissionReceived,
        CommissionPaidToAgents = source.CommissionPaidToAgents,
        CommissionOutstanding = source.CommissionOutstanding,
        RefreshedAtUtc = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
    INSERT (DealerId, AgentId, CommissionReceived, CommissionPaidToAgents, CommissionOutstanding, RefreshedAtUtc)
    VALUES (source.DealerId, NULL, source.CommissionReceived, source.CommissionPaidToAgents, source.CommissionOutstanding, SYSUTCDATETIME());
GO

-- ===== 6. Agent commission list (Dealer-only, full delete+replace) =====
DELETE FROM DashboardPerformanceEntry WHERE EntryType = 'AgentCommission';

;WITH ValidPayments AS (
    SELECT COALESCE(op.AccountNoBigint, kp.AccountNoBigint) AS AccountNo, kp.AmountValue
    FROM KosePayments kp
    LEFT JOIN OrphanedPayments op ON op.MpesaCode = kp.MpesaCode
),
PaymentTotals AS (
    SELECT AccountNo, SUM(AmountValue) AS TotalPaid FROM ValidPayments GROUP BY AccountNo
),
AccountCommission AS (
    SELECT
        dl.DealerId, ci.AssignedAgentId, ci.ContractID,
        (ci.Deposit * 0.50)
            + (CASE WHEN DATEDIFF(DAY, ci.StartDate, GETDATE()) >= 90 THEN ci.Deposit * 0.25 ELSE 0 END) AS AgentGrossCommission,
        ISNULL(pt.TotalPaid, 0)
            - (ci.Deposit + ci.Daily * DaysAccrued.Days + ci.Weekly * (DaysAccrued.Days / 7.0) + ci.Monthly * (DaysAccrued.Days / 30.0)) AS Arrears
    FROM Contract_Info ci
    INNER JOIN Devices d ON d.Id = ci.ID
    INNER JOIN Dealers dl ON dl.DealerReference = d.DeviceGroupId
    LEFT JOIN PaymentTotals pt ON pt.AccountNo = ci.ID
    CROSS APPLY (
        SELECT CASE WHEN DATEDIFF(DAY, ci.StartDate, GETDATE()) < CAST(ci.Term_in_Months * 30 AS INT)
                    THEN DATEDIFF(DAY, ci.StartDate, GETDATE())
                    ELSE CAST(ci.Term_in_Months * 30 AS INT) END AS Days
    ) DaysAccrued
    WHERE ci.StartDate IS NOT NULL AND ci.AssignedAgentId IS NOT NULL
),
AgentPaymentsAgg AS (
    SELECT ContractId, SUM(ISNULL(AmountPaid, 0)) AS TotalAgentPaid
    FROM AgentCommissionPayments
    GROUP BY ContractId
),
PerAgent AS (
    SELECT
        ac.DealerId, ac.AssignedAgentId AS AgentId,
        u.[Name] + ' ' + u.[LastName] AS AgentName,
        COUNT(*) AS Accounts,
        SUM(ac.AgentGrossCommission) - SUM(CASE WHEN ac.Arrears < 0 THEN -ac.Arrears ELSE 0 END) AS NetCommission,
        SUM(ISNULL(ap.TotalAgentPaid, 0)) AS Paid
    FROM AccountCommission ac
    INNER JOIN Users u ON u.UserId = ac.AssignedAgentId
    LEFT JOIN AgentPaymentsAgg ap ON ap.ContractId = ac.ContractID
    GROUP BY ac.DealerId, ac.AssignedAgentId, u.[Name], u.[LastName]
),
Ranked AS (
    SELECT *, ROW_NUMBER() OVER (PARTITION BY DealerId ORDER BY (NetCommission - Paid) DESC) AS Rn
    FROM PerAgent
)
INSERT INTO DashboardPerformanceEntry
    (ScopeDealerId, ScopeAgentId, EntryType, Rank, SubjectId, SubjectName, ParentName, Accounts, ActivePct, Revenue, CommissionPaid, CommissionDue, PctOfTarget)
SELECT DealerId, NULL, 'AgentCommission', Rn, AgentId, AgentName, NULL, Accounts, 0, NULL, Paid, NetCommission, 0
FROM Ranked WHERE Rn <= 20;
GO

-- ===== 7. Sanity check: see everything that got populated =====
SELECT 'Snapshot' AS Section, DealerId, RevenueThisMonth, RevenueGrowthPct, TotalAccounts, NewThisMonth,
       PortfolioGoodPct, PortfolioSlowPct, PortfolioArrearsPct, PortfolioNonPayingPct, ArrearsTotal,
       CommissionReceived, CommissionPaidToAgents, CommissionOutstanding, RefreshedAtUtc
FROM DashboardSnapshot ORDER BY DealerId;

SELECT 'CompletedContract' AS Section, * FROM DashboardCompletedContract ORDER BY ScopeDealerId, Status, CompletedDate DESC;

SELECT 'DeviceStock' AS Section, * FROM DashboardDeviceStock ORDER BY ScopeDealerId, Units DESC;

SELECT 'AgentCommission' AS Section, * FROM DashboardPerformanceEntry WHERE EntryType = 'AgentCommission' ORDER BY ScopeDealerId, Rank;
