using Ranalo.Controllers;
using Ranalo.DataStore.DataModels;
using Ranalo.Services;
using System.Net;
using System.Text.Json;

namespace Ranalo
{
    public class UserSettingsMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IServiceProvider _serviceProvider;

        public UserSettingsMiddleware(RequestDelegate next, IServiceProvider serviceProvider)
        {
            _next = next;
            _serviceProvider = serviceProvider;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Read cookie
            if (context.Request.Cookies.TryGetValue("UserSettings", out var cookieValue))
            {
                // If cookieValue is, for example, JSON with email/password, deserialize it
                var cookieData = CookieHelper.Deserialize<User>(cookieValue);

                using var scope = _serviceProvider.CreateScope();
                var usersService = scope.ServiceProvider.GetRequiredService<IUserService>();

                // Lookup user in DB using email/password from cookie
                var user = await usersService.LoginUser(cookieData.Email, cookieData.PasswordHash);

                if (user != null)
                    context.Items["UserSettings"] = user;
            }

            await _next(context);
        }
    }
}
