using Ranalo.Services;
using System.Net;

namespace Ranolo.Web.Tests
{
    // Who may post Safaricom C2B callbacks to api/kose/c2b/... (2026-10-22).
    public class SafaricomCallerRulesTests
    {
        private const string Token = "0123456789abcdef0123456789abcdef";
        private static readonly string[] Safaricom = { "196.201.214.200", "196.201.212.69" };

        [Test]
        public void NoTokenConfigured_RefusesEveryone()
        {
            Assert.That(SafaricomCallerRules.TokenOk(null, Token), Is.False);
            Assert.That(SafaricomCallerRules.TokenOk("", ""), Is.False);
        }

        [Test]
        public void ShortToken_RefusesEveryone()
        {
            Assert.That(SafaricomCallerRules.TokenOk("short", "short"), Is.False);
        }

        [Test]
        public void Token_MustMatch()
        {
            Assert.That(SafaricomCallerRules.TokenOk(Token, Token), Is.True);
            Assert.That(SafaricomCallerRules.TokenOk(Token, Token.ToUpperInvariant()), Is.False);
            Assert.That(SafaricomCallerRules.TokenOk(Token, Token + "x"), Is.False);
            Assert.That(SafaricomCallerRules.TokenOk(Token, null), Is.False);
        }

        [Test]
        public void PublicRemoteAddress_IsTheCaller_ForwardedForIgnored()
        {
            var ip = SafaricomCallerRules.CallerIp(IPAddress.Parse("41.90.1.2"), "196.201.214.200");
            Assert.That(ip, Is.EqualTo(IPAddress.Parse("41.90.1.2")));
            Assert.That(SafaricomCallerRules.IpAllowed(ip, Safaricom), Is.False);
        }

        [Test]
        public void BehindAzureFrontEnd_LastForwardedForEntry_WithPort()
        {
            var ip = SafaricomCallerRules.CallerIp(IPAddress.Parse("169.254.129.3"), "196.201.214.200:51234");
            Assert.That(SafaricomCallerRules.IpAllowed(ip, Safaricom), Is.True);
        }

        [Test]
        public void SpoofedForwardedFor_EarlierEntriesIgnored()
        {
            // The caller sent "196.201.214.200"; the front end added the real address.
            var ip = SafaricomCallerRules.CallerIp(IPAddress.Parse("10.0.0.5"), "196.201.214.200, 41.90.1.2:4000");
            Assert.That(ip, Is.EqualTo(IPAddress.Parse("41.90.1.2")));
            Assert.That(SafaricomCallerRules.IpAllowed(ip, Safaricom), Is.False);
        }

        [Test]
        public void Ipv4MappedRemoteAddress_Matches()
        {
            var ip = SafaricomCallerRules.CallerIp(IPAddress.Parse("196.201.212.69").MapToIPv6(), null);
            Assert.That(SafaricomCallerRules.IpAllowed(ip, Safaricom), Is.True);
        }

        [Test]
        public void NoAddressAtAll_Refused()
        {
            Assert.That(SafaricomCallerRules.IpAllowed(SafaricomCallerRules.CallerIp(null, null), Safaricom), Is.False);
            Assert.That(SafaricomCallerRules.IpAllowed(SafaricomCallerRules.CallerIp(IPAddress.Loopback, "not-an-ip"), Safaricom), Is.False);
        }
    }
}
