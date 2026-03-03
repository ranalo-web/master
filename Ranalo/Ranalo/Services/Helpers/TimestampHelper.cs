using System.Globalization;

namespace Ranalo.Services.Helpers
{
    public static class TimestampHelper
    {
        /// <summary>
        /// Converts a Unix timestamp in milliseconds to a formatted UTC date string.
        /// </summary>
        /// <param name="unixMilliseconds">The Unix timestamp in milliseconds.</param>
        /// <returns>Formatted date string like 26/09/2026T21:48:39</returns>
        public static string FormatRelockTimestamp(long unixMilliseconds)
        {
            return DateTimeOffset
                .FromUnixTimeMilliseconds(unixMilliseconds)
                .UtcDateTime
                .ToString("dd/MM/yyyy'T'HH:mm:ss", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Optional overload for nullable timestamps.
        /// Returns null if timestamp is null.
        /// </summary>
        public static string? FormatRelockTimestamp(long? unixMilliseconds)
        {
            if (!unixMilliseconds.HasValue) return null;
            return FormatRelockTimestamp(unixMilliseconds.Value);
        }

        /// <summary>
        /// Converts a Unix timestamp in milliseconds to a formatted UTC date string.
        /// Example: 26/09/2026
        /// </summary>
        public static string FormatDateOnly(long unixMilliseconds)
        {
            return DateTimeOffset
                .FromUnixTimeMilliseconds(unixMilliseconds)
                .UtcDateTime
                .ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Nullable overload for date-only formatting.
        /// </summary>
        public static string? FormatDateOnly(long? unixMilliseconds)
        {
            if (!unixMilliseconds.HasValue) return null;
            return FormatDateOnly(unixMilliseconds.Value);
        }
    }
}
