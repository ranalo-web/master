using System.Data;
using System.Data.SqlClient;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Ranalo;
using Ranalo.DataStore;
using Ranalo.DataStore.DataModels;
using Ranalo.Services;

namespace Ranolo.Web.Tests
{
    // A page nobody's waiting for any more has its database reads stopped:
    // the browser left, or the same user opened another page in the same tab.
    public class PageLoadTests
    {
        private const string Tab = "abc12345tab";

        [Test]
        public void Rules_OnlyPageOpensWithAUserAndATabAreTracked()
        {
            Assert.That(PageLoadRules.IsPageOpen("navigate"), Is.True);
            Assert.That(PageLoadRules.IsPageOpen("cors"), Is.False);     // a page's own fetch()
            Assert.That(PageLoadRules.IsPageOpen(null), Is.False);

            Assert.That(PageLoadRules.Key(7, Tab), Is.EqualTo("7:" + Tab));
            Assert.That(PageLoadRules.Key(null, Tab), Is.Null);          // not signed in
            Assert.That(PageLoadRules.Key(7, null), Is.Null);            // no tab cookie
            Assert.That(PageLoadRules.Key(7, "new"), Is.Null);           // opening in a new tab
            Assert.That(PageLoadRules.Key(7, "x;y=1"), Is.Null);

            Assert.That(PageLoadRules.IsRead("GET"), Is.True);
            Assert.That(PageLoadRules.IsRead("POST"), Is.False);
        }

        [Test]
        public void Registry_NewPageInSameTabStopsTheOldOne_OtherTabsAndUsersUntouched()
        {
            var reg = new PageLoadRegistry();
            var first = reg.Begin("7:" + Tab, "/financials");
            var otherTab = reg.Begin("7:othertab99", "/statusreport");
            var otherUser = reg.Begin("8:" + Tab, "/approver");

            var second = reg.Begin("7:" + Tab, "/restructured");

            Assert.That(first.Token.IsCancellationRequested, Is.True);
            Assert.That(first.StoppedFor, Is.EqualTo("/restructured"));
            Assert.That(second.Token.IsCancellationRequested, Is.False);
            Assert.That(otherTab.Token.IsCancellationRequested, Is.False);
            Assert.That(otherUser.Token.IsCancellationRequested, Is.False);

            // The stopped one finishing late doesn't remove the newer one.
            reg.End(first);
            Assert.That(reg.Count, Is.EqualTo(3));
            reg.End(second); reg.End(otherTab); reg.End(otherUser);
            Assert.That(reg.Count, Is.EqualTo(0));
        }

        [Test]
        public void Registry_ASaveStopsThePageLoadingBeforeIt()
        {
            var reg = new PageLoadRegistry();
            var load = reg.Begin("7:" + Tab, "/financials");
            reg.StopCurrent("7:" + Tab, "/payments/save");
            Assert.That(load.Token.IsCancellationRequested, Is.True);
            Assert.That(reg.Count, Is.EqualTo(0));
            Assert.DoesNotThrow(() => reg.End(load));
        }

        // ---- Middleware -------------------------------------------------

        private static DefaultHttpContext Context(string method, string path, bool navigate = true, string? tab = Tab, int? userId = 7)
        {
            var ctx = new DefaultHttpContext();
            ctx.Request.Method = method;
            ctx.Request.Path = path;
            ctx.Response.Body = new MemoryStream();
            if (navigate) ctx.Request.Headers["Sec-Fetch-Mode"] = "navigate";
            if (tab != null) ctx.Request.Headers.Cookie = PageLoadRules.TabCookie + "=" + tab;
            if (userId != null) ctx.Items["UserSettings"] = new User { UserId = userId.Value };
            return ctx;
        }

        // A page whose "query" waits until it's stopped (or 5 s).
        private static RequestDelegate SlowPage(TaskCompletionSource started, RequestCancellation rc) => async _ =>
        {
            started.TrySetResult();
            await Task.Delay(5000, rc.Token);
        };

        [Test]
        public async Task Middleware_SecondPageInSameTab_StopsFirst()
        {
            var reg = new PageLoadRegistry();
            var rc1 = new RequestCancellation();
            var started = new TaskCompletionSource();
            var mw1 = new PageLoadMiddleware(SlowPage(started, rc1), reg, NullLogger<PageLoadMiddleware>.Instance);
            var ctx1 = Context("GET", "/financials");
            var first = mw1.InvokeAsync(ctx1, rc1);
            await started.Task;

            var mw2 = new PageLoadMiddleware(_ => Task.CompletedTask, reg, NullLogger<PageLoadMiddleware>.Instance);
            await mw2.InvokeAsync(Context("GET", "/restructured"), new RequestCancellation());

            var done = await Task.WhenAny(first, Task.Delay(2000));
            Assert.That(done, Is.SameAs(first), "first page should stop at once, not run its 5 s");
            await first;
            Assert.That(ctx1.Response.StatusCode, Is.EqualTo(409));
            Assert.That(rc1.Token.CanBeCanceled, Is.False, "reset after the request");
            Assert.That(reg.Count, Is.EqualTo(0));
        }

        [TestCase("POST", true, Tab, 7)]     // a save is never stopped
        [TestCase("GET", false, Tab, 7)]     // a page's own background fetch
        [TestCase("GET", true, "new", 7)]    // a page opening in a new tab
        [TestCase("GET", true, null, 7)]     // no tab cookie
        [TestCase("GET", true, Tab, null)]   // not signed in
        public async Task Middleware_NotStoppedByAnotherPage(string method, bool navigate, string? tab, int? userId)
        {
            var reg = new PageLoadRegistry();
            var rc1 = new RequestCancellation();
            var started = new TaskCompletionSource();
            var release = new TaskCompletionSource();
            RequestDelegate page = async _ =>
            {
                started.TrySetResult();
                await Task.WhenAny(release.Task, Task.Delay(Timeout.Infinite, rc1.Token));
                rc1.Token.ThrowIfCancellationRequested();
            };
            var first = new PageLoadMiddleware(page, reg, NullLogger<PageLoadMiddleware>.Instance)
                .InvokeAsync(Context(method, "/first", navigate, tab, userId), rc1);
            await started.Task;

            await new PageLoadMiddleware(_ => Task.CompletedTask, reg, NullLogger<PageLoadMiddleware>.Instance)
                .InvokeAsync(Context("GET", "/second"), new RequestCancellation());

            await Task.Delay(100);
            Assert.That(first.IsCompleted, Is.False, "first page must keep running");
            release.SetResult();
            await first;
        }

        [Test]
        public async Task Middleware_BrowserLeft_StopsReads()
        {
            var reg = new PageLoadRegistry();
            var rc = new RequestCancellation();
            var started = new TaskCompletionSource();
            var ctx = Context("GET", "/statusreport", navigate: false);
            var aborted = new CancellationTokenSource();
            ctx.RequestAborted = aborted.Token;
            var run = new PageLoadMiddleware(SlowPage(started, rc), reg, NullLogger<PageLoadMiddleware>.Instance)
                .InvokeAsync(ctx, rc);
            await started.Task;
            aborted.Cancel();
            Assert.That(await Task.WhenAny(run, Task.Delay(2000)), Is.SameAs(run));
            await run;
        }

        // ---- Connection --------------------------------------------------
        // No database needed: a stopped page doesn't start a read at all,
        // while writes and transactions are never stopped (they get as far
        // as the closed connection complaining).

        private static (CancellableDbConnection, RequestCancellation, CancellationTokenSource) StoppedConnection()
        {
            var rc = new RequestCancellation();
            var cts = new CancellationTokenSource();
            rc.Token = cts.Token;
            cts.Cancel();
            return (new CancellableDbConnection(new SqlConnection("Server=nowhere;Database=x;"), rc), rc, cts);
        }

        [Test]
        public void Connection_StoppedPage_ReadsDoNotStart()
        {
            var (conn, _, _) = StoppedConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT 1";
            Assert.Throws<OperationCanceledException>(() => cmd.ExecuteReader());
            Assert.Throws<OperationCanceledException>(() => cmd.ExecuteScalar());
            Assert.ThrowsAsync<OperationCanceledException>(() => ((System.Data.Common.DbCommand)cmd).ExecuteReaderAsync());
        }

        [Test]
        public void Connection_StoppedPage_WritesStillRun()
        {
            var (conn, _, _) = StoppedConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE x SET y = 1";
            // Not cancelled: it goes on to the (closed) connection.
            Assert.Throws<InvalidOperationException>(() => cmd.ExecuteNonQuery());
        }

        [Test]
        public void Connection_PassesThroughToSqlConnection()
        {
            var rc = new RequestCancellation();
            using var conn = new CancellableDbConnection(new SqlConnection("Server=nowhere;Database=RanDitDB;"), rc);
            Assert.That(conn.Database, Is.EqualTo("RanDitDB"));
            Assert.That(conn.State, Is.EqualTo(ConnectionState.Closed));
            using var cmd = conn.CreateCommand();
            Assert.That(cmd.Connection, Is.SameAs(conn));
            var p = cmd.CreateParameter();
            Assert.That(p, Is.InstanceOf<SqlParameter>());
        }
    }
}
