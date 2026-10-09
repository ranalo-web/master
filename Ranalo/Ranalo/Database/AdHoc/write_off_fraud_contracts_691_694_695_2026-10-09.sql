-- One-off, 2026-10-09: write off contracts 691, 694 and 695 as Fraud (Newway
-- ghost customers; ended by end_fraud_contracts_691_694_695_2026-10-09.sql)
-- and note the reason on their returned collections cases.
--
-- Rows go in as Pending, like every write-off: approve them on the
-- Write-offs page. Fraud write-offs are never reinstated by a payment.
-- Run after the app with the "Fraud" reason is deployed, or the page shows
-- them as "360 days no payment".
--
-- All or nothing: any failed check rolls everything back.
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF EXISTS (SELECT 1 FROM Contract_Info WHERE ContractID IN (691, 694, 695) AND EndDate IS NULL)
    THROW 50001, 'End contracts 691, 694 and 695 first.', 1;

IF EXISTS (SELECT 1 FROM WriteOffs WHERE ContractId IN (691, 694, 695))
    THROW 50002, 'A write-off already exists for 691, 694 or 695.', 1;

INSERT INTO WriteOffs
    (ContractId, AccountNo, Reason, WrittenOffDate, LastPaymentDate,
     ContractValue, TotalPaid, OutstandingBalance, Status, HoldReason)
SELECT c.ContractID, CAST(c.ID AS BIGINT), 'Fraud', CAST(GETDATE() AS DATE), CAST(p.LastPay AS DATE),
       c.ContractValue, ISNULL(p.Total, 0), c.ContractValue - ISNULL(p.Total, 0), 'Pending',
       N'Dealer fraud (Newway): ghost customer, no device ever enrolled'
FROM (SELECT ContractID, ID,
             CAST(Deposit + Daily * 30 * Term_in_Months + Weekly * (30.0 / 7.0) * Term_in_Months
                  + Monthly * Term_in_Months AS DECIMAL(18,2)) AS ContractValue
      FROM Contract_Info WHERE ContractID IN (691, 694, 695)) c
OUTER APPLY (SELECT SUM(kp.AmountValue) AS Total, MAX(kp.PaymentDateValue) AS LastPay
             FROM KosePayments kp LEFT JOIN OrphanedPayments op ON op.MpesaCode = kp.MpesaCode
             WHERE COALESCE(op.AccountNoBigint, kp.AccountNoBigint) = c.ID) p;
IF @@ROWCOUNT <> 3 THROW 50003, 'Expected 3 write-offs.', 1;

UPDATE CollectionCases
SET Notes = N'Closed as dealer fraud (Newway ghost customer) 2026-10-09'
WHERE Id IN (329, 330, 332);

COMMIT TRANSACTION;

-- Check the result
SELECT ContractId, AccountNo, Reason, WrittenOffDate, ContractValue, TotalPaid, OutstandingBalance, Status, HoldReason
FROM WriteOffs WHERE ContractId IN (691, 694, 695);
SELECT Id, AccountNo, Status, Notes FROM CollectionCases WHERE Id IN (329, 330, 332);
