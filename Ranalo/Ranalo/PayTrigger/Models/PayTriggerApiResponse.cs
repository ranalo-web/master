using System.Text.Json.Serialization;

namespace Ranalo.PayTrigger.Models
{
    // Generic {code, message} envelope every PayTrigger endpoint returns.
    // code == 200 is success; anything else maps to the documented error
    // code table (e.g. 40000 = bad sign, 20003 = apiKey invalid/expired,
    // 40001/40003 = server IP not in the whitelist configured on the
    // Developer Management -> Customize IP portal page).
    public class PayTriggerApiResponse
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        public bool IsSuccess => Code == 200;
    }

    public class PayTriggerApiResponse<T> : PayTriggerApiResponse
    {
        [JsonPropertyName("data")]
        public T? Data { get; set; }
    }
}
