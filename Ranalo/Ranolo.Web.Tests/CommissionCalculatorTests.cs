using Ranalo.Services;

namespace Ranolo.Web.Tests
{
    // The single commission formula used by every commission page and card.
    public class CommissionCalculatorTests
    {
        private static CommissionInputs Account(
            decimal deposit = 4_000m, int days = 100, bool pastLock = false, decimal arrears = 0m,
            decimal totalPaid = 12_000m, decimal? buyingPrice = 10_000m, bool hasAgent = true,
            decimal agentPaid = 0m, decimal dealerPaid = 0m) => new()
        {
            Deposit = deposit,
            DaysSinceStart = days,
            IsPastLockDate = pastLock,
            Arrears = arrears,
            TotalPaid = totalPaid,
            BuyingPrice = buyingPrice,
            HasAgent = hasAgent,
            AgentPaid = agentPaid,
            DealerPaid = dealerPaid,
        };

        [Test]
        public void BeforeNinetyDays_AgentGetsUpfrontOnly()
        {
            var c = CommissionCalculator.Calculate(Account(days: 60));

            Assert.That(c.AgentUpfront, Is.EqualTo(2_000m));
            Assert.That(c.AgentBonus, Is.EqualTo(0m));
            Assert.That(c.BonusEarned, Is.False);
            Assert.That(c.DaysToBonus, Is.EqualTo(30));
        }

        [Test]
        public void AfterNinetyDaysAndPerforming_AgentGetsBonus()
        {
            var c = CommissionCalculator.Calculate(Account(days: 100));

            Assert.That(c.AgentEarned, Is.EqualTo(3_000m));
            Assert.That(c.BonusEarned, Is.True);
            Assert.That(c.BonusAtRisk, Is.False);
        }

        [Test]
        public void PastLockDate_BonusHeldAndArrearsDeductedFromAgentAndDealer()
        {
            var c = CommissionCalculator.Calculate(Account(days: 100, pastLock: true, arrears: -800m, totalPaid: 20_000m));

            Assert.That(c.BonusEarned, Is.False);
            Assert.That(c.BonusAtRisk, Is.True);
            Assert.That(c.AgentEarned, Is.EqualTo(2_000m));
            Assert.That(c.AgentArrearsDeducted, Is.EqualTo(800m));
            Assert.That(c.AgentNet, Is.EqualTo(1_200m));
            // Dealer base = 20,000 - 10,000 - 2,000 = 8,000 -> 30% = 2,400, minus 800 arrears.
            Assert.That(c.DealerCommission, Is.EqualTo(2_400m));
            Assert.That(c.DealerArrearsDeducted, Is.EqualTo(800m));
            Assert.That(c.DealerNet, Is.EqualTo(1_600m));
        }

        [Test]
        public void ShortfallBeforeLockDate_IsNotDeducted()
        {
            // A restructured account on a new plan: behind on the original schedule, lock date still ahead.
            var c = CommissionCalculator.Calculate(Account(pastLock: false, arrears: -800m));

            Assert.That(c.AgentArrearsDeducted, Is.EqualTo(0m));
            Assert.That(c.DealerArrearsDeducted, Is.EqualTo(0m));
            Assert.That(c.BonusEarned, Is.True);
        }

        [Test]
        public void DealerCommission_GrowsWithEachPaymentAndNeverGoesNegative()
        {
            var early = CommissionCalculator.Calculate(Account(totalPaid: 12_000m));
            var later = CommissionCalculator.Calculate(Account(totalPaid: 20_000m));

            // 12,000 - 10,000 - 3,000 = -1,000 -> nothing yet.
            Assert.That(early.DealerBase, Is.EqualTo(-1_000m));
            Assert.That(early.DealerCommission, Is.EqualTo(0m));
            // 20,000 - 10,000 - 3,000 = 7,000 -> 30% = 2,100.
            Assert.That(later.DealerCommission, Is.EqualTo(2_100m));
        }

        [Test]
        public void MissingBuyingPrice_NoDealerCommissionOrDeduction()
        {
            var c = CommissionCalculator.Calculate(Account(buyingPrice: null, pastLock: true, arrears: -500m));

            Assert.That(c.DealerCommission, Is.Null);
            Assert.That(c.DealerBalance, Is.Null);
            Assert.That(c.DealerArrearsDeducted, Is.EqualTo(0m));
            Assert.That(c.AgentArrearsDeducted, Is.EqualTo(500m));
        }

        [Test]
        public void DirectSale_NoAgentPayoutButAgentCommissionStillCostsTheDealer()
        {
            var c = CommissionCalculator.Calculate(Account(hasAgent: false, totalPaid: 20_000m));

            Assert.That(c.AgentEarned, Is.EqualTo(0m));
            Assert.That(c.AgentNet, Is.EqualTo(0m));
            Assert.That(c.DealerCommission, Is.EqualTo(2_100m));
        }

        [Test]
        public void Pool_CapsWithheldAndFloorsOwed()
        {
            var pool = CommissionCalculator.Pool(new[]
            {
                (Earned: 3_000m, Deducted: 0m, Paid: 1_000m),
                (Earned: 1_000m, Deducted: 5_000m, Paid: 0m),
            });

            Assert.That(pool.Earned, Is.EqualTo(4_000m));
            Assert.That(pool.Withheld, Is.EqualTo(4_000m));
            Assert.That(pool.Owed, Is.EqualTo(0m));
        }
    }
}
