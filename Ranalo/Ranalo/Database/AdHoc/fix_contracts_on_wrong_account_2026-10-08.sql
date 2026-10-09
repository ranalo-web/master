-- One-off fix, 2026-10-08: contracts created on the wrong account.
--
-- Cause: ScheduledTaskCreateContractOrders put each contract on the account
-- typed on the deposit's M-Pesa payment, ignoring Assign Payments and never
-- checking a device existed. Fixed in code in commit 022e082.
--
-- PREREQUISITE: collections cases 324, 333, 347 returned in Collections Admin.
-- All or nothing: any failed check rolls everything back.
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF EXISTS (SELECT 1 FROM CollectionCases WHERE Id IN (324, 333, 347) AND Status = 'Open')
    THROW 50001, 'Return collections cases 324, 333, 347 first.', 1;

-- 1. Order 19654 (Fidelian): move contract 780 from national ID 37426422 to Nuovo account 9789208.
IF EXISTS (SELECT 1 FROM Contract_Info WHERE ID = 9789208 AND EndDate IS NULL)
    THROW 50002, '9789208 already has an open contract.', 1;
UPDATE Contract_Info SET ID = 9789208
WHERE ContractID = 780 AND ID = 37426422 AND EndDate IS NULL;
IF @@ROWCOUNT <> 1 THROW 50003, 'Contract 780 not found open on 37426422.', 1;

-- 2. Stray 150 paid to the national ID on 19 Jun -> 9789208, as Assign Payments would (user 13).
IF NOT EXISTS (SELECT 1 FROM OrphanedPayments WHERE MpesaCode = 'UFJA28NT6S')
    INSERT INTO OrphanedPayments (Id, OrphanedAccountNo, MpesaCode, AccountNo, DateCreated, AssignedByUserId)
    VALUES (NEWID(), '37426422', 'UFJA28NT6S', '9789208', GETDATE(), 13);

-- 3. End leftover duplicates whose real contract already exists:
--    786 Douglas on 23115233 (real: 789 on 9829489)
--    755 Benson on account 0  (real: 758 on 2565870)
UPDATE Contract_Info SET EndDate = GETDATE()
WHERE ContractID IN (786, 755) AND EndDate IS NULL;
IF @@ROWCOUNT <> 2 THROW 50005, 'Contracts 786/755 not both open.', 1;

COMMIT TRANSACTION;

-- Check the result
SELECT ContractID, ID, First_Name, StartDate, EndDate FROM Contract_Info WHERE ContractID IN (780, 786, 755, 789, 758);
SELECT OrphanedAccountNo, MpesaCode, AccountNo, DateCreated FROM OrphanedPayments WHERE MpesaCode IN ('UFJJD8ED8V', 'UFJA28NT6S');
