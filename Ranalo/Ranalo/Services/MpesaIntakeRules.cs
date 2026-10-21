using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Ranalo.Services
{
    // Payments posted to api/mpesa/transaction (forwarded from IONOS, 2026-10-21).
    // The endpoint had no checks at all: anyone could add a payment. Now:
    //   - the shared secret (Azure setting MpesaIntake__SharedSecret) must be
    //     sent as the X-Ranalo-Key header or ?key= -- off until it's set, so
    //     payments keep flowing until IONOS sends it;
    //   - the M-Pesa code, amount, account and time must be valid;
    //   - an M-Pesa code already recorded isn't added again (KosePaymentsRepository.InsertAsync).
    public static class MpesaIntakeRules
    {
        public const string SecretHeader = "X-Ranalo-Key";
        public const string TransTimeFormat = "yyyyMMddHHmmss";

        // No secret configured = not enforced yet.
        public static bool SecretOk(string? configured, string? provided)
        {
            if (string.IsNullOrEmpty(configured))
            {
                return true;
            }

            return !string.IsNullOrEmpty(provided)
                && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(configured), Encoding.UTF8.GetBytes(provided));
        }

        // null = fine, otherwise why it's refused.
        public static string? Invalid(string? transId, string? amount, string? account, string? transTime)
        {
            if (string.IsNullOrWhiteSpace(transId) || transId.Length > 50 || !transId.All(char.IsLetterOrDigit))
            {
                return "Invalid TransID.";
            }
            if (!decimal.TryParse(amount, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) || value <= 0)
            {
                return "Invalid TransAmount.";
            }
            if (string.IsNullOrWhiteSpace(account) || account.Length > 30)
            {
                return "Invalid BillRefNumber.";
            }
            if (!DateTime.TryParseExact(transTime, TransTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            {
                return "Invalid TransTime.";
            }

            return null;
        }
    }
}
