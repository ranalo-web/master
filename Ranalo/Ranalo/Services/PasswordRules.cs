using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Ranalo.DataStore.DataModels;

namespace Ranalo.Services
{
    // Passwords are stored hashed (ASP.NET Identity's PBKDF2 format) in
    // Users.PasswordHash (2026-10-21). They used to be stored as typed; those
    // still log in, and are hashed the next time the person logs in.
    public static class PasswordRules
    {
        public const int MinLength = 8;

        private static readonly PasswordHasher<User> Hasher = new();

        public static string Hash(string password) => Hasher.HashPassword(null!, password);

        // Identity's v3 format: base64 starting with a 0x01 marker byte, 61+ bytes.
        public static bool IsHashed(string? stored)
        {
            if (string.IsNullOrEmpty(stored) || stored.Length < 80)
            {
                return false;
            }

            var bytes = new byte[stored.Length];
            return Convert.TryFromBase64String(stored, bytes, out var written) && written >= 61 && bytes[0] == 0x01;
        }

        // Ok = the password matches; NeedsRehash = it's stored as typed (or in
        // an older hash format) and should be hashed now.
        public static (bool Ok, bool NeedsRehash) Verify(string? stored, string? provided)
        {
            if (string.IsNullOrEmpty(stored) || string.IsNullOrEmpty(provided))
            {
                return (false, false);
            }

            if (IsHashed(stored))
            {
                var result = Hasher.VerifyHashedPassword(null!, stored, provided);
                return (result != PasswordVerificationResult.Failed, result == PasswordVerificationResult.SuccessRehashNeeded);
            }

            var ok = CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(stored), Encoding.UTF8.GetBytes(provided));
            return (ok, ok);
        }

        // null = acceptable, otherwise why not.
        public static string? CannotUse(string? password, string? confirm)
        {
            if (string.IsNullOrWhiteSpace(password) || password.Length < MinLength)
            {
                return $"The password must be at least {MinLength} characters.";
            }

            return password == confirm ? null : "The two passwords don't match.";
        }

        // Changes whenever the password does, so a password change signs out
        // every other browser (LoginSession).
        public static string Stamp(string? stored) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(stored ?? "")))[..16];
    }
}
