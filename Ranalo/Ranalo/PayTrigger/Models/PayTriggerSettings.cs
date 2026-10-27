namespace Ranalo.PayTrigger.Models
{
    // Bound from appsettings.*.json "PayTrigger" section. Non-India base URL
    // is https://paytrigger.transsion-os.com/PayTrigger/ -- India market uses
    // https://ind-paytrigger.transsion-os.com/PayTrigger/ instead. Must end
    // with a trailing slash so relative request paths (no leading slash)
    // combine correctly via HttpClient.BaseAddress.
    public class PayTriggerSettings
    {
        public string BaseUrl { get; set; } = null!;
        public string ApiKey { get; set; } = null!;

        // The "Pay with M-Pesa" button on popups: the app it opens (deeplinkPkg)
        // and, if needed, the link inside it (deeplink). No package = no button.
        public string? MpesaAppPackage { get; set; }
        public string? MpesaDeeplink { get; set; }
    }
}
