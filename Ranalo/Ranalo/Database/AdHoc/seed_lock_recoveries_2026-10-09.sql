-- Adds the first phones to Recovery (run after Database/Recovery/001).
-- The three Newway fraud phones (Samsung SM-A065F on Nuovo, unlocked past
-- their 28/05/2026 lock date) and the Galaxy A05 on 8143984 -- the same
-- four IMEIs sent to Veripay on 2026-10-09 (scripts/imeis_example.csv).
-- Details are copied from Devices; skips any account already open there.

-- Needed for the filtered index on LockRecoveries (sqlcmd defaults it OFF).
SET QUOTED_IDENTIFIER ON;

INSERT INTO LockRecoveries (AccountNo, Imei, Make, Model, CustomerName, DealerName, OriginalLockGroup, Reason, Status, Notes)
SELECT d.Id, d.ImeiNo, d.Make, d.Model,
       COALESCE(ci.First_Name, d.Name, d.CustomerName),
       dl.CompanyName, d.LockGroup, s.Reason, 'Investigating', s.Notes
FROM (VALUES
        (CAST(8511324 AS BIGINT), 'Fraud', N'Denis Kilonzo (Newway). Nuovo lock removed; IMEI sent to Veripay 2026-10-09.'),
        (8583526, 'Fraud', N'Margaret Muhanji (Newway). Nuovo lock removed; IMEI sent to Veripay 2026-10-09.'),
        (8680519, 'Fraud', N'Harrison''s phone (Newway), IMEI also on orders 18798/18804/18805. IMEI sent to Veripay 2026-10-09.'),
        (8143984, 'Other', N'Galaxy A05. IMEI sent to Veripay 2026-10-09.')
     ) s (AccountNo, Reason, Notes)
INNER JOIN Devices d ON d.Id = s.AccountNo
LEFT JOIN Contract_Info ci ON ci.ID = d.Id AND ci.EndDate IS NULL
LEFT JOIN Dealers dl ON dl.DealerReference = d.DeviceGroupId
WHERE NOT EXISTS (SELECT 1 FROM LockRecoveries r
                  WHERE r.AccountNo = s.AccountNo AND r.Status NOT IN ('NotCatchable', 'Closed', 'MovedToKnox'));

SELECT Id, AccountNo, Imei, Make, Model, CustomerName, DealerName, Reason, Status FROM LockRecoveries ORDER BY Id;
