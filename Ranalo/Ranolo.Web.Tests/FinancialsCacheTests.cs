using Microsoft.Extensions.DependencyInjection;
using Ranalo.Models;
using Ranalo.ScheduledServices;
using Ranalo.Services;

namespace Ranolo.Web.Tests
{
    // This month's Financials page: worked out daily at 04:00 Kenya time and
    // shared; any other period or date range is worked out live.
    public class FinancialsCacheTests
    {
        private class CountingBuilder : IFinancialsBuilder
        {
            public int Builds;
            public string? LastPeriod;

            public async Task<FinancialsViewModel> BuildAsync(string? period, DateTime? from, DateTime? to)
            {
                Interlocked.Increment(ref Builds);
                LastPeriod = period;
                await Task.Delay(100);
                return new FinancialsViewModel { Period = period ?? "month", BuiltAtUtc = DateTime.UtcNow };
            }
        }

        private CountingBuilder _builder = null!;
        private FinancialsCache _cache = null!;

        [SetUp]
        public void SetUp()
        {
            _builder = new CountingBuilder();
            var services = new ServiceCollection()
                .AddSingleton<IFinancialsBuilder>(_builder)
                .BuildServiceProvider();
            _cache = new FinancialsCache(services.GetRequiredService<IServiceScopeFactory>());
        }

        [Test]
        public async Task ManyOpensAtOnce_OneBuild()
        {
            var models = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => _cache.GetAsync()));
            Assert.That(_builder.Builds, Is.EqualTo(1));
            Assert.That(models.Distinct().Count(), Is.EqualTo(1));
            Assert.That(_builder.LastPeriod, Is.EqualTo("month"));
        }

        [Test]
        public async Task Reused_UntilRefreshed()
        {
            var first = await _cache.GetAsync();
            Assert.That(await _cache.GetAsync(), Is.SameAs(first));
            Assert.That(_builder.Builds, Is.EqualTo(1));

            var refreshed = await _cache.GetAsync(refresh: true);
            Assert.That(_builder.Builds, Is.EqualTo(2));
            Assert.That(refreshed, Is.Not.SameAs(first));
        }

        [Test]
        public void Fresh_ForADayAndAnHour()
        {
            var now = new DateTime(2026, 11, 19, 9, 0, 0, DateTimeKind.Utc);
            Assert.That(FinancialsCache.IsFresh(new FinancialsViewModel { BuiltAtUtc = now.AddHours(-24) }, now), Is.True);
            Assert.That(FinancialsCache.IsFresh(new FinancialsViewModel { BuiltAtUtc = now.AddHours(-26) }, now), Is.False);
            Assert.That(FinancialsCache.IsFresh(new FinancialsViewModel(), now), Is.False);
        }

        [TestCase(null, null, null, true)]           // opening the page
        [TestCase("month", null, null, true)]        // the Month button
        [TestCase("MONTH", null, null, true)]
        [TestCase("custom", null, null, true)]       // custom without dates falls back to month
        [TestCase("week", null, null, false)]
        [TestCase("ytd", null, null, false)]
        [TestCase("all", null, null, false)]
        [TestCase("custom", "2025-01-01", "2025-12-31", false)]
        public void OnlyThisMonthUsesTheDailyCopy(string? period, string? from, string? to, bool daily)
        {
            DateTime? f = from == null ? null : DateTime.Parse(from);
            DateTime? t = to == null ? null : DateTime.Parse(to);
            Assert.That(FinancialsBuilder.IsDefaultView(period, f, t), Is.EqualTo(daily));
        }

        [TestCase("2026-11-19 00:30", "2026-11-19 01:00")] // 03:30 Kenya -> 04:00 today
        [TestCase("2026-11-19 01:00", "2026-11-20 01:00")] // exactly 04:00 -> tomorrow
        [TestCase("2026-11-19 09:00", "2026-11-20 01:00")] // midday -> tomorrow
        [TestCase("2026-11-30 22:00", "2026-12-01 01:00")] // 01:00 Kenya on the 1st -> 04:00 that day
        public void RunsAt4amKenyaTime(string nowUtc, string expectedUtc)
        {
            Assert.That(ScheduledFinancialsRefresh.NextRunUtc(DateTime.Parse(nowUtc)), Is.EqualTo(DateTime.Parse(expectedUtc)));
        }
    }
}
