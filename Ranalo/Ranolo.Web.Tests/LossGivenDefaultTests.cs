using Ranalo.Models;
using Ranalo.Services;
using static Ranalo.Services.LossGivenDefault;

namespace Ranolo.Web.Tests
{
    public class LossGivenDefaultTests
    {
        private static Account Phone(decimal? cost, decimal paid, decimal commissions = 0, decimal resale = 0,
            decimal repossession = 0, string dealer = "Dealer A", bool writtenOff = false) =>
            new(dealer, cost, paid, commissions, repossession, resale, writtenOff);

        [Test]
        public void Deposit_and_payments_reduce_the_loss()
        {
            // KES 10,000 phone, KES 1,000 commission; customer paid a 3,000
            // deposit and 2,000 more: we're 6,000 short.
            Assert.That(Loss(Phone(10000m, 5000m, commissions: 1000m)), Is.EqualTo(6000m));
        }

        [Test]
        public void Paying_more_than_the_phone_cost_is_no_loss()
        {
            Assert.That(Loss(Phone(10000m, 14000m)), Is.EqualTo(0m));
        }

        [Test]
        public void Resale_and_repossession_cost_count()
        {
            Assert.That(Loss(Phone(10000m, 2000m, resale: 4000m, repossession: 500m)), Is.EqualTo(4500m));
        }

        [Test]
        public void LGD_is_weighted_by_phone_cost()
        {
            // 30,000 phone lost 15,000 (50%); 10,000 phone lost nothing.
            // Weighted: 15,000 / 40,000 = 37.5% (a simple average would say 25%).
            var summary = Summarise(new[] { Phone(30000m, 15000m), Phone(10000m, 12000m) });

            Assert.That(summary.LgdPct, Is.EqualTo(37.5m));
            Assert.That(summary.Loss, Is.EqualTo(15000m));
            Assert.That(summary.PhoneCost, Is.EqualTo(40000m));
        }

        [Test]
        public void Missing_phone_cost_is_counted_not_guessed()
        {
            var summary = Summarise(new[] { Phone(10000m, 4000m), Phone(null, 0m), Phone(0m, 0m) });

            Assert.That(summary.Defaulted, Is.EqualTo(3));
            Assert.That(summary.MissingCost, Is.EqualTo(2));
            Assert.That(summary.LgdPct, Is.EqualTo(60m));
        }

        [Test]
        public void No_defaults_is_0_percent()
        {
            Assert.That(Summarise(Array.Empty<Account>()).LgdPct, Is.EqualTo(0m));
        }

        [Test]
        public void By_dealer_splits_the_defaulted_accounts()
        {
            var byDealer = ByDealer(new[]
            {
                Phone(10000m, 2000m, dealer: "A"),
                Phone(10000m, 10000m, dealer: "B"),
                Phone(10000m, 0m, dealer: " "),
            });

            Assert.That(byDealer.Keys, Is.EquivalentTo(new[] { "A", "B" }));
            Assert.That(byDealer["A"].LgdPct, Is.EqualTo(80m));
            Assert.That(byDealer["B"].LgdPct, Is.EqualTo(0m));
        }

        [Test]
        public void Defaulted_is_bad_accounts_plus_written_off_contracts_once_each()
        {
            var bad = new DashboardAccountDetailRow { ContractId = 1, DealerName = "A", BuyingPrice = 10000m, TotalPaid = 3000m };
            var writtenOff = new DashboardAccountDetailRow { ContractId = 2, DealerName = "A", BuyingPrice = 8000m, TotalPaid = 1000m };
            var badAndWrittenOff = new DashboardAccountDetailRow { ContractId = 3, DealerName = "B", BuyingPrice = 9000m, TotalPaid = 0m };
            var paying = new DashboardAccountDetailRow { ContractId = 4, DealerName = "B", BuyingPrice = 9000m, TotalPaid = 5000m };
            var costs = new Dictionary<long, ContractLossCostRow>
            {
                [1] = new() { ContractId = 1, CommissionsPaid = 500m },
                [2] = new() { ContractId = 2, IsWrittenOff = true, ResaleValue = 2000m },
                [3] = new() { ContractId = 3, IsWrittenOff = true },
                [4] = new() { ContractId = 4, CommissionsPaid = 700m },
            };

            var defaulted = DashboardReportService.DefaultedAccounts(
                new List<DashboardAccountDetailRow> { bad, writtenOff, badAndWrittenOff, paying },
                new List<DashboardAccountDetailRow> { bad, badAndWrittenOff },
                costs);

            Assert.That(defaulted, Has.Count.EqualTo(3));
            Assert.That(defaulted.Count(a => a.WrittenOff), Is.EqualTo(2));
            // 10,000 + 500 - 3,000 = 7,500; 8,000 - 1,000 - 2,000 = 5,000; 9,000.
            Assert.That(defaulted.Sum(a => Loss(a)), Is.EqualTo(21500m));
        }
    }
}
