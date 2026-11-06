namespace Ranalo
{
    // my.ranalocredit.com is the customer app: only its pages, Safaricom's
    // callbacks and static files answer there. Every staff page is a plain
    // 404 on that host, so a customer can never reach one, even by typing
    // the address. The home page goes to the app.
    public class CustomerHostGate
    {
        private static readonly string[] Allowed =
        {
            "/my", "/api/kose/", "/css/", "/js/", "/images/", "/assets/", "/lib/", "/fonts/",
            "/favicon", "/.well-known/",
        };

        private readonly RequestDelegate _next;

        public CustomerHostGate(RequestDelegate next)
        {
            _next = next;
        }

        public static bool IsCustomerHost(HostString host) =>
            host.Host.StartsWith("my.", StringComparison.OrdinalIgnoreCase);

        public static bool PathAllowed(PathString path)
        {
            var p = path.Value ?? "/";
            return Allowed.Any(a => p.StartsWith(a, StringComparison.OrdinalIgnoreCase));
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (IsCustomerHost(context.Request.Host))
            {
                var path = context.Request.Path;
                if (path == "/" || !path.HasValue)
                {
                    context.Response.Redirect("/my");
                    return;
                }
                if (!PathAllowed(path))
                {
                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                    return;
                }
            }

            await _next(context);
        }
    }
}
