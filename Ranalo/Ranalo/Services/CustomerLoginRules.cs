using System.Security.Cryptography;

namespace Ranalo.Services
{
    // Customer app login (my.ranalocredit.com, 2026-11-06): the account
    // number, then a one-time code sent to the financed phone itself
    // (Nuovo/Knox/Transsion message), which proves the person holds it.
    //
    // Wrong codes slow the account down, then lock it: a locked account
    // stays locked until staff unblock it after the customer emails in.
    public static class CustomerLoginRules
    {
        public const int CodeLength = 6;
        public static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(10);

        // A code issued by staff (Customer App page) for a customer whose
        // phone messages don't arrive, read to them over a call: longer,
        // still one use. Recorded as issued by staff, never the code itself.
        public static readonly TimeSpan StaffCodeLifetime = TimeSpan.FromMinutes(30);
        public static string StaffIssued(int userId) => $"staff:{userId}";

        // A new code at most this often, and at most MaxCodesPerHour an hour.
        public static readonly TimeSpan ResendGap = TimeSpan.FromSeconds(60);
        public const int MaxCodesPerHour = 5;

        // Wrong codes in a row (any code) before the account is locked.
        public const int LockAfterFailures = 10;

        // Stays signed in on the phone this long after the last visit.
        public static readonly TimeSpan SessionLifetime = TimeSpan.FromDays(90);

        public static string NewCode() =>
            RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

        public static string? CleanCode(string? typed)
        {
            var digits = new string((typed ?? "").Where(char.IsDigit).ToArray());
            return digits.Length == CodeLength ? digits : null;
        }

        public static long? AccountNo(string? typed)
        {
            var digits = new string((typed ?? "").Where(char.IsDigit).ToArray());
            return digits.Length is >= 5 and <= 12 && long.TryParse(digits, out var n) ? n : null;
        }

        // After 3 wrong codes in a row, each further try waits longer:
        // 3 -> 1 min, 4 -> 2, 5 -> 4, ... capped at 30 min. Locked at 10.
        public static TimeSpan WaitAfter(int failures) =>
            failures < 3 ? TimeSpan.Zero : TimeSpan.FromMinutes(Math.Min(30, Math.Pow(2, failures - 3)));

        public static bool Locks(int failures) => failures >= LockAfterFailures;

        // How long until another code can be sent, or zero.
        public static TimeSpan SendWait(IReadOnlyCollection<DateTime> codesSentUtc, DateTime nowUtc)
        {
            var lastHour = codesSentUtc.Where(t => t > nowUtc.AddHours(-1)).OrderBy(t => t).ToList();
            if (lastHour.Count >= MaxCodesPerHour)
            {
                return lastHour[^MaxCodesPerHour].AddHours(1) - nowUtc;
            }
            var last = lastHour.LastOrDefault();
            return last != default && nowUtc - last < ResendGap ? ResendGap - (nowUtc - last) : TimeSpan.Zero;
        }

        public static string Wait(TimeSpan wait) =>
            wait.TotalMinutes >= 1 ? $"{Math.Ceiling(wait.TotalMinutes):0} minute{(Math.Ceiling(wait.TotalMinutes) == 1 ? "" : "s")}"
                                   : $"{Math.Max(1, Math.Ceiling(wait.TotalSeconds)):0} seconds";

        public static string CodeMessage(string code) =>
            $"Your Ranalo app code is {code}. It works for 10 minutes. Never share it: Ranalo staff will never ask for it.";

        // What MessageLogs keeps instead: staff can read the log, so never the code.
        public const string CodeLogText = "Ranalo app login code sent (code not logged).";
    }
}
