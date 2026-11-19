using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace Ranalo.Services
{
    // Scoped: the "stop" signal for the database reads of this request (see
    // CancellableDbConnection). None unless PageLoadMiddleware sets it, so
    // background jobs and shared cache builds, which make their own scope,
    // are never stopped by someone leaving a page.
    public class RequestCancellation
    {
        public CancellationToken Token { get; set; } = CancellationToken.None;
    }

    public static class PageLoadRules
    {
        public const string TabCookie = "rn_tab";

        // Set by click-guard.js; "new" (or nothing) when the page is opening
        // in a new tab, which mustn't stop the page loading in the old one.
        private static readonly Regex TabId = new("^[a-z0-9]{8,40}$", RegexOptions.Compiled);

        // Only reads can be stopped: a save (POST) always runs to the end.
        public static bool IsRead(string method) =>
            HttpMethods.IsGet(method) || HttpMethods.IsHead(method);

        // A whole page opened by a click, a typed address or a form, as
        // opposed to a page's own background requests for its charts and
        // tables, several of which are meant to run at once. Browsers mark
        // these with Sec-Fetch-Mode: navigate.
        public static bool IsPageOpen(string? secFetchMode) =>
            string.Equals(secFetchMode, "navigate", StringComparison.OrdinalIgnoreCase);

        // Page loads are matched per signed-in user AND per browser tab, so
        // opening an account in a second tab doesn't stop the first tab.
        // Null: not tracked (not signed in, or no usable tab id).
        public static string? Key(int? userId, string? tabCookie)
        {
            if (userId is not > 0 || tabCookie == null || !TabId.IsMatch(tabCookie)) return null;
            return userId + ":" + tabCookie;
        }
    }

    // The page each user/tab is loading right now. Opening another page in
    // the same tab stops the one before it.
    public class PageLoadRegistry
    {
        public sealed class PageLoad
        {
            internal PageLoad(string key, string path)
            {
                Key = key;
                Path = path;
            }

            internal readonly CancellationTokenSource Source = new();
            public string Key { get; }
            public string Path { get; }
            public string? StoppedFor { get; internal set; }
            public CancellationToken Token => Source.Token;
        }

        private readonly ConcurrentDictionary<string, PageLoad> _current = new();

        // A new page open for this key: stops the one already loading.
        // Returns the new page load (pass it to End when it's done).
        public PageLoad Begin(string key, string path)
        {
            var load = new PageLoad(key, path);
            PageLoad? previous = null;
            _current.AddOrUpdate(key, load, (_, old) => { previous = old; return load; });
            Stop(previous, path);
            return load;
        }

        // A save in this tab: the page loading before it isn't wanted any
        // more, but the save itself is never stopped, so isn't recorded.
        public void StopCurrent(string key, string path)
        {
            if (_current.TryRemove(key, out var previous)) Stop(previous, path);
        }

        public void End(PageLoad load)
        {
            // Only if it's still the current one (a newer one may have replaced it).
            _current.TryRemove(new KeyValuePair<string, PageLoad>(load.Key, load));
            load.Source.Dispose();
        }

        public int Count => _current.Count;

        private static void Stop(PageLoad? load, string newPath)
        {
            if (load == null) return;
            load.StoppedFor = newPath;
            // Never let stopping the old page break the new one: it may have
            // finished meanwhile (disposed), or SqlCommand.Cancel may throw.
            try { load.Source.Cancel(); }
            catch (Exception) { }
        }
    }
}
