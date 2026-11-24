using NUnit.Framework;
using Ranalo.Services;
using Dealer = Ranalo.DataStore.Dealer;

namespace Ranolo.Web.Tests
{
    public class DealerRulesTests
    {
        [TestCase("FaceBook (closed)", true)]
        [TestCase("Jones (Closed)  ", true)]
        [TestCase("Ranalo Credit", false)]
        [TestCase("Closed Shop", false)]
        [TestCase(null, false)]
        public void IsClosed_ByNameSuffix(string? name, bool closed) =>
            Assert.That(DealerRules.IsClosed(name), Is.EqualTo(closed));

        [Test]
        public void Open_LeavesOutClosedDealers()
        {
            var dealers = new[]
            {
                new Dealer { DealerId = 1, CompanyName = "FaceBook (closed)" },
                new Dealer { DealerId = 2, CompanyName = "Jacksolo Group" },
                new Dealer { DealerId = 11, CompanyName = "Ranalo Credit" },
            };
            Assert.That(DealerRules.Open(dealers).Select(d => d.DealerId), Is.EqualTo(new[] { 2, 11 }));
            Assert.That(DealerRules.Open(null), Is.Empty);
        }

        [Test]
        public void DealersPage_HidesClosedUnlessAsked()
        {
            var dealers = new[]
            {
                new Dealer { DealerId = 1, CompanyName = "FaceBook (closed)" },
                new Dealer { DealerId = 2, CompanyName = "Jacksolo Group" },
            };
            var hidden = Ranalo.Models.DealersViewModel.Page(dealers, 1, 20, "");
            Assert.That(hidden.Dealers!.Select(d => d.DealerId), Is.EqualTo(new[] { 2 }));
            Assert.That(hidden.ClosedCount, Is.EqualTo(1));
            Assert.That(hidden.TotalRecords, Is.EqualTo(1));

            Assert.That(hidden.OpenCount, Is.EqualTo(1));

            // The Closed tab shows only the closed ones.
            var closed = Ranalo.Models.DealersViewModel.Page(dealers, 1, 20, "", showClosed: true);
            Assert.That(closed.Dealers!.Select(d => d.DealerId), Is.EqualTo(new[] { 1 }));
        }
    }
}
