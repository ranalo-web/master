using Ranalo.DataStore.DataModels;
using Ranalo.Services;

namespace Ranolo.Web.Tests
{
    // Who can manage which logins (2026-10-21).
    public class UserAdminRulesTests
    {
        private static User U(int id, UserRole role, int dealerId = 0) => new() { UserId = id, RoleId = role, DealerId = dealerId };

        [Test]
        public void Admin_ManagesAnyoneButThemselves()
        {
            var admin = U(1, UserRole.Admin);
            Assert.That(UserAdminRules.CanManage(admin, 0, U(2, UserRole.Admin)), Is.True);
            Assert.That(UserAdminRules.CanManage(admin, 0, U(3, UserRole.Agent, 9)), Is.True);
            Assert.That(UserAdminRules.CanManage(admin, 0, admin), Is.False);
        }

        [Test]
        public void Dealer_ManagesOnlyTheirOwnAgentsAndCollectors()
        {
            var dealer = U(10, UserRole.Dealer);
            Assert.That(UserAdminRules.CanManage(dealer, 7, U(11, UserRole.Agent, 7)), Is.True);
            Assert.That(UserAdminRules.CanManage(dealer, 7, U(12, UserRole.Collector, 7)), Is.True);
            Assert.That(UserAdminRules.CanManage(dealer, 7, U(13, UserRole.Agent, 8)), Is.False, "another dealer's agent");
            Assert.That(UserAdminRules.CanManage(dealer, 7, U(14, UserRole.Admin, 7)), Is.False);
            Assert.That(UserAdminRules.CanManage(dealer, 7, U(15, UserRole.Dealer, 7)), Is.False);
        }

        [TestCase(UserRole.Approver)]
        [TestCase(UserRole.Agent)]
        [TestCase(UserRole.Collector)]
        public void OtherRoles_ManageNobody_AndAddNobody(UserRole role)
        {
            var actor = U(20, role, 7);
            Assert.That(UserAdminRules.CanManage(actor, 7, U(21, UserRole.Agent, 7)), Is.False);
            Assert.That(UserAdminRules.CanAddUsers(actor), Is.False);
        }

        [Test]
        public void Dealer_CantGiveAdminOrDealer()
        {
            var dealer = U(10, UserRole.Dealer);
            Assert.That(UserAdminRules.CanAssign(dealer, UserRole.Agent), Is.True);
            Assert.That(UserAdminRules.CanAssign(dealer, UserRole.Admin), Is.False);
            Assert.That(UserAdminRules.CanAssign(dealer, UserRole.Dealer), Is.False);
            Assert.That(UserAdminRules.CanAssign(dealer, UserRole.Approver), Is.False);
        }

        [TestCase("Eddie", "Guda", "EG")]
        [TestCase("eddie", null, "E")]
        [TestCase(null, null, "?")]
        public void Initials(string? first, string? last, string expected)
        {
            Assert.That(UserAdminRules.Initials(first, last), Is.EqualTo(expected));
        }
    }
}
