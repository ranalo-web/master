using Ranalo.Models;
using Ranalo.Services.DeviceLock;

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
    //
    // itel/TECNO/Infinix -> Transsion PayTrigger (agreed 2026-10-14). These
    // phones are assumed hacked or flashed, so Nuovo is left on them and
    // Transsion is added as the lock that's relied on:
    //   1. Send to Transsion: pre-enrol the IMEI, locked as soon as it's
    //      active.
    //   2. Check in Transsion; once Transsion says it's active (lockState
    //      3000) it's moved to Transsion straight away: Devices.LockGroup = 3
    //      and an Enrolments row, so the daily lock jobs manage it from then
    //      on. (Move to Transsion by hand if that didn't happen.)
    //   3. Lock now / Unlock through updateRepayInfo (lock date now / a day
    //      ahead), once it's active -- Transsion refuses a lock date before
    //      activation (code 20001), and a sent phone locks on activation.
    // A phone that can't be caught is marked Not catchable and stays with
    // its old locker. Other brands are tracked with notes only.
    public static class RecoveryRules
    {
        public const int KnoxLockGroup = 2;
        public const int TranssionLockGroup = 3;

        // PayTrigger lockState: 3000 active, 5000 removed.
        public const int TranssionActiveState = 3000;
        public const int TranssionRemovedState = 5000;

        // Some Knox devices have no make, only a Samsung model ("SM-A165F/DS",
        // account 41109528).
        public static bool IsSamsung(string? make, string? model = null) =>
            string.Equals(make?.Trim(), "samsung", StringComparison.OrdinalIgnoreCase)
            || (string.IsNullOrWhiteSpace(make) && model?.Trim().StartsWith("SM-", StringComparison.OrdinalIgnoreCase) == true);

        // Nuovo's makes come in any case ("INFINIX", "ITEL", "TECNO").
        public static bool IsTranssion(string? make) => TranssionBrand(make) != null;

        // The brand as the rest of the app spells it (DeviceLockRules).
        public static string? TranssionBrand(string? make) =>
            make == null ? null : new[] { DeviceLockRules.Itel, DeviceLockRules.Tecno, DeviceLockRules.Infinix }
                .FirstOrDefault(b => string.Equals(b, make.Trim(), StringComparison.OrdinalIgnoreCase));

        public static bool IsOpen(string status) =>
            !RecoveryStatus.IsMoved(status) && status is not (RecoveryStatus.NotCatchable or RecoveryStatus.Closed);

        public static IReadOnlySet<RecoveryAction> AllowedActions(LockRecovery r)
        {
            var actions = new HashSet<RecoveryAction> { RecoveryAction.Note };
            var samsung = IsSamsung(r.Make, r.Model);
            var hasTransaction = !string.IsNullOrWhiteSpace(r.VeritechTransId);

            if (r.Status == RecoveryStatus.MovedToKnox)
            {
                // The daily jobs own it now; checking Knox is still useful.
                actions.Add(RecoveryAction.KnoxCheck);
                return actions;
            }

            if (r.Status == RecoveryStatus.MovedToTranssion)
            {
                actions.Add(RecoveryAction.TranssionCheck);
                return actions;
            }

            if (!IsOpen(r.Status))
            {
                actions.Add(RecoveryAction.Reopen);
                return actions;
            }

            actions.Add(RecoveryAction.NotCatchable);
            actions.Add(RecoveryAction.Close);
            if (IsTranssion(r.Make))
            {
                actions.Add(RecoveryAction.TranssionSend);
                actions.Add(RecoveryAction.TranssionCheck);
                // Unlike Knox, Transsion refuses a lock date until the phone
                // has activated ("the device is not activated", code 20001,
                // 2026-10-14). Sent phones lock on activation anyway.
                if (r.Status is RecoveryStatus.Approved or RecoveryStatus.LockSent)
                {
                    actions.Add(RecoveryAction.Lock);
                    actions.Add(RecoveryAction.Unlock);
                    actions.Add(RecoveryAction.MoveToTranssion);
                }
                return actions;
            }
            if (!samsung)
            {
                return actions;
            }

            // Already on Knox (enrolled there): lock and unlock it with its
            // enrolment's approve id straight away. The upload and approval
            // steps stay open in case Knox has lost it (41109528: approved
            // in March, no longer in Knox by 2026-10-15). It's already the
            // Knox phone, so no Move to Knox.
            var onKnox = r.CurrentLockGroup == KnoxLockGroup;

            actions.Add(RecoveryAction.Upload);
            actions.Add(RecoveryAction.KnoxCheck);
            if (hasTransaction)
            {
                actions.Add(RecoveryAction.UploadStatus);
                actions.Add(RecoveryAction.Approve);
            }
            if (r.Status is RecoveryStatus.Approved or RecoveryStatus.LockSent || (onKnox && hasTransaction))
            {
                actions.Add(RecoveryAction.Lock);
                actions.Add(RecoveryAction.Unlock);
            }
            if (r.Status is RecoveryStatus.Approved or RecoveryStatus.LockSent && !onKnox)
            {
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
                RecoveryAction.TranssionSend when current == RecoveryStatus.Investigating => RecoveryStatus.UploadSent,
                RecoveryAction.MoveToTranssion => RecoveryStatus.MovedToTranssion,
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

        // A Transsion check that finds the phone active makes a sent phone
        // "Active in Transsion"; it never moves a phone back.
        public static string StatusAfterTranssionCheck(string current, int? lockState) =>
            lockState == TranssionActiveState && current is RecoveryStatus.Investigating or RecoveryStatus.UploadSent
                ? RecoveryStatus.Approved
                : current;

        // Same conditions as Knox: only once Transsion has the phone active,
        // and nothing else already enrols the account.
        public static string? CannotMoveToTranssion(LockRecovery r, bool accountHasEnrolment)
        {
            if (!AllowedActions(r).Contains(RecoveryAction.MoveToTranssion))
                return "Check the phone in Transsion first: it must be active there.";
            if (accountHasEnrolment)
                return $"Account {r.AccountNo} already has an enrolment; it needs sorting out by hand first.";
            if (r.CurrentLockGroup is KnoxLockGroup)
                return "The phone is on Knox.";
            return null;
        }

        public static string TranssionStateName(int lockState) => lockState switch
        {
            0 => "unregistered",
            1000 => "registered",
            2000 => "ready to activate",
            TranssionActiveState => "active",
            TranssionRemovedState => "removed",
            _ => lockState.ToString(),
        };

        public static string LockGroupName(int? lockGroup) => lockGroup switch
        {
            2 => "Knox",
            3 => "Transsion",
            _ => "Nuovo",
        };
    }
}
