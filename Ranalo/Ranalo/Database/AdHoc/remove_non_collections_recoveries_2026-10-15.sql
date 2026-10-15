-- One-off, 2026-10-15: remove 17 recoveries sent from Lock check that aren't
-- collections cases. Lock check now uses the Collections rule (in arrears,
-- no payment for 90 days), with a restructured account judged on its
-- restructured plan (daily amount agreed x days since, less what's been paid
-- since):
--    9 keeping to their restructure (paid in the last 0-9 days):
--      20 Jackie 4314098, 31 Morris 6985486, 36 Alpha 7005299,
--      43 Peter 7044739, 55 Elijah 7114723, 78 Jackline 7623782,
--      79 Felix 7623909, 95 Brian 7959738, 117 Randy 8317760
--    6 paid within the last 90 days:
--      34 Samuel 6995575, 70 Caroline 7270014, 80 Daniel 7657104,
--      100 Daniel 8012218, 107 Felisters 8053523, 108 John 8054493
--    2 not in arrears: 17 Arvinder 2519739, 28 Oscar 6377983
--
-- Kept in Recovery (breached their restructure: behind on it and no payment
-- for 90+ days): 29 Daniel 6621258, 38 Lorin 7024451, 51 Haward 7097536,
-- 73 Munguti 7310470, 86 Stephen 7759647, 109 Geoffrey 8069165, 121 Ephy 8550709.
--
-- NOT included: 124 Sammy 8696673 -- keeping to his restructure, but already
-- sent to Transsion with lock on switch-on; cancel that at Transsion first.
--
-- Each removed recovery has only its "Added" log line -- nothing was sent to
-- Knox/Transsion and the phones were never touched -- so removing it, its log
-- and the Lock check "sent to Recovery" entry returns the account to normal.
-- All or nothing: any failed check rolls everything back.
SET NOCOUNT ON; SET XACT_ABORT ON; SET QUOTED_IDENTIFIER ON; SET ANSI_NULLS ON; SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON; SET ARITHABORT ON; SET CONCAT_NULL_YIELDS_NULL ON;
BEGIN TRANSACTION;

SELECT r.Id AS RecoveryId, r.AccountNo
INTO #remove
FROM LockRecoveries r
WHERE r.Id IN (17, 20, 28, 31, 34, 36, 43, 55, 70, 78, 79, 80, 95, 100, 107, 108, 117)
  AND r.Status = 'Investigating'
  AND NOT EXISTS (SELECT 1 FROM LockRecoveryAttempts x WHERE x.RecoveryId = r.Id AND x.Step <> 'Added');

IF (SELECT COUNT(*) FROM #remove) <> 17
    THROW 50001, 'Expected 17 untouched recoveries (one has moved on since) -- nothing changed.', 1;

DELETE a FROM LockRecoveryAttempts a INNER JOIN #remove x ON x.RecoveryId = a.RecoveryId;
DELETE r FROM LockRecoveries r INNER JOIN #remove x ON x.RecoveryId = r.Id;
IF @@ROWCOUNT <> 17 THROW 50002, 'Did not remove exactly 17 recoveries -- rolled back.', 1;
DELETE l FROM LockCheckActions l INNER JOIN #remove x ON x.AccountNo = l.AccountNo WHERE l.Action = 'ToRecovery';

COMMIT TRANSACTION;

-- Check: should be 0.
SELECT COUNT(*) AS RecoveriesLeft FROM LockRecoveries
WHERE Id IN (17, 20, 28, 31, 34, 36, 43, 55, 70, 78, 79, 80, 95, 100, 107, 108, 117);
