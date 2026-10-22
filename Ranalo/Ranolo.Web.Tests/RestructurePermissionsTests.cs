using Ranalo.DataStore.DataModels;
using Ranalo.Services;

namespace Ranolo.Web.Tests
{
    // Who may restructure, and to what rate (2026-10-22).
    public class RestructurePermissionsTests
    {
        [TestCase(UserRole.Admin, true)]
        [TestCase(UserRole.Approver, true)]
        [TestCase(UserRole.Collector, false)]
        [TestCase(UserRole.Dealer, false)]
        [TestCase(UserRole.Agent, false)]
        [TestCase(UserRole.Guest, false)]
        [TestCase(UserRole.Customer, false)]
        [TestCase(UserRole.Supplier, false)]
        public void OnlyAdminAndApprover(UserRole role, bool allowed)
        {
            Assert.That(RestructurePermissions.CanRestructure(role), Is.EqualTo(allowed));
            Assert.That(RestructurePermissions.Refusal(role, 100m, 60m) == null, Is.EqualTo(allowed));
        }

        [Test]
        public void Admin_AnyRate_EvenBelowDaily()
        {
            Assert.That(RestructurePermissions.Refusal(UserRole.Admin, 5m, 60m), Is.Null);
            Assert.That(RestructurePermissions.Refusal(UserRole.Admin, 5m, null), Is.Null);
            Assert.That(RestructurePermissions.MinimumRate(UserRole.Admin, 60m), Is.Null);
        }

        [Test]
        public void Approver_DailyOrMore()
        {
            // 50 daily + 70 weekly (10 a day) = 60 a day.
            var daily = RestructureCalculator.DailyPaymentAll(50m, 70m, 0m);

            Assert.That(RestructurePermissions.Refusal(UserRole.Approver, 60m, daily), Is.Null);
            Assert.That(RestructurePermissions.Refusal(UserRole.Approver, 90m, daily), Is.Null);
            Assert.That(RestructurePermissions.Refusal(UserRole.Approver, 59.99m, daily), Is.Not.Null);
        }

        [Test]
        public void Approver_DailyRoundedToCents()
        {
            // 100 monthly = 3.3333 a day; 3.33 is enough.
            Assert.That(RestructurePermissions.MinimumRate(UserRole.Approver, 100m / 30m), Is.EqualTo(3.33m));
            Assert.That(RestructurePermissions.Refusal(UserRole.Approver, 3.33m, 100m / 30m), Is.Null);
        }

        [Test]
        public void Approver_AccountNotFound_Refused()
        {
            Assert.That(RestructurePermissions.Refusal(UserRole.Approver, 100m, null), Is.Not.Null);
            Assert.That(RestructurePermissions.Refusal(UserRole.Approver, 100m, 0m), Is.Not.Null);
        }

        [Test]
        public void ZeroOrNegativeRate_Refused()
        {
            Assert.That(RestructurePermissions.Refusal(UserRole.Admin, 0m, 60m), Is.Not.Null);
            Assert.That(RestructurePermissions.Refusal(UserRole.Admin, -10m, 60m), Is.Not.Null);
        }
    }
}
