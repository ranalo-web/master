namespace Ranalo.Models
{
    // Bound from the "SafaricomC2B" section. Safaricom posts paybill payments
    // straight to SafaricomC2BController (replacing kosewefarms.com/malipo/
    // confirmpayments.php on IONOS).
    //
    // CallbackToken is a secret: set it only in Azure App Service >
    // Environment variables as SafaricomC2B__CallbackToken (at least 32
    // random characters). Until it's set, every callback is refused.
    public class SafaricomC2BSettings
    {
        public string? CallbackToken { get; set; }

        // Our paybill. A callback for any other shortcode is refused.
        public string? ShortCode { get; set; }

        // Safaricom's callback servers. Turn off only to test from somewhere
        // else (e.g. Postman against a test slot) -- never in production.
        public bool EnforceIpAllowlist { get; set; } = true;
        public List<string> AllowedIps { get; set; } = new();
    }
}
