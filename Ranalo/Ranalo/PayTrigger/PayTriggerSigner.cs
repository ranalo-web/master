using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Ranalo.PayTrigger
{
    // Implements PayTrigger's request-signing algorithm exactly as documented
    // (section 1.2 of the PayTrigger Partner API docs), verified against
    // their worked example:
    //   1. Take non-null/non-empty request fields.
    //   2. Sort by field name, ordinal ASCII ascending.
    //   3. Join as "key1=value1&key2=value2..." (no URL-encoding, no
    //      trailing "&"). Values are each field's plain string form --
    //      booleans as lowercase "true"/"false", exactly like JS string
    //      concatenation of a parsed JSON value.
    //   4. HMAC-SHA256 that string, using the apiKey itself (UTF-8 bytes) as
    //      the HMAC key.
    //   5. Hex-encode the MAC, uppercase it.
    //   6. Base64-encode the UTF-8 bytes of THAT UPPERCASE HEX STRING (not
    //      the raw MAC bytes -- this double-encoding step is easy to miss).
    // The result goes in the "sign" HTTP header.
    public static class PayTriggerSigner
    {
        public static string Sign(IEnumerable<KeyValuePair<string, string?>> fields, string apiKey)
        {
            var content = BuildSignContent(fields);

            var keyBytes = Encoding.UTF8.GetBytes(apiKey);
            var contentBytes = Encoding.UTF8.GetBytes(content);

            using var hmac = new HMACSHA256(keyBytes);
            var mac = hmac.ComputeHash(contentBytes);

            var hexUpper = Convert.ToHexString(mac); // already uppercase

            return Convert.ToBase64String(Encoding.UTF8.GetBytes(hexUpper));
        }

        public static string BuildSignContent(IEnumerable<KeyValuePair<string, string?>> fields)
        {
            var nonEmpty = fields
                .Where(kv => !string.IsNullOrEmpty(kv.Value))
                .OrderBy(kv => kv.Key, StringComparer.Ordinal);

            return string.Join("&", nonEmpty.Select(kv => $"{kv.Key}={kv.Value}"));
        }

        public static string Bool(bool value) => value ? "true" : "false";

        // Extracts top-level field name/value pairs from a flat JSON object,
        // as sign-content-ready strings (numbers/booleans in their JSON text
        // form, strings as-is). Shared by PayTriggerClient (signing our own
        // outbound requests) and PayTriggerWebhookController (verifying
        // PayTrigger's incoming callback signature) so both always derive
        // the sign content the exact same way.
        public static IEnumerable<KeyValuePair<string, string?>> ExtractFieldsFromJson(string json)
        {
            using var doc = JsonDocument.Parse(json);

            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                string? value = prop.Value.ValueKind switch
                {
                    JsonValueKind.String => prop.Value.GetString(),
                    JsonValueKind.Number => prop.Value.GetRawText(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    JsonValueKind.Null => null,
                    _ => prop.Value.GetRawText()
                };

                yield return new KeyValuePair<string, string?>(prop.Name, value);
            }
        }

        public static string SignJson(string json, string apiKey) =>
            Sign(ExtractFieldsFromJson(json), apiKey);

        // Constant-time comparison -- this validates an inbound webhook's
        // authenticity, so it must not leak timing information about how
        // much of the signature matched.
        public static bool VerifyJson(string json, string apiKey, string? providedSign)
        {
            if (string.IsNullOrEmpty(providedSign))
            {
                return false;
            }

            var expected = SignJson(json, apiKey);

            var expectedBytes = Encoding.UTF8.GetBytes(expected);
            var providedBytes = Encoding.UTF8.GetBytes(providedSign);

            return expectedBytes.Length == providedBytes.Length &&
                   CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
        }
    }
}
