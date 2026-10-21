using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;
using Ranalo.DataStore.DataModels;

namespace Ranalo.Configuration
{
    // Pages that need someone signed in. UserSettingsMiddleware has already
    // loaded them from the database (HttpContext.Items["UserSettings"]); this
    // used to read the user straight from the cookie, which anyone could edit.
    public class LoadUserSettingsFromCookieAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            if (context.HttpContext.Items["UserSettings"] is not User)
            {
                context.Result = new RedirectToActionResult("Index", "Login", null);
            }

            base.OnActionExecuting(context);
        }
    }
}
