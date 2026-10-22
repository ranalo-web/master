using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace Ranalo.Services
{
    // Who may post to SafaricomC2BController. Safaricom doesn't sign C2B
    // callbacks, so a callback is only believed when both:
    //   - the secret token in the registered URL matches, and
    //   - it comes from one of Safaricom's callback IPs.
    public static class SafaricomCallerRules
    {
        public const int MinTokenLength = 32;

        // Not configured (or too short to be a real secret) = refuse all.
        public static bool TokenOk(string? configured, string? provided)
        {
            if (string.IsNullOrEmpty(configured) || configured.Length < MinTokenLength || string.IsNullOrEmpty(provided))
            {
                return false;
            }

            return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(configured), Encoding.UTF8.GetBytes(provided));
        }

        // The caller's real address. On App Service the connection can come
        // from Azure's front end (a private address); then the caller is the
        // last X-Forwarded-For entry, the one the front end added. Earlier
        // entries are whatever the caller sent and can't be trusted.
        public static IPAddress? CallerIp(IPAddress? remote, string? forwardedFor)
        {
            if (remote != null && remote.IsIPv4MappedToIPv6)
            {
                remote = remote.MapToIPv4();
            }

            if (remote != null && !IsPrivate(remote))
            {
                return remote;
            }

            var last = forwardedFor?.Split(',').Select(e => e.Trim()).LastOrDefault(e => e.Length > 0);
            return ParseAddress(last) ?? remote;
        }

        public static bool IpAllowed(IPAddress? caller, IEnumerable<string> allowed)
        {
            if (caller == null)
            {
                return false;
            }

            return allowed.Any(a => IPAddress.TryParse(a.Trim(), out var ip) && ip.Equals(caller));
        }

        // "1.2.3.4", "1.2.3.4:5678", "::1", "[::1]:5678".
        private static IPAddress? ParseAddress(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }

            if (IPEndPoint.TryParse(value, out var endPoint))
            {
                var ip = endPoint.Address;
                return ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4() : ip;
            }

            return null;
        }

        private static bool IsPrivate(IPAddress ip)
        {
            if (IPAddress.IsLoopback(ip))
            {
                return true;
            }

            if (ip.AddressFamily == AddressFamily.InterNetworkV6)
            {
                return ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || (ip.GetAddressBytes()[0] & 0xFE) == 0xFC;
            }

            var b = ip.GetAddressBytes();
            return b[0] == 10
                || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
                || (b[0] == 192 && b[1] == 168)
                || (b[0] == 169 && b[1] == 254)
                || (b[0] == 100 && b[1] >= 64 && b[1] <= 127);
        }
    }
}
