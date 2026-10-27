using Ranalo.PayTrigger.Models;
using Ranalo.Services;

namespace Ranalo.PayTrigger
{
    // A customer message as a PayTrigger popup, clipped to its limits, with
    // the "Pay with M-Pesa" button when PayTriggerSettings.MpesaAppPackage
    // is set (off until tried on a phone: set PayTrigger__MpesaAppPackage).
    public static class PayTriggerPopup
    {
        public const int PushTypePopup = 1;

        public static SendPushInfoRequest For(string? imei, CustomerMessages.Message message, PayTriggerSettings? settings)
        {
            var request = new SendPushInfoRequest
            {
                Imei = imei,
                Title = CustomerMessages.Clip(message.Title, CustomerMessages.TitleLimit),
                Content = CustomerMessages.Clip(message.Content, CustomerMessages.ContentLimit),
                PushType = PushTypePopup
            };

            if (!string.IsNullOrWhiteSpace(settings?.MpesaAppPackage))
            {
                request.DeeplinkPkg = settings.MpesaAppPackage;
                request.Deeplink = string.IsNullOrWhiteSpace(settings.MpesaDeeplink) ? null : settings.MpesaDeeplink;
            }

            return request;
        }
    }
}
