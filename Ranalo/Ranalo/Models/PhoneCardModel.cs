using System.Globalization;

namespace Ranalo.Models
{
    // The phone card on Account Details and the account status page
    // (Views/Shared/_PhoneCard.cshtml), 2026-11-10.
    public class PhoneCardModel
    {
        public string? Make { get; set; }
        public string? Model { get; set; }
        public string? Imei { get; set; }
        public string Service { get; set; } = "Nuovo";
        public bool Locked { get; set; }
        public string? Enrolment { get; set; }
        public DateTime? NextLock { get; set; }
        public string? NextLockText { get; set; }      // shown as is when it can't be read
        public DateTime? LastConnectedUtc { get; set; }
        public DateTime? EnrolledUtc { get; set; }
        public string? ProductName { get; set; }
        public string? Storage { get; set; }
        public string? Ram { get; set; }
        public string? Colour { get; set; }

        public string Title => string.Join(" ", new[] { Make, Model }.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!.Trim()))
            is { Length: > 0 } t ? Cap(t) : "Phone";

        public IEnumerable<string> Specs => new[] { ProductName, Storage, Ram, Colour }
            .Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!.Trim());

        private static string Cap(string s) => char.ToUpperInvariant(s[0]) + s[1..];

        private static readonly string[] Formats =
        {
            "dd-MM-yy HH:mm:ss", "dd/MM/yyyy'T'HH:mm:ss", "dd/MM/yyyy HH:mm:ss", "dd/MM/yyyy",
            "yyyy-MM-dd'T'HH:mm:ss", "yyyy-MM-dd HH:mm:ss",
        };

        // Our date text ("10-11-26 09:55:00 UTC", "11/11/2026T12:55:00", ISO).
        // null when unreadable or "never" (01-01-70).
        public static DateTime? Parse(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            var t = text.Trim().Replace(" UTC", "");
            if (!DateTime.TryParseExact(t, Formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
                && !DateTime.TryParse(t, CultureInfo.InvariantCulture, DateTimeStyles.None, out d))
                return null;
            return d.Year < 2000 ? null : d;
        }

        // "5 min ago", "3 h ago", "2 days ago".
        public static string Ago(DateTime utc, DateTime nowUtc)
        {
            var span = nowUtc - utc;
            if (span < TimeSpan.Zero) return "just now";
            if (span.TotalMinutes < 60) return $"{Math.Max(1, (int)span.TotalMinutes)} min ago";
            if (span.TotalHours < 24) return $"{(int)span.TotalHours} h ago";
            var days = (int)span.TotalDays;
            return days == 1 ? "1 day ago" : $"{days} days ago";
        }
    }
}
