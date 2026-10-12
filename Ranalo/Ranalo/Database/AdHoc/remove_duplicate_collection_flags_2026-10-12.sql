-- One-off, 2026-10-12: remove repeat "can't reach" flags.
--
-- The My Collections page took ~60s to reload after "Flag for admin", so a
-- collector clicked again and again: 43 flags, only 6 different ones. For
-- each case, collector and note, this keeps the latest open flag and
-- deletes the open copies before it. Flags with different notes, and flags
-- an admin has already cleared, are left alone.
--
-- Expected: 37 deleted, 6 left (one each on cases 136, 144, 281, 287, 369,
-- 374). The app now stops repeats itself, so this shouldn't be needed again.
--
-- All or nothing: if the count isn't what's expected, nothing changes.
SET XACT_ABORT ON;
BEGIN TRANSACTION;

;WITH Ranked AS (
    SELECT Id,
           ROW_NUMBER() OVER (PARTITION BY CaseId, CollectorUserId, LTRIM(RTRIM(Note))
                              ORDER BY CreatedAt DESC, Id DESC) AS rn
    FROM CollectionFlags
    WHERE ResolvedAt IS NULL
)
DELETE f
FROM CollectionFlags f
INNER JOIN Ranked r ON r.Id = f.Id
WHERE r.rn > 1;

DECLARE @Deleted INT = @@ROWCOUNT;
PRINT CONCAT('Deleted ', @Deleted, ' repeat flags.');

IF @Deleted > 37
    THROW 50001, 'More repeats than expected (37) -- new flags since 2026-10-12? Nothing was changed; check before re-running.', 1;

COMMIT;

-- What's left: one flag per case, collector and note.
SELECT f.Id, f.CaseId, c.AccountNo, f.CollectorUserId, f.Note, f.CreatedAt
FROM CollectionFlags f
INNER JOIN CollectionCases c ON c.Id = f.CaseId
WHERE f.ResolvedAt IS NULL
ORDER BY f.CaseId;
