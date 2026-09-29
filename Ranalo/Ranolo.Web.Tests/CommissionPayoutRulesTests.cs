using Ranalo.Services;

namespace Ranolo.Web.Tests
{
    // Suspension, payable and allocation rules for paying commissions out.
    public class CommissionPayoutRulesTests
    {
        // upfront defaults to earned (a dealer account: one pass).
        private static PayoutAccount Acc(long id, decimal earned, decimal deducted = 0, decimal paid = 0, int daysAgo = 100, decimal? upfront = null) => new()
        {
            ContractId = id,
            StartDate = new DateTime(2026, 9, 1).AddDays(-daysAgo),
            Earned = earned,
            Upfront = upfront ?? earned,
            Deducted = deducted,
            Paid = paid,
        };

        [Test]
        public void DefaultRate_IsShareOfAccountsInArrears()
        {
            Assert.That(CommissionPayoutRules.DefaultRatePct(10, 3), Is.EqualTo(30m));
            Assert.That(CommissionPayoutRules.DefaultRatePct(3, 1), Is.EqualTo(33.3m));
            Assert.That(CommissionPayoutRules.DefaultRatePct(0, 0), Is.EqualTo(0m));
        }

        [Test]
        public void Suspended_OnlyAboveThirtyPercent()
        {
            Assert.That(CommissionPayoutRules.IsSuspended(30m), Is.False);
            Assert.That(CommissionPayoutRules.IsSuspended(30.1m), Is.True);
        }

        [Test]
        public void Payable_IsZeroWhileSuspended()
        {
            var all = new[] { Acc(1, 2_000m) };

            Assert.That(CommissionPayoutRules.Payable(all, new long[] { 1 }, suspended: true), Is.EqualTo(0m));
        }

        [Test]
        public void Payable_DeductsArrearsOnAccountsNotBeingPaid()
        {
            // Paying account 1 only; account 2's arrears still come off.
            var all = new[] { Acc(1, 2_000m), Acc(2, 1_000m, deducted: 1_500m) };

            Assert.That(CommissionPayoutRules.Payable(all, new long[] { 1 }, suspended: false), Is.EqualTo(1_500m));
        }

        [Test]
        public void Payable_NeverMoreThanUnpaidOnSelectedAccounts()
        {
            var all = new[] { Acc(1, 2_000m, paid: 1_500m), Acc(2, 3_000m) };

            Assert.That(CommissionPayoutRules.Payable(all, new long[] { 1 }, suspended: false), Is.EqualTo(500m));
        }

        [Test]
        public void Allocate_CoversOwnNetOldestFirst()
        {
            var selected = new[] { Acc(2, 1_000m, daysAgo: 10), Acc(1, 2_000m, daysAgo: 200) };

            var lines = CommissionPayoutRules.Allocate(selected, 2_500m);

            Assert.That(lines, Is.EqualTo(new[] { (1L, 2_000m), (2L, 500m) }));
        }

        [Test]
        public void Allocate_PaysEveryUpfrontBeforeAnyBonus()
        {
            // Old account: 2,000 upfront + 1,000 bonus. New account: 2,000 upfront, no bonus yet.
            var selected = new[]
            {
                Acc(1, 3_000m, upfront: 2_000m, daysAgo: 200),
                Acc(2, 2_000m, upfront: 2_000m, daysAgo: 10),
            };

            var lines = CommissionPayoutRules.Allocate(selected, 4_500m);

            Assert.That(lines, Is.EqualTo(new[] { (1L, 2_500m), (2L, 2_000m) }));
        }

        [Test]
        public void Allocate_AlreadyPaidCountsTowardUpfrontFirst()
        {
            // Upfront 2,000 already paid at sale; only the 1,000 bonus is left on account 1.
            var selected = new[]
            {
                Acc(1, 3_000m, paid: 2_000m, upfront: 2_000m, daysAgo: 200),
                Acc(2, 2_000m, upfront: 2_000m, daysAgo: 10),
            };

            var lines = CommissionPayoutRules.Allocate(selected, 2_000m);

            Assert.That(lines, Is.EqualTo(new[] { (2L, 2_000m) }));
        }

        [Test]
        public void Allocate_OverrideBeyondUnpaidLandsOnNewestAccount()
        {
            var selected = new[] { Acc(1, 1_000m, daysAgo: 200), Acc(2, 0m, daysAgo: 10) };

            var lines = CommissionPayoutRules.Allocate(selected, 1_500m);

            Assert.That(lines, Is.EqualTo(new[] { (1L, 1_000m), (2L, 500m) }));
            Assert.That(lines.Sum(l => l.Amount), Is.EqualTo(1_500m));
        }
    }
}
