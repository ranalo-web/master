using System.Text.Json;

namespace Ranalo.Controllers
{
    public static class CookieHelper
    {
        public static void SetObjectAsJson(this ISession session, string key, object value)
        {
            var json = JsonSerializer.Serialize(value);
            session.SetString(key, json);
        }

        public static void SetCookieObject(HttpContext context, string key, object value, int? expireDays = null)
        {
            var json = JsonSerializer.Serialize(value);
            var options = new CookieOptions
            {
                Expires = expireDays.HasValue ? DateTime.Now.AddDays(expireDays.Value) : null
            };
            context.Response.Cookies.Append(key, json, options);
        }

        public static T? GetCookieObject<T>(HttpContext context, string key)
        {
            var cookieValue = context.Request.Cookies[key];
            return cookieValue is null ? default : JsonSerializer.Deserialize<T>(cookieValue);
        }

        public static string Serialize<T>(T obj)
        {
            return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(obj)));
        }

        public static T Deserialize<T>(string value)
        {
            var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(value));
            return JsonSerializer.Deserialize<T>(json);
        }
    }
}
