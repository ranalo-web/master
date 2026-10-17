-- One-off, 2026-10-16: contracts loaded at go-live (10-12 Nov 2025) were
-- given the load date as StartDate instead of the sale date. A contract
-- begins with the first payment on the account, so:
--
-- 1. StartDate = first payment on the account, for the 151 go-live contracts
--    whose first payment is within 30 days of their Nuovo enrolment (open and
--    closed). The status report and lock jobs already count from the first
--    payment; this brings Collections, Lock check, the dashboard and fraud
--    checks in line with them.
--    Left alone for a manual check -- payments that predate the phone:
--      3440802 Oule (paid from 2024-04, enrolled 2025-08),
--      4845922 Ernest (enrolled 2024-06, first payment 2025-03),
--      5150918 Patty (paid from 2024-12, enrolled 2024-08),
--      7004991 Eric (one stray 2022 payment, enrolled 2025-05).
--
-- 2. A restructure from today at the contract's original daily amount for the
--    13 that are now behind but still paying (paid in the last 90 days, not a
--    Collections or Recovery case, no restructure yet), so they aren't
--    locked as long as they keep paying. Existing restructures keep their
--    agreed rate; Collections and Recovery cases are not touched.
--
-- Old values are kept in AdHoc_StartDateFix_20261016 and
-- AdHoc_RestructureFix_20261016 (see the undo at the bottom).
-- All or nothing: any failed check rolls everything back.
SET NOCOUNT ON; SET XACT_ABORT ON; SET QUOTED_IDENTIFIER ON; SET ANSI_NULLS ON; SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON; SET ARITHABORT ON; SET CONCAT_NULL_YIELDS_NULL ON;

IF OBJECT_ID('AdHoc_StartDateFix_20261016') IS NOT NULL
    THROW 50001, 'Already run (AdHoc_StartDateFix_20261016 exists) -- nothing changed.', 1;

BEGIN TRANSACTION;

-- 1. Start dates
SELECT ci.ContractID, ci.ID AS AccountNo, ci.StartDate AS OldStartDate, fp.FirstPay AS NewStartDate
INTO AdHoc_StartDateFix_20261016
FROM Contract_Info ci
INNER JOIN Devices d ON d.Id = ci.ID
CROSS APPLY (SELECT MIN(k.PaymentDateValue) AS FirstPay FROM KosePayments k WHERE k.AccountNoBigint = ci.ID) fp
WHERE ci.StartDate >= DATEFROMPARTS(2025, 11, 10) AND ci.StartDate < DATEFROMPARTS(2025, 11, 13)
  AND ci.PaymentAccount IS NULL
  AND fp.FirstPay < DATEADD(DAY, -14, ci.StartDate)
  AND ABS(DATEDIFF(DAY, fp.FirstPay, TRY_CONVERT(date, LEFT(d.EnrolledOn, 8), 5))) <= 30
  AND ci.ID NOT IN (3440802, 4845922, 5150918, 7004991);

IF (SELECT COUNT(*) FROM AdHoc_StartDateFix_20261016) <> 151
    THROW 50002, 'Expected 151 go-live contracts -- nothing changed.', 1;

UPDATE ci SET ci.StartDate = f.NewStartDate
FROM Contract_Info ci
INNER JOIN AdHoc_StartDateFix_20261016 f ON f.ContractID = ci.ContractID;

-- 2. Restructures
SELECT ci.ID AS AccountNo, ci.Daily AS AmountRes, CAST(NULL AS INT) AS RestructuredId
INTO AdHoc_RestructureFix_20261016
FROM Contract_Info ci
WHERE ci.ID IN (7870175, 7292165, 7361066, 7023065, 7746907, 8053523, 8499170,
                7930673, 7385744, 7421622, 8054493, 8440271, 8021703)
  AND ci.EndDate IS NULL
  AND ci.Daily > 0
  AND NOT EXISTS (SELECT 1 FROM RestructuredRecords r WHERE r.AccountNo = ci.ID)
  AND NOT EXISTS (SELECT 1 FROM CollectionCases c WHERE c.AccountNo = ci.ID AND c.Status = 'Open')
  AND NOT EXISTS (SELECT 1 FROM LockRecoveries x WHERE x.AccountNo = CAST(ci.ID AS NVARCHAR(30)) AND x.Status <> 'Closed');

IF (SELECT COUNT(*) FROM AdHoc_RestructureFix_20261016) <> 13
    THROW 50003, 'Expected 13 accounts to restructure (one has changed since) -- nothing changed.', 1;

-- Same values as a restructure made on the Reports page.
INSERT INTO RestructuredRecords
    (AccountNo, Date_Agreed, Amount_Res, Days_Restructured, Total_Due_R, Total_Paid_R, Arrears_R, Auto_lock_date_pmt_R)
SELECT f.AccountNo, CAST(CAST(GETUTCDATE() AS DATE) AS DATETIME), f.AmountRes, 0, 0, 0, 0, DATEADD(HOUR, 24, GETUTCDATE())
FROM AdHoc_RestructureFix_20261016 f;

UPDATE f SET f.RestructuredId = r.Id
FROM AdHoc_RestructureFix_20261016 f
INNER JOIN RestructuredRecords r ON r.AccountNo = f.AccountNo;

COMMIT TRANSACTION;

SELECT COUNT(*) AS StartDatesFixed FROM AdHoc_StartDateFix_20261016;
SELECT f.AccountNo, ci.First_Name, f.AmountRes, f.RestructuredId
FROM AdHoc_RestructureFix_20261016 f INNER JOIN Contract_Info ci ON ci.ID = f.AccountNo AND ci.EndDate IS NULL;

-- Undo:
-- UPDATE ci SET ci.StartDate = f.OldStartDate FROM Contract_Info ci INNER JOIN AdHoc_StartDateFix_20261016 f ON f.ContractID = ci.ContractID;
-- DELETE r FROM RestructuredRecords r INNER JOIN AdHoc_RestructureFix_20261016 f ON f.RestructuredId = r.Id;
-- DROP TABLE AdHoc_StartDateFix_20261016; DROP TABLE AdHoc_RestructureFix_20261016;
