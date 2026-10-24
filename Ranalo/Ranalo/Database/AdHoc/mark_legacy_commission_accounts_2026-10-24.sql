-- Marks every legacy contract as earning no commission (decided 24 Oct 2026):
-- every contract that started before 1 Oct 2026, EXCEPT Mali Mali's -- all of
-- Mali Mali's accounts keep earning, for the dealer and the agent.
-- Commission on legacy accounts was paid under the old calculators, so no
-- more is due. Run Database/Commissions/005 first.
--
-- Step 1 (read only): check the Mali Mali dealer row(s) and the counts.
-- Step 2: the INSERT. Safe to re-run: already-marked contracts are skipped,
-- and a contract an admin has since changed is never touched again
-- (it has a log entry).

DECLARE @Cutoff DATE = '2026-10-01';

-- Step 1: the dealer(s) treated as Mali Mali. Expect exactly one row.
SELECT DealerId, DealerReference, CompanyName
FROM Dealers
WHERE CompanyName LIKE '%mali%mali%';

-- Contracts per dealer: how many become legacy, how many keep earning.
SELECT ISNULL(dl.CompanyName, '(no dealer)') AS Dealer,
       SUM(CASE WHEN ci.StartDate < @Cutoff AND ISNULL(dl.CompanyName, '') NOT LIKE '%mali%mali%' THEN 1 ELSE 0 END) AS BecomeLegacy,
       SUM(CASE WHEN ci.StartDate >= @Cutoff OR dl.CompanyName LIKE '%mali%mali%' THEN 1 ELSE 0 END) AS KeepEarning
FROM Contract_Info ci
LEFT JOIN Devices d ON d.Id = ci.ID
LEFT JOIN Dealers dl ON dl.DealerReference = COALESCE(ci.DealerReference, d.DeviceGroupId)
WHERE ci.StartDate IS NOT NULL
GROUP BY dl.CompanyName
ORDER BY dl.CompanyName;

-- Step 2: mark them.
BEGIN TRANSACTION;

INSERT INTO CommissionExclusions (ContractId, Reason, RecoverArrears, Notes, SetByUserId, SetAtUtc)
OUTPUT INSERTED.ContractId, 'Qualifies', 'NoCommission', INSERTED.Reason, INSERTED.Notes, NULL, INSERTED.SetAtUtc
INTO CommissionExclusionLog (ContractId, OldStatus, NewStatus, Reason, Notes, ChangedByUserId, ChangedAtUtc)
SELECT DISTINCT ci.ContractID, 'Legacy', 0,
       'Sold before the 1 Oct 2026 commission rules; paid under the old calculators', NULL, SYSUTCDATETIME()
FROM Contract_Info ci
LEFT JOIN Devices d ON d.Id = ci.ID
LEFT JOIN Dealers dl ON dl.DealerReference = COALESCE(ci.DealerReference, d.DeviceGroupId)
WHERE ci.StartDate IS NOT NULL
  AND ci.StartDate < @Cutoff
  AND ISNULL(dl.CompanyName, '') NOT LIKE '%mali%mali%'
  AND NOT EXISTS (SELECT 1 FROM CommissionExclusions x WHERE x.ContractId = ci.ContractID)
  AND NOT EXISTS (SELECT 1 FROM CommissionExclusionLog l WHERE l.ContractId = ci.ContractID);

SELECT @@ROWCOUNT AS ContractsMarkedLegacy;

COMMIT TRANSACTION;
