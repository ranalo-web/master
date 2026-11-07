using System.Globalization;

namespace Ranalo.Services
{
    // One message to every customer (Admin > Announcements): a title and a
    // text with {name} and {account} filled in per customer, sent through
    // each phone's own lock provider and logged like every message, so it
    // also shows in the app's Notifications. Each send-to-all gets its own
    // MessageType ("Announcement:20261107-2100"), which is how a customer
    // is never sent the same one twice. TVs are left out.
    public static class AnnouncementRules
    {
        public const string TypePrefix = "Announcement:";
        public const string TestType = TypePrefix + "test";

        public const string DefaultTitle = "New: the Ranalo Credit app";

        public const string DefaultBody =
            "Hi {name}, you can now see your balance, statement and messages in the Ranalo Credit app.\n" +
            "\n" +
            "Open the Ranalo Credit icon on your phone. No icon? Open my.ranalocredit.com in Chrome and tap Install.\n" +
            "Sign in with your account number {account}. We send a code to this phone.\n" +
            "\n" +
            "Help: call " + CustomerMessages.HelpLine;

        public static string TypeFor(DateTime utc) =>
            TypePrefix + utc.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture);

        public static bool IsAnnouncement(string? messageType) =>
            messageType?.StartsWith(TypePrefix, StringComparison.Ordinal) == true;

        public static CustomerMessages.Message For(string title, string body, string? firstName, long accountNo) =>
            new(title.Trim(),
                body.Replace("\r\n", "\n").Trim()
                    .Replace("{name}", CustomerMessages.Name(firstName))
                    .Replace("{account}", accountNo.ToString(CultureInfo.InvariantCulture)));

        // null = fine to send. The longest name and account are allowed for
        // so no customer's copy runs past PayTrigger's limit.
        public static string? Problem(string? title, string? body)
        {
            if (string.IsNullOrWhiteSpace(title)) return "Give the message a title.";
            if (string.IsNullOrWhiteSpace(body)) return "Write the message.";
            if (title.Trim().Length > CustomerMessages.TitleLimit)
                return $"The title is too long: {CustomerMessages.TitleLimit} characters at most.";

            var longest = For(title, body, "Bartholomew", 999_999_999_999).Content;
            if (longest.Length > CustomerMessages.ContentLimit)
                return $"The message is too long ({longest.Length} characters with a long name and account number; {CustomerMessages.ContentLimit} at most).";

            var refused = (title + body).Where(c => CustomerMessages.KnoxNotAllowed.Contains(c)).Distinct().ToArray();
            if (refused.Length > 0)
                return "Knox refuses these characters, so leave them out: " + string.Join(" ", refused);

            return null;
        }
    }
}
