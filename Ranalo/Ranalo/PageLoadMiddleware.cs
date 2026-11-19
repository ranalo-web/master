using System.Net;
using Ranalo.DataStore.DataModels;
using Ranalo.Services;

namespace Ranalo
{
    // Stops the database work of a page nobody is waiting for any more, so
    // clicks don't queue up queries on the database:
    //  - the browser left the page (closed the tab, pressed Stop, or clicked
    //    elsewhere and dropped the request);
    //  - the same person opened another page in the same tab before this one
    //    finished. Matched per user and per tab (click-guard.js sets the tab
    //    cookie), so a page in another tab is left alone.
    // Only page reads (GET) are stopped, and only their read queries (see
    // CancellableDbConnection). A save is never stopped; one in the same tab
    // does stop the page that was loading before it.
    public class PageLoadMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly PageLoadRegistry _registry;
        private readonly ILogger<PageLoadMiddleware> _logger;

        public PageLoadMiddleware(RequestDelegate next, PageLoadRegistry registry, ILogger<PageLoadMiddleware> logger)
        {
            _next = next;
            _registry = registry;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, RequestCancellation cancellation)
        {
            var request = context.Request;
            var path = request.Path + request.QueryString;
            var isRead = PageLoadRules.IsRead(request.Method);

            string? key = null;
            if (PageLoadRules.IsPageOpen(request.Headers["Sec-Fetch-Mode"]))
            {
                var user = context.Items["UserSettings"] as User;
                request.Cookies.TryGetValue(PageLoadRules.TabCookie, out var tab);
                key = PageLoadRules.Key(user?.UserId, tab);
            }

            PageLoadRegistry.PageLoad? load = null;
            if (key != null)
            {
                if (isRead) load = _registry.Begin(key, path);
                else _registry.StopCurrent(key, path);
            }

            if (!isRead)
            {
                await _next(context);
                return;
            }

            using var stop = load == null
                ? CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted)
                : CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted, load.Token);
            cancellation.Token = stop.Token;
            try
            {
                await _next(context);
            }
            catch (Exception) when (stop.IsCancellationRequested)
            {
                if (context.RequestAborted.IsCancellationRequested)
                {
                    _logger.LogInformation("Stopped {Path}: the browser left the page", path);
                    return;
                }

                _logger.LogInformation("Stopped {Path}: {NewPath} was opened in the same tab", path, load?.StoppedFor);
                if (!context.Response.HasStarted)
                {
                    // Normally nobody sees this: the tab has already moved on.
                    context.Response.Clear();
                    context.Response.StatusCode = StatusCodes.Status409Conflict;
                    context.Response.ContentType = "text/html; charset=utf-8";
                    var again = WebUtility.HtmlEncode(path);
                    await context.Response.WriteAsync(
                        "<!doctype html><meta charset=utf-8><meta name=viewport content=\"width=device-width\">" +
                        "<title>Page stopped</title><body style=\"font:15px system-ui,sans-serif;padding:24px\">" +
                        "<p>This page was stopped because another page was opened.</p>" +
                        $"<p><a href=\"{again}\">Open it again</a></p></body>");
                }
            }
            finally
            {
                cancellation.Token = CancellationToken.None;
                if (load != null) _registry.End(load);
            }
        }
    }
}
