using Microsoft.AspNetCore.DataProtection;
using Ranalo.DataStore.DataModels;

namespace Ranalo.Services
{
    public interface ILoginSession
    {
        void SignIn(HttpResponse response, User user);
        void SignOut(HttpResponse response);
        // The signed-in user id and password stamp, or null when the cookie is
        // missing, tampered with or from before 2026-10-21.
        (int UserId, string Stamp)? Read(HttpRequest request);
    }

    // The login cookie holds only the user id and a stamp of their password,
    // encrypted and signed with ASP.NET data protection. Who they are, their
    // role and dealer are loaded from the database on every request
    // (UserSettingsMiddleware). It used to hold the whole user -- role and
    // password included -- readable and editable by anyone.
    public class LoginSession : ILoginSession
    {
        public const string CookieName = "Ranalo.Session";
        public const string OldCookieName = "UserSettings";
        public static readonly TimeSpan Lifetime = TimeSpan.FromDays(30);

        private readonly IDataProtector _protector;

        public LoginSession(IDataProtectionProvider provider)
        {
            _protector = provider.CreateProtector("Ranalo.Login.v1");
        }

        public void SignIn(HttpResponse response, User user)
        {
            response.Cookies.Delete(OldCookieName);
            response.Cookies.Append(CookieName, _protector.Protect($"{user.UserId}|{PasswordRules.Stamp(user.PasswordHash)}"), new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.Add(Lifetime),
                HttpOnly = true,
                Secure = true,
                IsEssential = true,
                SameSite = SameSiteMode.Strict
            });
        }

        public void SignOut(HttpResponse response)
        {
            response.Cookies.Delete(CookieName);
            response.Cookies.Delete(OldCookieName);
        }

        public (int UserId, string Stamp)? Read(HttpRequest request)
        {
            if (!request.Cookies.TryGetValue(CookieName, out var value) || string.IsNullOrEmpty(value))
            {
                return null;
            }

            try
            {
                var parts = _protector.Unprotect(value).Split('|');
                return parts.Length == 2 && int.TryParse(parts[0], out var userId) ? (userId, parts[1]) : null;
            }
            catch (System.Security.Cryptography.CryptographicException)
            {
                return null;
            }
        }
    }
}
