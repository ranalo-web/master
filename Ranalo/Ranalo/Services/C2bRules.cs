namespace Ranalo.Services
{
    // Safaricom C2B callbacks sent straight to Azure (api/malipo/confirm), so
    // payments don't depend on IONOS (callbacks to IONOS stopped 2026-10-21
    // 17:32). Only Safaricom's servers are accepted: the same IPs IONOS's
    // confirmpayments.php has checked for months. Azure setting
    // C2b__AllowedIps replaces the list if Safaricom publishes new ones.
    public static class C2bRules
    {
        public const string SafaricomIps =
            "196.201.214.200,196.201.214.206,196.201.213.114,196.201.214.207,196.201.214.208,196.201.213.44," +
            "196.201.212.127,196.201.212.138,196.201.212.129,196.201.212.136,196.201.212.74,196.201.212.69";

        public static string AllowedIps(string? configured) =>
            string.IsNullOrWhiteSpace(configured) ? SafaricomIps : configured;

        // What Safaricom expects back. Any other answer (or none) and it retries.
        public static object Accepted => new { ResultCode = 0, ResultDesc = "Accepted" };
    }
}
