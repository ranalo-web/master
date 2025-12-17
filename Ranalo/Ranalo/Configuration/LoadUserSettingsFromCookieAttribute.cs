using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;
using Ranalo.Controllers;
using Ranalo.DataStore.DataModels;

namespace Ranalo.Configuration
{
    public class LoadUserSettingsFromCookieAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var cookies = context.HttpContext.Request.Cookies;
            if (cookies.TryGetValue("UserSettings", out var encoded))
            {
                try
                {
                    var userSettings = CookieHelper.Deserialize<User>(encoded);
                    // Store in HttpContext.Items so it can be accessed in controllers
                    context.HttpContext.Items["UserSettings"] = userSettings;
                }
                catch
                {
                    context.Result = new RedirectToActionResult("Index", "Login", null);
                }
            }
            else
            {
                context.Result = new RedirectToActionResult("Index", "Login", null);
            }

            base.OnActionExecuting(context);
        }
    }
}
