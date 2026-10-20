using Ranalo.Models;
using Ranalo.Services;

namespace Ranolo.Web.Tests
{
    // Reports that filter in memory (Auto Restructured, Arrears, Collections)
    // fetch the whole book, filter, then page (2026-10-20).
    public class QualifyingPageTests
    {
        private static ApplicationReportService Service() => new(null!, null!, null!, null!);

        private static Func<int, int, Task<List<MobileStatusReport>>> Book(int accounts, List<int>? fetches = null) =>
            (page, size) =>
            {
                fetches?.Add(page);
                var rows = Enumerable.Range(1, accounts)
                    .Skip((page - 1) * size).Take(size)
                    .Select(i => new MobileStatusReport { AccountNo = i, Arrears = i % 2 == 0 ? -100m : 50m })
                    .ToList();
                return Task.FromResult(rows);
            };

        [Test]
        public async Task InArrearsOnly_PagedAfterFiltering()
        {
            // 10 accounts, the even ones in arrears.
            var (total, page) = await Service().GetQualifyingPageAsync(1, 3, true, false, false, Book(10));

            Assert.That(total, Is.EqualTo(5));
            Assert.That(page.Select(p => p.AccountNo), Is.EqualTo(new[] { 2, 4, 6 }));
        }

        [Test]
        public async Task ExcludedAccounts_LeftOutOfTheCountAndThePage()
        {
            // 4 is on a manual restructure.
            var (total, page) = await Service().GetQualifyingPageAsync(1, 3, true, false, false, Book(10),
                excludeAccounts: new HashSet<long> { 4 });

            Assert.That(total, Is.EqualTo(4));
            Assert.That(page.Select(p => p.AccountNo), Is.EqualTo(new[] { 2, 6, 8 }));
        }

        [Test]
        public async Task ShortBatch_IsTheLastFetch()
        {
            var fetches = new List<int>();
            await Service().GetQualifyingPageAsync(1, 10, false, false, false, Book(10, fetches));

            Assert.That(fetches, Is.EqualTo(new[] { 1 }));
        }
    }
}
