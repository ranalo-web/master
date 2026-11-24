using System.Globalization;

namespace Ranalo.Services
{
    // "Last seen": when someone last used the app, as opposed to Last Login
    // (when they last typed their password). Stamped on a signed-in request
    // at most once every StampEveryMinutes, so browsing doesn't write to the
    // database on every page. Times are stored in UTC and shown in Kenya time
    // (UTC+3, as everywhere else in the app). No I/O.
    public static class LastSeenRules
    {
        public const int StampEveryMinutes = 5;

        public static bool ShouldStamp(DateTime? lastSeenUtc, DateTime nowUtc) =>
            lastSeenUtc == null || nowUtc - lastSeenUtc.Value >= TimeSpan.FromMinutes(StampEveryMinutes);

        // "Online now", "12 min ago", "3 hr ago", "Yesterday 14:32",
        // "21 Nov 2026 09:05", or "Never".
        public static string Describe(DateTime? lastSeenUtc, DateTime nowUtc)
        {
            if (lastSeenUtc == null) return "Never";
            var ago = nowUtc - lastSeenUtc.Value;
            if (ago < TimeSpan.FromMinutes(StampEveryMinutes)) return "Online now";
            if (ago < TimeSpan.FromHours(1)) return $"{(int)ago.TotalMinutes} min ago";

            var seen = lastSeenUtc.Value.AddHours(3);
            var today = nowUtc.AddHours(3).Date;
            if (seen.Date == today) return $"{(int)ago.TotalHours} hr ago";
            if (seen.Date == today.AddDays(-1)) return "Yesterday " + seen.ToString("HH:mm", CultureInfo.InvariantCulture);
            return seen.ToString("dd MMM yyyy HH:mm", CultureInfo.InvariantCulture);
        }
    }
}
