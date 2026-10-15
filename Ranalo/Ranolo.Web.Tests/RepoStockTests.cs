using Ranalo.Models;
using Ranalo.Services;

namespace Ranolo.Web.Tests
{
    // Repo stock: phones taken back and sold again (agreed 2026-10-15).
    public class RepoStockRulesTests
    {
        private static RepoStockLookup Account(int? contractId = 704, int? openCase = null, bool inStock = false, int timesBefore = 0) => new()
        {
            AccountNo = 8478363, ContractId = contractId, CustomerName = "Sammy", Imei = "352367242606595",
            Make = "TECNO", Model = "TECNO KM5", LockGroup = 3, DeviceGroupId = 1234,
            OpenCollectionsCaseId = openCase, InStockNow = inStock, TimesInStockBefore = timesBefore,
        };

        private static RepoSale Sale() => new()
        {
            CustomerName = "New Buyer", Deposit = 2000, Daily = 80, TermInMonths = 12, DealerReference = 1234,
        };

        private static RepoStockItem InStock() => new() { Id = 1, AccountNo = 8478363, Status = RepoStockStatus.InStock };

        [TestCase(0, "_R")]
        [TestCase(1, "_R2")]
        [TestCase(2, "_R3")]
        public void EachTimeInStockGetsItsOwnPaymentSuffix(int timesBefore, string suffix)
        {
            Assert.That(RepoStockRules.PaymentSuffix(timesBefore), Is.EqualTo(suffix));
        }

        [Test]
        public void AnAccountWithAnOpenContractCanBeAdded()
        {
            Assert.That(RepoStockRules.CannotAdd(Account(), RepoStockReason.Repossessed), Is.Null);
            Assert.That(RepoStockRules.CannotAdd(Account(), RepoStockReason.Returned), Is.Null, "up-to-date customers can hand phones back");
        }

        [Test]
        public void AnAccountWhoseContractAlreadyEndedCanBeAdded()
        {
            // 8478363: contract 704 ended on 14/10 before repo stock existed.
            var account = Account();
            account.ContractEndedOn = new DateTime(2026, 10, 14);
            Assert.That(RepoStockRules.CannotAdd(account, RepoStockReason.Repossessed), Is.Null);
        }

        [Test]
        public void AddingIsRefusedWhenItCantBeDoneCleanly()
        {
            Assert.Multiple(() =>
            {
                Assert.That(RepoStockRules.CannotAdd(null, RepoStockReason.Repossessed), Does.Contain("No device"));
                Assert.That(RepoStockRules.CannotAdd(Account(inStock: true), RepoStockReason.Repossessed), Does.Contain("already in repo stock"));
                Assert.That(RepoStockRules.CannotAdd(Account(contractId: null), RepoStockReason.Repossessed), Does.Contain("no contract"));
                Assert.That(RepoStockRules.CannotAdd(Account(openCase: 338), RepoStockReason.Repossessed), Does.Contain("collections case (338)"));
                Assert.That(RepoStockRules.CannotAdd(Account(), "Stolen"), Does.Contain("Choose why"));
            });
        }

        [Test]
        public void ASaleNeedsTheBuyerTermsAndDealer()
        {
            Assert.That(RepoStockRules.CannotSell(InStock(), Sale()), Is.Null);

            Assert.Multiple(() =>
            {
                Assert.That(RepoStockRules.CannotSell(InStock(), new RepoSale { Daily = 80, TermInMonths = 12, DealerReference = 1 }), Does.Contain("buyer's name"));
                var noDaily = Sale(); noDaily.Daily = 0;
                Assert.That(RepoStockRules.CannotSell(InStock(), noDaily), Does.Contain("daily rate"));
                var badTerm = Sale(); badTerm.TermInMonths = 0;
                Assert.That(RepoStockRules.CannotSell(InStock(), badTerm), Does.Contain("term"));
                var noDealer = Sale(); noDealer.DealerReference = 0;
                Assert.That(RepoStockRules.CannotSell(InStock(), noDealer), Does.Contain("dealer"));
                var negative = Sale(); negative.Deposit = -1;
                Assert.That(RepoStockRules.CannotSell(InStock(), negative), Does.Contain("negative"));
            });
        }

        [Test]
        public void ASoldPhoneCantBeSoldAgain()
        {
            var sold = InStock();
            sold.Status = RepoStockStatus.Sold;
            Assert.That(RepoStockRules.CannotSell(sold, Sale()), Does.Contain("isn't in stock"));
        }

        [Test]
        public void ContractTotalsUseThirtyDayMonths()
        {
            Assert.That(RepoStockRules.TotalLoan(80, 12), Is.EqualTo(28800m));
            Assert.That(RepoStockRules.TotalCost(2000, 80, 12), Is.EqualTo(30800m));
            Assert.That(RepoStockRules.TotalLoan(100, 14), Is.EqualTo(42000m));
        }

        [Test]
        public void WriteOffIsTheUnpaidBalanceNeverNegative()
        {
            Assert.That(RepoStockRules.WriteOff(new RepoOldContract { ContractValue = 50870.25m, TotalPaid = 12000m }), Is.EqualTo(38870.25m));
            Assert.That(RepoStockRules.WriteOff(new RepoOldContract { ContractValue = 30000m, TotalPaid = 31000m }), Is.EqualTo(0m), "overpaid: nothing to write off");
        }

        [Test]
        public void TheOldCustomersPaymentsLiveUnderTheirSuffix()
        {
            var item = new RepoStockItem { AccountNo = 8478363, PaymentSuffix = "_R2" };
            Assert.That(item.OldPaymentAccount, Is.EqualTo("8478363_R2"));
        }
    }
}
