using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Ranalo.DataStore.DataModels;
using Ranalo.Services;

namespace Ranolo.Web.Tests
{
    // Hashed passwords and the signed login cookie (2026-10-21).
    public class LoginSecurityTests
    {
        // ---- Passwords ------------------------------------------------------

        [Test]
        public void HashedPassword_VerifiesAndIsntTheText()
        {
            var hash = PasswordRules.Hash("correct horse");

            Assert.That(hash, Is.Not.EqualTo("correct horse"));
            Assert.That(PasswordRules.IsHashed(hash), Is.True);
            Assert.That(PasswordRules.Verify(hash, "correct horse"), Is.EqualTo((true, false)));
            Assert.That(PasswordRules.Verify(hash, "wrong horse").Ok, Is.False);
        }

        [Test]
        public void PasswordStoredAsTyped_StillLogsIn_AndNeedsHashing()
        {
            Assert.That(PasswordRules.IsHashed("Kenya2025!"), Is.False);
            Assert.That(PasswordRules.Verify("Kenya2025!", "Kenya2025!"), Is.EqualTo((true, true)));
            Assert.That(PasswordRules.Verify("Kenya2025!", "kenya2025!").Ok, Is.False);
        }

        [TestCase(null, "x")]
        [TestCase("", "x")]
        [TestCase("stored", null)]
        [TestCase("stored", "")]
        public void MissingPassword_NeverMatches(string? stored, string? provided)
        {
            Assert.That(PasswordRules.Verify(stored, provided).Ok, Is.False);
        }

        [Test]
        public void NewPassword_AtLeast8_AndConfirmed()
        {
            Assert.That(PasswordRules.CannotUse("short", "short"), Is.Not.Null);
            Assert.That(PasswordRules.CannotUse("longenough", "different1"), Is.Not.Null);
            Assert.That(PasswordRules.CannotUse("longenough", "longenough"), Is.Null);
        }

        [Test]
        public void Stamp_ChangesWithThePassword()
        {
            Assert.That(PasswordRules.Stamp(PasswordRules.Hash("one-password")),
                Is.Not.EqualTo(PasswordRules.Stamp(PasswordRules.Hash("one-password"))));
        }

        // ---- Login cookie ---------------------------------------------------

        private static readonly LoginSession Session = new(new EphemeralDataProtectionProvider());

        private static HttpRequest RequestWithCookie(string name, string value)
        {
            var context = new DefaultHttpContext();
            context.Request.Headers.Cookie = $"{name}={value}";
            return context.Request;
        }

        private static string IssuedCookie(User user)
        {
            var context = new DefaultHttpContext();
            Session.SignIn(context.Response, user);
            var header = context.Response.Headers.SetCookie.First(h => h!.StartsWith(LoginSession.CookieName))!;
            return header.Split(';')[0][(LoginSession.CookieName.Length + 1)..];
        }

        [Test]
        public void Cookie_RoundTrips_UserIdAndStamp()
        {
            var user = new User { UserId = 42, PasswordHash = PasswordRules.Hash("secret-pass") };
            var read = Session.Read(RequestWithCookie(LoginSession.CookieName, IssuedCookie(user)));

            Assert.That(read, Is.EqualTo((42, PasswordRules.Stamp(user.PasswordHash))));
        }

        [Test]
        public void Cookie_DoesntContainThePasswordOrRole()
        {
            var user = new User { UserId = 42, RoleId = UserRole.Dealer, Email = "a@b.c", PasswordHash = "PlainText123" };
            var cookie = IssuedCookie(user);

            Assert.That(cookie, Does.Not.Contain("PlainText123"));
            Assert.That(cookie, Does.Not.Contain("a@b.c"));
        }

        [Test]
        public void TamperedCookie_IsRejected()
        {
            var cookie = IssuedCookie(new User { UserId = 42, PasswordHash = "x" });
            var tampered = (cookie[0] == 'A' ? 'B' : 'A') + cookie[1..];

            Assert.That(Session.Read(RequestWithCookie(LoginSession.CookieName, tampered)), Is.Null);
        }

        [Test]
        public void OldStyleCookie_IsRejected()
        {
            // The old cookie: the whole user as base64 JSON, role and all.
            var old = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("{\"UserId\":1,\"RoleId\":1}"));

            Assert.That(Session.Read(RequestWithCookie(LoginSession.CookieName, old)), Is.Null);
            Assert.That(Session.Read(RequestWithCookie(LoginSession.OldCookieName, old)), Is.Null);
        }
    }
}
