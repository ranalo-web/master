-- One-off, 2026-10-09: end contracts 691, 694 and 695 as dealer fraud (Newway).
--
-- 691 Millan Makeba (8584527, order 18774) and 695 Harrison Kimeu (8689126,
-- order 18798): deposit only, no device ever enrolled (not in Nuovo), the two
-- orders share one ID-card back photo, Millan's IMEI (11 digits) and next of
-- kin number are invalid, and Harrison's IMEI is enrolled on another account
-- (8680519). 694 Kennedy Musyoki (8621993, order 18782): deposit only, no
-- device, invalid IMEI; the same ID already has a paying contract (300).
--
-- PREREQUISITE: collections cases 329, 330 and 332 returned in Collections Admin.
-- All or nothing: any failed check rolls everything back.
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF EXISTS (SELECT 1 FROM CollectionCases WHERE Id IN (329, 330, 332) AND Status = 'Open')
    THROW 50001, 'Return collections cases 329, 330 and 332 first.', 1;

IF EXISTS (SELECT 1 FROM Devices WHERE Id IN (8584527, 8621993, 8689126))
    THROW 50002, 'A device now exists on 8584527, 8621993 or 8689126 -- check before ending.', 1;

UPDATE Contract_Info SET EndDate = GETDATE()
WHERE ContractID = 691 AND ID = 8584527 AND EndDate IS NULL;
IF @@ROWCOUNT <> 1 THROW 50003, 'Contract 691 not found open on 8584527.', 1;

UPDATE Contract_Info SET EndDate = GETDATE()
WHERE ContractID = 694 AND ID = 8621993 AND EndDate IS NULL;
IF @@ROWCOUNT <> 1 THROW 50006, 'Contract 694 not found open on 8621993.', 1;

UPDATE Contract_Info SET EndDate = GETDATE()
WHERE ContractID = 695 AND ID = 8689126 AND EndDate IS NULL;
IF @@ROWCOUNT <> 1 THROW 50004, 'Contract 695 not found open on 8689126.', 1;

COMMIT TRANSACTION;

-- Check the result
SELECT ContractID, ID, First_Name, StartDate, EndDate FROM Contract_Info WHERE ContractID IN (691, 694, 695);
SELECT Id, AccountNo, ContractId, Status, ClosedAt FROM CollectionCases WHERE Id IN (329, 330, 332);
