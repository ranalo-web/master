using Microsoft.AspNetCore.DataProtection;

namespace Ranalo.Services
{
    public interface ICustomerSession
    {
        void SignIn(HttpResponse response, long accountNo, string stamp);
        void SignOut(HttpResponse response);
        // The signed-in account and its session stamp, or null.
        (long AccountNo, string Stamp)? Read(HttpRequest request);

        void StartPreview(HttpResponse response, long accountNo, int staffUserId);
        void EndPreview(HttpResponse response);
        long? ReadPreview(HttpRequest request, int staffUserId);
    }

    // The customer app's login cookie: only the account number and its
    // CustomerAccess.SessionStamp, encrypted and signed. Separate from the
    // staff cookie (LoginSession) and protector, so neither can stand in for
    // the other. Renewed on every visit, so an app in use stays signed in.
    public class CustomerSession : ICustomerSession
    {
        public const string CookieName = "Ranalo.My";
        private readonly IDataProtector _protector;

        public CustomerSession(IDataProtectionProvider provider)
        {
            _protector = provider.CreateProtector("Ranalo.CustomerApp.v1");
        }

        public void SignIn(HttpResponse response, long accountNo, string stamp) =>
            response.Cookies.Append(CookieName, _protector.Protect($"{accountNo}|{stamp}"), new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.Add(CustomerLoginRules.SessionLifetime),
                HttpOnly = true,
                Secure = true,
                IsEssential = true,
                // Lax: opening the app from a link or the Play Store wrapper keeps it.
                SameSite = SameSiteMode.Lax,
                Path = "/",
            });

        public void SignOut(HttpResponse response) => response.Cookies.Delete(CookieName);

        // ---- Staff preview: Admin/Approver see an account's app, read-only ----

        public const string PreviewCookieName = "Ranalo.MyPreview";
        public static readonly TimeSpan PreviewLifetime = TimeSpan.FromHours(1);

        public void StartPreview(HttpResponse response, long accountNo, int staffUserId) =>
            response.Cookies.Append(PreviewCookieName,
                _protector.Protect($"preview|{accountNo}|{staffUserId}|{DateTime.UtcNow.Add(PreviewLifetime).Ticks}"), new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.Add(PreviewLifetime),
                    HttpOnly = true,
                    Secure = true,
                    IsEssential = true,
                    SameSite = SameSiteMode.Strict,
                    Path = "/",
                });

        public void EndPreview(HttpResponse response) => response.Cookies.Delete(PreviewCookieName);

        // The account being previewed, if the cookie is valid, unexpired and
        // belongs to this staff user.
        public long? ReadPreview(HttpRequest request, int staffUserId)
        {
            if (!request.Cookies.TryGetValue(PreviewCookieName, out var value) || string.IsNullOrEmpty(value)) return null;
            try
            {
                var p = _protector.Unprotect(value).Split('|');
                return p.Length == 4 && p[0] == "preview"
                    && long.TryParse(p[1], out var account)
                    && int.TryParse(p[2], out var user) && user == staffUserId
                    && long.TryParse(p[3], out var ticks) && new DateTime(ticks, DateTimeKind.Utc) > DateTime.UtcNow
                    ? account : null;
            }
            catch (System.Security.Cryptography.CryptographicException)
            {
                return null;
            }
        }

        public (long AccountNo, string Stamp)? Read(HttpRequest request)
        {
            if (!request.Cookies.TryGetValue(CookieName, out var value) || string.IsNullOrEmpty(value)) return null;
            try
            {
                var parts = _protector.Unprotect(value).Split('|');
                return parts.Length == 2 && long.TryParse(parts[0], out var account) ? (account, parts[1]) : null;
            }
            catch (System.Security.Cryptography.CryptographicException)
            {
                return null;
            }
        }
    }
}
