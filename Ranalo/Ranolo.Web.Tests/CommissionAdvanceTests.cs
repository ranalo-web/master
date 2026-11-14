using Ranalo.Models;
using Ranalo.Services;

namespace Ranolo.Web.Tests
{
    // Dealer advances: capped at the commission projected once the customer
    // has paid in full, none while suspended, and repaid from later commission.
    public class CommissionAdvanceTests
    {
        private static PayoutAccount Acc(long id, decimal earned, decimal? projected, decimal deducted = 0, decimal paid = 0) => new()
        {
            ContractId = id,
            StartDate = new DateTime(2026, 9, 1).AddDays(id),
            Earned = earned,
            Upfront = earned,
            Deducted = deducted,
            Paid = paid,
            Projected = projected,
        };

        private static CommissionAccount Account(decimal? buyingPrice = 10_000m, decimal? totalCost = 25_000m, double daysPastLock = 0,
            CollectionTerms? collections = null, CommissionExclusion? exclusion = null) => new()
        {
            ContractId = "1",
            Deposit = 4_000m,
            TotalCost = totalCost,
            BuyingPrice = buyingPrice,
            DaysPastLock = daysPastLock,
            Exclusion = exclusion,
            Commission = new CommissionBreakdown
            {
                InCollections = collections != null,
                NoCommission = exclusion != null,
            },
        };

        [Test]
        public void Projected_IsThirtyPercentOfContractLessBuyingPriceLessFullAgentCommission()
        {
            // 25,000 - 10,000 - 75% of 4,000 = 12,000; 30% = 3,600.
            Assert.That(CommissionCalculator.ProjectedDealerCommission(4_000m, 25_000m, 10_000m), Is.EqualTo(3_600m));
            Assert.That(CommissionCalculator.ProjectedDealerCommission(4_000m, 12_000m, 10_000m), Is.EqualTo(0m));
            Assert.That(CommissionCalculator.ProjectedDealerCommission(4_000m, null, 10_000m), Is.Null);
            Assert.That(CommissionCalculator.ProjectedDealerCommission(4_000m, 25_000m, null), Is.Null);
        }

        [Test]
        public void Room_IsProjectedLessArrearsLessAlreadyPaid()
        {
            Assert.That(CommissionPayoutRules.AdvanceRoom(Acc(1, earned: 600, projected: 3_600, deducted: 500, paid: 1_000)), Is.EqualTo(2_100m));
            Assert.That(CommissionPayoutRules.AdvanceRoom(Acc(1, earned: 600, projected: 3_600, paid: 4_000)), Is.EqualTo(0m));
            Assert.That(CommissionPayoutRules.AdvanceRoom(Acc(1, earned: 600, projected: null)), Is.EqualTo(0m));
        }

        [Test]
        public void Payable_IsZeroWhenSuspended()
        {
            var all = new[] { Acc(1, 600, 3_600) };
            Assert.That(CommissionPayoutRules.AdvancePayable(all, new long[] { 1 }, suspended: true), Is.EqualTo(0m));
            Assert.That(CommissionPayoutRules.AdvancePayable(all, new long[] { 1 }, suspended: false), Is.EqualTo(3_600m));
        }

        [Test]
        public void Payable_IsReducedByLossesOnTheDealersOtherAccounts()
        {
            // Account 2 is in collections (no projection): its handover
            // deduction of 2,000 against 300 earned comes off the advance.
            var all = new[] { Acc(1, 600, 3_600), Acc(2, 300, null, deducted: 2_000) };
            Assert.That(CommissionPayoutRules.AdvancePayable(all, new long[] { 1 }, suspended: false), Is.EqualTo(1_900m));
        }

        [Test]
        public void Allocate_FillsEachAccountUpToItsRoom_OldestFirst()
        {
            var selected = new[] { Acc(1, 0, 1_000), Acc(2, 0, 3_000) };
            var lines = CommissionPayoutRules.Allocate(selected, 2_500m, CommissionPart.Advance);
            Assert.That(lines, Is.EqualTo(new[] { new PayoutLine(1, null, 1_000m), new PayoutLine(2, null, 1_500m) }));
        }

        [Test]
        public void AdvanceIsRepaidFromLaterCommission()
        {
            // 2,000 advanced, 600 earned so far: nothing payable and 1,400 paid ahead.
            var pool = CommissionCalculator.Pool(new[] { (600m, 0m, 2_000m) });
            Assert.That(pool.Owed, Is.EqualTo(0m));
            Assert.That(CommissionPayoutRules.PaidAhead(pool), Is.EqualTo(1_400m));

            // Once 2,500 is earned, only the 500 beyond the advance is owed.
            pool = CommissionCalculator.Pool(new[] { (2_500m, 0m, 2_000m) });
            Assert.That(pool.Owed, Is.EqualTo(500m));
            Assert.That(CommissionPayoutRules.PaidAhead(pool), Is.EqualTo(0m));
        }

        [Test]
        public void BlockedAccounts_CantBeAdvancedOn()
        {
            Assert.That(CommissionPayees.AdvanceBlockedReason(Account()), Is.Null);
            Assert.That(CommissionPayees.ProjectedDealerCommission(Account()), Is.EqualTo(3_600m));
            Assert.That(CommissionPayees.AdvanceBlockedReason(Account(buyingPrice: null)), Is.EqualTo("No buying price"));
            Assert.That(CommissionPayees.AdvanceBlockedReason(Account(totalCost: null)), Is.EqualTo("No contract value"));
            Assert.That(CommissionPayees.AdvanceBlockedReason(Account(daysPastLock: 8)), Is.Not.Null);
            Assert.That(CommissionPayees.AdvanceBlockedReason(Account(collections: new CollectionTerms())), Is.EqualTo("In collections"));
            Assert.That(CommissionPayees.ProjectedDealerCommission(Account(daysPastLock: 8)), Is.Null);
        }
    }
}
