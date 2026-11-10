using Ranalo.SumsungKnox.Models;

namespace Ranalo.Services
{
    // What Knox itself says about a phone (2026-11-10). Our pages used to
    // show only what we had sent (a lock, a move); a phone could sit in
    // Knox as "Accepted" or "Activating" -- unable to be locked or messaged
    // -- while we showed it as enrolled, or be active in Knox while we sent
    // its locks to Nuovo. ScheduledTaskKnoxDevicesPull saves Knox's status on
    // Devices; the Knox check page lists where the two disagree.
    public static class KnoxStatusRules
    {
        public const string NotInKnox = "Not in Knox";

        public record KnoxDeviceState(string Imei, string? Imei2, string Status, DateTime? LastSeenUtc,
            DateTime? RelockUtc, bool Offline);

        public static KnoxDeviceState From(DeviceDto d) => new(
            d.Imei ?? "", d.Imei2, (d.Status ?? "").Trim(),
            d.LastSeen > 0 ? DateTimeOffset.FromUnixTimeMilliseconds(d.LastSeen).UtcDateTime : null,
            d.RelockTimestamp > 0 ? DateTimeOffset.FromUnixTimeMilliseconds(d.RelockTimestamp).UtcDateTime : null,
            d.IsDeviceOffline);

        // Knox can lock and message the phone.
        public static bool Active(string? status) => RecoveryRules.KnoxIsActive(status);

        // In plain words, for staff.
        public static string Explain(string? status) => (status ?? "").Trim().ToLowerInvariant() switch
        {
            "" => "Not read from Knox yet",
            "enrolled" or "active" => "Active: Knox can lock and message it",
            "locked" => "Locked by Knox",
            "blinking" => "Active: lock reminder showing",
            "accepted" => "Accepted: not active yet, can't be locked or messaged",
            "activating" => "Activating: waits for the phone to come online",
            "approved" => "Approved: waits for the phone to come online",
            "pending" => "Pending: not active yet",
            "completed" => "Completed: released by Knox",
            "deleted" => "Deleted from Knox",
            "not in knox" => "Not found in Knox",
            var s => $"{status!.Trim()}: not active",
        };

        public enum Problem
        {
            None,
            // Knox controls the phone, but our locks and messages go to Nuovo or Transsion.
            ActiveInKnoxButSentElsewhere,
            // We send to Knox, but Knox can't act on it (Accepted, Activating, released, missing).
            SentToKnoxButNotActive,
        }

        // An open recovery is already moving the phone (its daily sweep moves
        // it once Knox has it active), so it isn't listed as a problem.
        public static Problem Check(int? lockGroup, string? knoxStatus, bool openRecovery)
        {
            var onKnox = lockGroup == RecoveryRules.KnoxLockGroup;
            if (!onKnox && Active(knoxStatus) && !openRecovery) return Problem.ActiveInKnoxButSentElsewhere;
            if (onKnox && !string.IsNullOrWhiteSpace(knoxStatus) && !Active(knoxStatus)) return Problem.SentToKnoxButNotActive;
            return Problem.None;
        }
    }
}
