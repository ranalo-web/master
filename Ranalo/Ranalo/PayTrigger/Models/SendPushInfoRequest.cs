using System.Text.Json.Serialization;

namespace Ranalo.PayTrigger.Models
{
    // POST api/partner/push/v1/sendPushInfo -- sends a message to a single
    // device. Real equivalent of Knox's SendMessageAsync (used for payment
    // reminders in ScheduledSendPaymentMessages.cs / PaymentReminderService.cs).
    public class SendPushInfoRequest
    {
        [JsonPropertyName("deviceTag")]
        public string? DeviceTag { get; set; }

        [JsonPropertyName("imei")]
        public string? Imei { get; set; }

        [JsonPropertyName("apiKey")]
        public string ApiKey { get; set; } = null!;

        // Limit 500 chars.
        [JsonPropertyName("content")]
        public string Content { get; set; } = null!;

        // Limit 80 chars.
        [JsonPropertyName("title")]
        public string Title { get; set; } = null!;

        // 1-popup 2-push notification 3-simulated call recording 4-promisePay
        [JsonPropertyName("pushType")]
        public int PushType { get; set; }

        [JsonPropertyName("h5link")]
        public string? H5Link { get; set; }

        [JsonPropertyName("imgUrl")]
        public string? ImgUrl { get; set; }

        [JsonPropertyName("deeplink")]
        public string? Deeplink { get; set; }

        [JsonPropertyName("deeplinkPkg")]
        public string? DeeplinkPkg { get; set; }
    }
}
