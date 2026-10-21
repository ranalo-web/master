using System.Globalization;
using System.Text.Json;
using Ranalo.Controllers;
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

        // The posted JSON as a transaction, null if it isn't a JSON object.
        // Field names any case; numbers (TransAmount 10) taken as text.
        public static MpesaTransaction? Read(JsonElement body)
        {
            if (body.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            string? Field(string name)
            {
                foreach (var property in body.EnumerateObject())
                {
                    if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                    {
                        return property.Value.ValueKind switch
                        {
                            JsonValueKind.String => property.Value.GetString(),
                            JsonValueKind.Number => property.Value.GetRawText(),
                            _ => null
                        };
                    }
                }
                return null;
            }

            return new MpesaTransaction
            {
                TransID = Field("TransID"),
                TransAmount = Field("TransAmount"),
                BillRefNumber = Field("BillRefNumber"),
                TransTime = Field("TransTime"),
                FirstName = Field("FirstName")
            };
        }

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
