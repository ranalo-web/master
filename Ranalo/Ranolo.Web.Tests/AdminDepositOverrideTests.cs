using Ranalo.DataStore.DataModels;
using Ranalo.Services.DeviceLock;

namespace Ranolo.Web.Tests
{
    // The M-Pesa deposit must be confirmed to enrol, whoever enrols (2026-10-21).
    public class AdminDepositOverrideTests
    {
        private static DepositCheckResult Missing() =>
            DeviceLockRules.CheckDeposit(null, "39282520", 3000m, Array.Empty<DepositPayment>());

        [Test]
        public void NoDepositCode_BlocksEveryone_AdminsToo()
        {
            // The admin override was removed the same day: every enrolment
            // needs its deposit confirmed, as before.
            Assert.That(Missing().Blocks, Is.True);
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
