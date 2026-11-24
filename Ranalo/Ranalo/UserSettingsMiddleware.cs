using Ranalo.Services;

namespace Ranalo
{
    // Who's signed in: the user id from the signed login cookie (LoginSession),
    // loaded fresh from the database every request, so the role, dealer and
    // suspension are always current and the cookie can't be edited to change
    // them. A cookie for a suspended user, or from before their last password
    // change, signs them out.
    public class UserSettingsMiddleware
    {
        private readonly RequestDelegate _next;

        public UserSettingsMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, ILoginSession session, IUserService users)
        {
            var signedIn = session.Read(context.Request);
            if (signedIn != null)
            {
                var user = await users.GetSignedInUserAsync(signedIn.Value.UserId);
                if (user != null && PasswordRules.Stamp(user.PasswordHash) == signedIn.Value.Stamp)
                {
                    context.Items["UserSettings"] = user;

                    // Last seen, at most every few minutes. Never blocks the page.
                    var now = DateTime.UtcNow;
                    if (LastSeenRules.ShouldStamp(user.LastSeen, now))
                    {
                        try { await users.StampLastSeenAsync(user.UserId, now); }
                        catch (Exception ex)
                        {
                            context.RequestServices.GetService<ILogger<UserSettingsMiddleware>>()
                                ?.LogWarning(ex, "Couldn't stamp last seen for user {UserId}", user.UserId);
                        }
                    }
                }
                else
                {
                    session.SignOut(context.Response);
                }
            }

            await _next(context);
        }
    }
}
