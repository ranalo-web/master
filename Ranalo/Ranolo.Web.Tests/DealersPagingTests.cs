using NUnit.Framework;
using Dealer = Ranalo.DataStore.Dealer;
using Ranalo.Models;

namespace Ranolo.Web.Tests
{
    public class DealersPagingTests
    {
        private static List<Dealer> Dealers(int n) =>
            Enumerable.Range(1, n).Select(i => new Dealer { DealerId = i, CompanyName = i == 7 ? "Renalo Shop" : $"Dealer {i}", DealerReference = $"R{i}" }).ToList();

        [Test]
        public void Page_SplitsNewestFirst()
        {
            var vm = DealersViewModel.Page(Dealers(45), 2, 20, "");
            Assert.That(vm.TotalPages, Is.EqualTo(3));
            Assert.That(vm.TotalRecords, Is.EqualTo(45));
            Assert.That(vm.Dealers!.First().DealerId, Is.EqualTo(25));
            Assert.That(vm.Dealers!.Count, Is.EqualTo(20));
        }

        [Test]
        public void Page_Search_CaseInsensitive()
        {
            var vm = DealersViewModel.Page(Dealers(45), 1, 20, " renalo ");
            Assert.That(vm.Dealers!.Single().DealerId, Is.EqualTo(7));
            Assert.That(vm.SearchTerm, Is.EqualTo("renalo"));
        }

        [Test]
        public void Page_OutOfRange_AndOddSize_AreFixed()
        {
            var vm = DealersViewModel.Page(Dealers(5), 9, 7, "");
            Assert.That(vm.PageSize, Is.EqualTo(20));
            Assert.That(vm.CurrentPage, Is.EqualTo(1));
            Assert.That(DealersViewModel.Page(new List<Dealer>(), 1, 20, "x").TotalPages, Is.EqualTo(1));
        }
    }
}
