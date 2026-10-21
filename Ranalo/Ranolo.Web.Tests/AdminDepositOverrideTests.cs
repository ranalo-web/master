using Ranalo.DataStore.DataModels;
using Ranalo.Services.DeviceLock;

namespace Ranolo.Web.Tests
{
    // Admins enrol without the M-Pesa deposit code (2026-10-21).
    public class AdminDepositOverrideTests
    {
        private static DepositCheckResult Missing() =>
            DeviceLockRules.CheckDeposit(null, "39282520", 3000m, Array.Empty<DepositPayment>());

        [Test]
        public void NoDepositCode_BlocksApproverAndDealer()
        {
            Assert.That(DeviceLockRules.AllowForAdmin(Missing(), UserRole.Approver).Blocks, Is.True);
            Assert.That(DeviceLockRules.AllowForAdmin(Missing(), UserRole.Dealer).Blocks, Is.True);
        }

        [Test]
        public void NoDepositCode_AdminEnrols_FlaggedShort()
        {
            var result = DeviceLockRules.AllowForAdmin(Missing(), UserRole.Admin);

            Assert.That(result.Blocks, Is.False);
            Assert.That(result.IsShort, Is.True, "short, so only an admin can approve it");
            Assert.That(result.Message, Does.Contain("No confirmed deposit"));
        }

        [Test]
        public void DepositOk_UnchangedForAdmin()
        {
            var ok = new DepositCheckResult(DepositCheck.Ok, 3000m, 3000m, null);
            Assert.That(DeviceLockRules.AllowForAdmin(ok, UserRole.Admin), Is.EqualTo(ok));
        }

        [TestCase("IN ACCOUNT", null)]
        [TestCase("  ", null)]
        [TestCase(null, null)]
        [TestCase("paid cash", null)]
        [TestCase(" UJK2DB4M46 ", "UJK2DB4M46")]
        public void DepositReferenceThatIsntACode_CountsAsNone(string? stored, string? expected)
        {
            Assert.That(DeviceLockRules.DepositCodeOrNull(stored), Is.EqualTo(expected));
        }
    }
}
