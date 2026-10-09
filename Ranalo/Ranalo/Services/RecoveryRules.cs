using Ranalo.Models;

namespace Ranalo.Services
{
    // Recovery rules (agreed with the business 2026-10-09). Kept free of I/O
    // so they can be unit tested; see Database/Recovery/001 for the tables.
    //
    // A recovery is a phone we're trying to get back under a lock -- e.g. a
    // Nuovo phone whose Nuovo lock was removed (the Newway fraud phones).
    // Recovery is separate from Fraud: Fraud finds the problem, Recovery
    // tries to get the phone back.
    //
    // Samsung -> Knox Guard, one admin step at a time, every call logged
    // with the provider's reply:
    //   1. Upload the IMEI through Veritech (gives the transaction id that
    //      Knox uses as the approveId), and check the upload.
    //   2. Approve the device in Knox with that id.
    //   3. Check it in Knox; lock it (locks when it next connects).
    //   4. Once the lock works, Move to Knox: Devices.LockGroup = 2 and an
    //      Enrolments row, so the daily lock jobs use Knox from then on.
    // A phone that can't be caught is marked Not catchable and stays with
    // its old locker. Other brands are tracked with notes only for now.
    public static class RecoveryRules
    {
        public const int KnoxLockGroup = 2;

        public static bool IsSamsung(string? make) => string.Equals(make?.Trim(), "samsung", StringComparison.OrdinalIgnoreCase);

        public static bool IsOpen(string status) =>
            status is not (RecoveryStatus.MovedToKnox or RecoveryStatus.NotCatchable or RecoveryStatus.Closed);

        public static IReadOnlySet<RecoveryAction> AllowedActions(LockRecovery r)
        {
            var actions = new HashSet<RecoveryAction> { RecoveryAction.Note };
            var samsung = IsSamsung(r.Make);
            var hasTransaction = !string.IsNullOrWhiteSpace(r.VeritechTransId);

            if (r.Status == RecoveryStatus.MovedToKnox)
            {
                // The daily jobs own it now; checking Knox is still useful.
                actions.Add(RecoveryAction.KnoxCheck);
                return actions;
            }

            if (!IsOpen(r.Status))
            {
                actions.Add(RecoveryAction.Reopen);
                return actions;
            }

            actions.Add(RecoveryAction.NotCatchable);
            actions.Add(RecoveryAction.Close);
            if (!samsung)
            {
                return actions;
            }

            actions.Add(RecoveryAction.Upload);
            actions.Add(RecoveryAction.KnoxCheck);
            if (hasTransaction)
            {
                actions.Add(RecoveryAction.UploadStatus);
                actions.Add(RecoveryAction.Approve);
            }
            if (r.Status is RecoveryStatus.Approved or RecoveryStatus.LockSent)
            {
                actions.Add(RecoveryAction.Lock);
                actions.Add(RecoveryAction.Unlock);
                actions.Add(RecoveryAction.MoveToKnox);
            }
            return actions;
        }

        // Status after a provider call. A failed call never moves it.
        public static string StatusAfter(RecoveryAction action, bool success, string current)
        {
            if (!success) return current;
            return action switch
            {
                RecoveryAction.Upload => RecoveryStatus.UploadSent,
                RecoveryAction.Approve => RecoveryStatus.Approved,
                RecoveryAction.Lock => RecoveryStatus.LockSent,
                RecoveryAction.Unlock when current == RecoveryStatus.LockSent => RecoveryStatus.Approved,
                RecoveryAction.MoveToKnox => RecoveryStatus.MovedToKnox,
                RecoveryAction.NotCatchable => RecoveryStatus.NotCatchable,
                RecoveryAction.Close => RecoveryStatus.Closed,
                RecoveryAction.Reopen => RecoveryStatus.Investigating,
                _ => current,
            };
        }

        // Moving to Knox replaces the phone's lock provider, so only when
        // nothing else already enrols the account and the phone isn't
        // already on another app-managed locker.
        public static string? CannotMoveToKnox(LockRecovery r, bool accountHasEnrolment)
        {
            if (!AllowedActions(r).Contains(RecoveryAction.MoveToKnox))
                return "Approve the phone in Knox first.";
            if (string.IsNullOrWhiteSpace(r.VeritechTransId))
                return "There is no Veritech transaction id to give Knox.";
            if (accountHasEnrolment)
                return $"Account {r.AccountNo} already has an enrolment; it needs sorting out by hand first.";
            if (r.CurrentLockGroup is 3)
                return "The phone is on Transsion PayTrigger.";
            return null;
        }

        public static string LockGroupName(int? lockGroup) => lockGroup switch
        {
            2 => "Knox",
            3 => "Transsion",
            _ => "Nuovo",
        };
    }
}
