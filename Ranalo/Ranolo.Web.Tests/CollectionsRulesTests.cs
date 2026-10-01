using Ranalo.Services;

namespace Ranolo.Web.Tests
{
    // The collections rules agreed with the business (2026-10-01), using the
    // worked example from that discussion: handed over 10,000 behind, the
    // collector recovers 6,000 (earning 1,200), repossession cost 2,000.
    public class CollectionsRulesTests
    {
        private static CollectionTerms InCollections(decimal frozen = 10_000m, decimal basis = 20_000m) => new()
        {
            FrozenDeduction = frozen,
            CommissionPaidBasis = basis,
        };

        private static CollectionTerms Returned(decimal shortfallAtReturn = 4_000m, decimal totalPaidAtReturn = 26_000m,
            decimal collectorEarned = 1_200m, decimal repoCost = 0m) => new()
        {
            IsReturned = true,
            FrozenDeduction = 10_000m,
            CommissionPaidBasis = 20_000m,
            ShortfallAtReturn = shortfallAtReturn,
            TotalPaidAtReturn = totalPaidAtReturn,
            CollectorEarned = collectorEarned,
            RepossessionCost = repoCost,
        };

        [Test]
        public void CollectorEarnsTwentyPercentOfWhatTheyRecover()
        {
            Assert.That(CollectionsRules.CollectorShare(6_000m), Is.EqualTo(1_200m));
            Assert.That(CollectionsRules.CollectorShare(0m), Is.EqualTo(0m));
        }

        [Test]
        public void EligibleOnlyWhenBehindAndNinetyDaysWithoutPayment()
        {
            Assert.That(CollectionsRules.IsEligible(5_000m, 90), Is.True);
            Assert.That(CollectionsRules.IsEligible(5_000m, 89), Is.False);
            Assert.That(CollectionsRules.IsEligible(0m, 200), Is.False);
        }

        [Test]
        public void HeldNinetyDaysWithoutPayment_IsDueForReassignment()
        {
            Assert.That(CollectionsRules.IsDueForReassignment(90), Is.True);
            Assert.That(CollectionsRules.IsDueForReassignment(89), Is.False);
        }

        [Test]
        public void InCollections_DeductionStaysFrozenWhateverTheCollectorRecovers()
        {
            var t = InCollections();

            Assert.That(CollectionsRules.Deduction(t, liveShortfall: 10_000m), Is.EqualTo(10_000m));
            Assert.That(CollectionsRules.Deduction(t, liveShortfall: 4_000m), Is.EqualTo(10_000m));
            Assert.That(CollectionsRules.Deduction(t, liveShortfall: 0m), Is.EqualTo(10_000m));
        }

        [Test]
        public void Returned_DeductionIsFrozenPlusCollectorEarningsPlusRepossessionCost()
        {
            var t = Returned(repoCost: 2_000m);

            Assert.That(CollectionsRules.Deduction(t, liveShortfall: 4_000m), Is.EqualTo(13_200m));
        }

        [Test]
        public void Returned_NewShortfallAfterTheReturnIsAddedOnTop()
        {
            var t = Returned();

            // 4,000 behind at the return, 7,000 now: 3,000 more.
            Assert.That(CollectionsRules.Deduction(t, liveShortfall: 7_000m), Is.EqualTo(14_200m));
            // Catching up never takes it below the frozen amount + costs.
            Assert.That(CollectionsRules.Deduction(t, liveShortfall: 0m), Is.EqualTo(11_200m));
        }

        [Test]
        public void DealerCommission_CountsOnlyPaymentsOutsideCollections()
        {
            Assert.That(CollectionsRules.DealerCountedPaid(InCollections(basis: 20_000m), liveTotalPaid: 26_000m), Is.EqualTo(20_000m));
            // Returned at 26,000 paid; 1,500 paid since.
            Assert.That(CollectionsRules.DealerCountedPaid(Returned(totalPaidAtReturn: 26_000m), liveTotalPaid: 27_500m), Is.EqualTo(21_500m));
        }

        [Test]
        public void AgentKeepsOnlyBonusAlreadyPaid()
        {
            Assert.That(CollectionsRules.AgentBonusKept(1_000m, 0m), Is.EqualTo(0m));
            Assert.That(CollectionsRules.AgentBonusKept(1_000m, 400m), Is.EqualTo(400m));
            Assert.That(CollectionsRules.AgentBonusKept(1_000m, 1_500m), Is.EqualTo(1_000m));
        }

        [Test]
        public void CollectorCantTakeTheirOwnAccounts()
        {
            var agentCollector = new CollectorCandidate { UserId = 28, DealerId = 14 };
            var dealerCollector = new CollectorCandidate { UserId = 20, DealerId = 14, IsDealerUser = true };

            Assert.That(CollectionsRules.IsConflicted(agentCollector, accountAgentUserId: 28, accountDealerId: 3), Is.True);
            // An agent of the same dealer who didn't sell it may collect it.
            Assert.That(CollectionsRules.IsConflicted(agentCollector, accountAgentUserId: 31, accountDealerId: 14), Is.False);
            Assert.That(CollectionsRules.IsConflicted(dealerCollector, accountAgentUserId: 31, accountDealerId: 14), Is.True);
            Assert.That(CollectionsRules.IsConflicted(dealerCollector, accountAgentUserId: 31, accountDealerId: 9), Is.False);
        }

        [Test]
        public void Calculator_InCollections_DeductsFrozenAmountFromAgentAndDealer()
        {
            var c = CommissionCalculator.Calculate(new CommissionInputs
            {
                Deposit = 4_000m,
                DaysSinceStart = 200,
                IsPastLockDate = true,
                Arrears = -4_000m,          // recoveries brought it down from 10,000
                TotalPaid = 26_000m,
                BuyingPrice = 10_000m,
                HasAgent = true,
                HasWooOrder = true,
                Collections = InCollections(frozen: 10_000m, basis: 20_000m),
            });

            Assert.That(c.InCollections, Is.True);
            Assert.That(c.AgentArrearsDeducted, Is.EqualTo(10_000m));
            Assert.That(c.DealerArrearsDeducted, Is.EqualTo(10_000m));
            // Dealer base uses the 20,000 paid before handover, not 26,000.
            Assert.That(c.DealerBase, Is.EqualTo(20_000m - 10_000m - 2_000m));
            Assert.That(c.AgentBonus, Is.EqualTo(0m));
            Assert.That(c.BonusAtRisk, Is.False);
        }

        [Test]
        public void Calculator_InCollections_KeepsBonusAlreadyPaid()
        {
            var c = CommissionCalculator.Calculate(new CommissionInputs
            {
                Deposit = 4_000m,
                DaysSinceStart = 200,
                Arrears = -9_000m,
                TotalPaid = 15_000m,
                BuyingPrice = 10_000m,
                HasAgent = true,
                HasWooOrder = true,
                AgentPaid = 3_000m,
                AgentBonusPaid = 1_000m,
                Collections = InCollections(),
            });

            Assert.That(c.AgentBonus, Is.EqualTo(1_000m));
            Assert.That(c.AgentEarned, Is.EqualTo(3_000m));
        }

        [Test]
        public void Calculator_NotInCollections_Unchanged()
        {
            var c = CommissionCalculator.Calculate(new CommissionInputs
            {
                Deposit = 4_000m,
                DaysSinceStart = 200,
                Arrears = -4_000m,
                TotalPaid = 26_000m,
                BuyingPrice = 10_000m,
                HasAgent = true,
                HasWooOrder = true,
            });

            Assert.That(c.InCollections, Is.False);
            Assert.That(c.AgentArrearsDeducted, Is.EqualTo(4_000m));
            // Performing and 90+ days: the agent bonus (1,000) is earned as usual.
            Assert.That(c.DealerBase, Is.EqualTo(26_000m - 10_000m - 3_000m));
        }
    }
}
