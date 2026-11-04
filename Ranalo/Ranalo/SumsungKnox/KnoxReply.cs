using System.Text.Json;

namespace Ranalo.SumsungKnox
{
    // Knox answers a refused message with HTTP 200 and
    // {"result":"FAIL",...}, so the status code alone says nothing:
    // a message was sent only when the body says SUCCESS.
    public static class KnoxReply
    {
        public static bool Accepted(string? body)
        {
            if (string.IsNullOrWhiteSpace(body)) return false;
            try
            {
                using var doc = JsonDocument.Parse(body);
                return doc.RootElement.ValueKind == JsonValueKind.Object
                    && doc.RootElement.TryGetProperty("result", out var result)
                    && string.Equals(result.GetString(), "SUCCESS", StringComparison.OrdinalIgnoreCase);
            }
            catch (JsonException)
            {
                return false;
            }
        }
    }
}
