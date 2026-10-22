using System.Globalization;

namespace Ranalo.Services
{
    // Payment callbacks stopped reaching us at 17:32 EAT on 2026-10-21, so
    // customers who paid were being locked. While the Azure setting
    // LockGrace__Until (e.g. "2026-10-24T09:00:00+03:00") is in the future:
    //   - no phone with a balance gets a lock date before it, overdue ones
    //     included (they're unlocked until then); lost/stolen phones stay locked;
    //   - lock reminder and restructure SMS aren't sent.
    // It ends by itself at that time; clear or change the setting to end or
    // extend it. A time with no offset ("2026-10-24 09:00") is Nairobi time.
    public static class LockGrace
    {
        public const string Setting = "LockGrace:Until";
        private static readonly TimeSpan Nairobi = TimeSpan.FromHours(3);

        // The grace end, or null when there's none now (unset, unreadable or past).
        public static DateTimeOffset? Until(string? configured, DateTimeOffset now)
        {
            if (string.IsNullOrWhiteSpace(configured)
                || !DateTime.TryParse(configured.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
            {
                return null;
            }

            var until = parsed.Kind == DateTimeKind.Unspecified
                ? new DateTimeOffset(parsed, Nairobi)
                : new DateTimeOffset(parsed.ToUniversalTime());
            return until > now ? until : null;
        }

        public static DateTimeOffset? Until(IConfiguration configuration) =>
            Until(configuration[Setting], DateTimeOffset.UtcNow);

        // The lock date, moved to the grace end if it falls before it.
        public static DateTime Apply(DateTime autoLockDate, DateTimeOffset? until)
        {
            if (until == null || autoLockDate == DateTime.MaxValue)
            {
                return autoLockDate;
            }

            var lockAt = autoLockDate.Kind == DateTimeKind.Utc
                ? new DateTimeOffset(autoLockDate)
                : new DateTimeOffset(DateTime.SpecifyKind(autoLockDate, DateTimeKind.Local));
            if (lockAt >= until.Value)
            {
                return autoLockDate;
            }

            return autoLockDate.Kind == DateTimeKind.Utc ? until.Value.UtcDateTime : until.Value.LocalDateTime;
        }
    }
}
