using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Ranalo.DataStore;
using Ranalo.Models;
using Ranalo.Services;

namespace Ranolo.Web.Tests
{
    // One Admin Dashboard shared by every admin: built once however many ask
    // at the same time, reused until it's 30 minutes old or refreshed.
    public class AdminDashboardCacheTests
    {
        // Fake dashboard service: counts builds, each taking a moment.
        public class CountingDashboard : DispatchProxy
        {
            public int Builds;

            protected override object? Invoke(MethodInfo? method, object?[]? args)
            {
                if (method!.Name == nameof(IDashboardReportService.GetAdminDashboardAsync))
                {
                    Interlocked.Increment(ref Builds);
                    var period = args?[0] as string;
                    return Build(period);
                }
                throw new NotSupportedException(method.Name);
            }

            private static async Task<AdminDashboardViewModel> Build(string? period)
            {
                await Task.Delay(100);
                return new AdminDashboardViewModel { Period = period ?? "month" };
            }
        }

        // Write-off figures unavailable: the cards show zeros, the page still builds.
        public class NoWriteOffs : DispatchProxy
        {
            protected override object? Invoke(MethodInfo? method, object?[]? args) => throw new NotSupportedException(method!.Name);
        }

        private CountingDashboard _counter = null!;
        private AdminDashboardCache _cache = null!;

        [SetUp]
        public void SetUp()
        {
            var dashboard = DispatchProxy.Create<IDashboardReportService, CountingDashboard>();
            _counter = (CountingDashboard)(object)dashboard;
            var services = new ServiceCollection()
                .AddSingleton(dashboard)
                .AddSingleton(DispatchProxy.Create<IWriteOffRepository, NoWriteOffs>())
                .BuildServiceProvider();
            _cache = new AdminDashboardCache(services.GetRequiredService<IServiceScopeFactory>(), NullLogger<AdminDashboardCache>.Instance);
        }

        [Test]
        public async Task ManyAdminsAtOnce_OneBuild_SameFigures()
        {
            var models = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => _cache.GetAsync(null)));

            Assert.That(_counter.Builds, Is.EqualTo(1));
            Assert.That(models.Distinct().Count(), Is.EqualTo(1));
            Assert.That(models[0].BuiltAtUtc, Is.Not.Null);
        }

        [Test]
        public async Task Reused_UntilRefreshed()
        {
            var first = await _cache.GetAsync(null);
            var again = await _cache.GetAsync("month"); // a plain visit and ?period=month share one copy
            Assert.That(again, Is.SameAs(first));
            Assert.That(_counter.Builds, Is.EqualTo(1));

            var refreshed = await _cache.GetAsync("month", refresh: true);
            Assert.That(_counter.Builds, Is.EqualTo(2));
            Assert.That(refreshed, Is.Not.SameAs(first));
        }

        [Test]
        public async Task EachPeriod_HasItsOwnCopy()
        {
            await _cache.GetAsync("month");
            await _cache.GetAsync("week");
            await _cache.GetAsync("week");
            Assert.That(_counter.Builds, Is.EqualTo(2));
        }

        [Test]
        public void Fresh_UntilTheNextScheduledRebuild()
        {
            var now = new DateTime(2026, 11, 18, 15, 0, 0, DateTimeKind.Utc);
            // Built at 06:05 Kenya (03:05 UTC): still shown until the 18:05 rebuild.
            Assert.That(AdminDashboardCache.IsFresh(new AdminDashboardViewModel { BuiltAtUtc = now.AddHours(-12) }, now), Is.True);
            Assert.That(AdminDashboardCache.IsFresh(new AdminDashboardViewModel { BuiltAtUtc = now.AddHours(-14) }, now), Is.False);
            Assert.That(AdminDashboardCache.IsFresh(new AdminDashboardViewModel(), now), Is.False);
        }

        [TestCase("2026-11-17 02:00", "2026-11-17 03:05")] // before 06:05 Nairobi
        [TestCase("2026-11-17 03:02", "2026-11-17 03:05")] // between the snapshot and the rebuild
        [TestCase("2026-11-17 03:05", "2026-11-17 15:05")] // next: 18:05 Nairobi
        [TestCase("2026-11-17 16:00", "2026-11-17 21:05")] // next: 00:05 Nairobi
        [TestCase("2026-11-17 21:10", "2026-11-18 03:05")]
        public void Rebuilds_FiveMinutesAfterEachCommissionSnapshot(string nowUtc, string expectedUtc)
        {
            Assert.That(Ranalo.ScheduledServices.ScheduledAdminDashboardRefresh.NextRunUtc(DateTime.Parse(nowUtc)),
                Is.EqualTo(DateTime.Parse(expectedUtc)));
        }
    }
}
