using Ranalo.Models;
using Ranalo.Services;
using static Ranalo.Services.LossGivenDefault;

namespace Ranolo.Web.Tests
{
    public class LossGivenDefaultTests
    {
        private static Account Phone(decimal? cost, decimal paid, decimal resale = 0,
            string dealer = "Dealer A", bool writtenOff = false, decimal commissions = 0, decimal repossession = 0) =>
            new(dealer, cost, paid, resale, writtenOff, 0, commissions, repossession);

        [Test]
        public void Loss_is_what_we_put_in_less_what_came_back()
        {
            // KES 10,000 phone + 1,000 commission + 500 to repossess; the
            // customer paid a 3,000 deposit and 2,000 more, resold for 4,000.
            Assert.That(Loss(Phone(10000m, 5000m, resale: 4000m, commissions: 1000m, repossession: 500m)), Is.EqualTo(2500m));
        }

        [Test]
        public void Paying_back_what_we_put_in_is_no_loss()
        {
            Assert.That(Loss(Phone(10000m, 11000m, commissions: 1000m)), Is.EqualTo(0m));
            Assert.That(Loss(Phone(10000m, 14000m)), Is.EqualTo(0m));
        }

        [Test]
        public void LGD_is_loss_over_what_we_put_in_weighted_by_money()
        {
            // 30,000 phone lost 15,000; 10,000 phone + 2,000 commission lost
            // nothing. Weighted: 15,000 / 42,000.
            var summary = Summarise(new[] { Phone(30000m, 15000m), Phone(10000m, 12000m, commissions: 2000m) });

            Assert.That(summary.Loss, Is.EqualTo(15000m));
            Assert.That(summary.Cost, Is.EqualTo(42000m));
            Assert.That(summary.LgdPct, Is.EqualTo(35.7m));
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

        private static readonly DateTime NowKenya = new(2026, 10, 28, 12, 0, 0);

        private static DashboardAccountDetailRow Row(long contractId, decimal paid, int daysSincePayment,
            bool restructured = false) => new()
        {
            ContractId = contractId,
            DealerName = "A",
            BuyingPrice = 10000m,
            TotalPaid = paid,
            FullContractValue = 25000m,
            LastPaymentDate = NowKenya.AddDays(-daysSincePayment),
            IsManuallyRestructured = restructured,
        };

        [Test]
        public void Defaulted_is_over_90_days_plus_written_off_contracts_once_each()
        {
            var over90 = Row(1, 3000m, 120);
            var writtenOff = Row(2, 1000m, 400);
            var both = Row(3, 0m, 400);
            var current = Row(4, 5000m, 2);
            var costs = new Dictionary<long, ContractLossCostRow>
            {
                [1] = new() { ContractId = 1, CommissionsPaid = 500m },
                [2] = new() { ContractId = 2, IsWrittenOff = true, ResaleValue = 2000m },
                [3] = new() { ContractId = 3, IsWrittenOff = true },
                [4] = new() { ContractId = 4, CommissionsPaid = 700m },
            };

            var (defaulted, leftOut) = DashboardReportService.DefaultedAccounts(
                new List<DashboardAccountDetailRow> { over90, writtenOff, both, current },
                new List<DashboardAccountDetailRow> { over90, both },
                costs, NowKenya);

            Assert.That(defaulted, Has.Count.EqualTo(3));
            Assert.That(defaulted.Count(a => a.WrittenOff), Is.EqualTo(2));
            // 10,000 + 500 commission - 3,000 = 7,500; 10,000 - 1,000 - 2,000 resale = 7,000; 10,000.
            Assert.That(defaulted.Sum(a => Loss(a)), Is.EqualTo(24500m));
            Assert.That(leftOut.Paying, Is.EqualTo(0));
        }

        [Test]
        public void Anyone_paying_in_the_last_30_days_is_not_a_loss_yet()
        {
            var payingRestructured = Row(1, 3000m, 10, restructured: true);
            var payingOver90 = Row(2, 3000m, 29);
            var stoppedRestructured = Row(3, 3000m, 31, restructured: true);
            var costs = new Dictionary<long, ContractLossCostRow>();

            var (defaulted, leftOut) = DashboardReportService.DefaultedAccounts(
                new List<DashboardAccountDetailRow> { payingRestructured, payingOver90, stoppedRestructured },
                new List<DashboardAccountDetailRow> { payingRestructured, payingOver90, stoppedRestructured },
                costs, NowKenya);

            // A restructured customer who stopped paying is a loss.
            Assert.That(defaulted, Has.Count.EqualTo(1));
            Assert.That(leftOut.Paying, Is.EqualTo(2));
            Assert.That(leftOut.RestructuredPaying, Is.EqualTo(1));
        }

        [Test]
        public void Repossessed_write_off_stays_a_loss_when_the_new_customer_pays()
        {
            var repossessed = Row(1, 6000m, 3);
            var costs = new Dictionary<long, ContractLossCostRow>
            {
                [1] = new() { ContractId = 1, IsWrittenOff = true, WriteOffReason = "Repossessed", ResaleValue = 1000m },
            };

            var (defaulted, _) = DashboardReportService.DefaultedAccounts(
                new List<DashboardAccountDetailRow> { repossessed }, new List<DashboardAccountDetailRow>(), costs, NowKenya);

            Assert.That(defaulted, Has.Count.EqualTo(1));
        }

        [Test]
        public void Snapshot_counts_real_losses_and_paid_share_of_contract()
        {
            var summary = Summarise(new[]
            {
                Phone(10000m, 2500m) with { ContractValue = 25000m },
                Phone(10000m, 12000m) with { ContractValue = 25000m },
                Phone(null, 0m) with { ContractValue = 25000m },
            });

            Assert.That(summary.LossMaking, Is.EqualTo(1));
            Assert.That(summary.Covered, Is.EqualTo(1));
            // Costed accounts only: 14,500 / 50,000.
            Assert.That(summary.PaidPctOfContract, Is.EqualTo(29m));
        }
    }
}
