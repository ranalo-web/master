using System.Globalization;
using System.Text.Json;
using Ranalo.Controllers;
using System.Security.Cryptography;
using System.Text;

namespace Ranalo.Services
{
    // Payments posted to api/mpesa/transaction (forwarded from IONOS, 2026-10-21).
    // The endpoint had no checks at all: anyone could add a payment. Checks:
    //   - the shared secret (Azure setting MpesaIntake__SharedSecret), sent as
    //     the X-Ranalo-Key header or ?key=;
    //   - the sender's IP is in MpesaIntake__AllowedIps (comma separated, the
    //     IONOS server), when set;
    //   - the M-Pesa code, amount, account and time must be valid;
    //   - an M-Pesa code already recorded isn't added again.
    // Turning these on at once stopped payments on 2026-10-21, so by default
    // (MpesaIntake__Mode unset or "Monitor") they only log what they'd refuse
    // and the payment is saved as before. Set Mode to "Enforce" once the logs
    // show IONOS's payments pass.
    public static class MpesaIntakeRules
    {
        public const string SecretHeader = "X-Ranalo-Key";
        public const string TransTimeFormat = "yyyyMMddHHmmss";

        public static bool Enforce(string? mode) =>
            string.Equals(mode?.Trim(), "Enforce", StringComparison.OrdinalIgnoreCase);

        // No list configured = any IP. Entries may be "1.2.3.4" or "1.2.3.4:443".
        public static bool IpAllowed(string? allowedIps, string? ip)
        {
            if (string.IsNullOrWhiteSpace(allowedIps))
            {
                return true;
            }

            var sender = WithoutPort(ip);
            return !string.IsNullOrEmpty(sender)
                && allowedIps.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Any(allowed => string.Equals(WithoutPort(allowed), sender, StringComparison.OrdinalIgnoreCase));
        }

        // The sender's IP. Azure's front end adds the real one as the last
        // X-Forwarded-For entry; earlier entries are whatever the sender wrote.
        public static string? SenderIp(string? forwardedFor, string? remoteIp)
        {
            var last = forwardedFor?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault();
            return WithoutPort(last ?? remoteIp);
        }

        private static string? WithoutPort(string? ip)
        {
            if (string.IsNullOrWhiteSpace(ip))
            {
                return null;
            }

            ip = ip.Trim();
            if (ip.StartsWith('['))
            {
                // [::1]:443
                var end = ip.IndexOf(']');
                return end > 0 ? ip[1..end] : ip;
            }

            // One colon = IPv4 with a port; more = a bare IPv6 address.
            return ip.Count(c => c == ':') == 1 ? ip[..ip.IndexOf(':')] : ip;
        }

        // The raw body as a transaction: JSON, or form fields, so nothing is
        // lost to the content type. null if it can't be read.
        public static MpesaTransaction? Read(string? body)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return null;
            }

            try
            {
                using var json = JsonDocument.Parse(body);
                return Read(json.RootElement);
            }
            catch (JsonException)
            {
            }

            var form = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(body);
            string? Field(string name) =>
                form.FirstOrDefault(f => string.Equals(f.Key, name, StringComparison.OrdinalIgnoreCase)).Value.FirstOrDefault();

            var transaction = new MpesaTransaction
            {
                TransID = Field("TransID"),
                TransAmount = Field("TransAmount"),
                BillRefNumber = Field("BillRefNumber"),
                TransTime = Field("TransTime"),
                FirstName = Field("FirstName")
            };
            return transaction.TransID == null && transaction.TransAmount == null ? null : transaction;
        }

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

        // No secret configured = passes.
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
