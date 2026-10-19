using Ranalo.Services.DeviceLock;

namespace Ranolo.Web.Tests
{
    // The deposit's M-Pesa code typed at enrolment when the order has none (2026-10-19).
    public class EnteredDepositCodeTests
    {
        [Test]
        public void Normalise_TrimsAndUppercases()
        {
            Assert.That(DeviceLockRules.NormaliseMpesaCode("  ujej79vied "), Is.EqualTo("UJEJ79VIED"));
            Assert.That(DeviceLockRules.NormaliseMpesaCode("  "), Is.Null);
        }

        [Test]
        public void NothingEntered_NoError()
        {
            Assert.That(DeviceLockRules.CheckEnteredDepositCode("", null, false), Is.Null);
        }

        [Test]
        public void OrderHasNoCode_ValidCode_Accepted()
        {
            Assert.That(DeviceLockRules.CheckEnteredDepositCode("", "UJEJ79VIED", false), Is.Null);
            Assert.That(DeviceLockRules.CheckEnteredDepositCode(null, "UJEJ79VIED", false), Is.Null);
        }

        [Test]
        public void NotAnMpesaCode_Rejected()
        {
            Assert.That(DeviceLockRules.CheckEnteredDepositCode("", "KKKKKKK", false), Does.Contain("isn't an M-Pesa code"));
            Assert.That(DeviceLockRules.CheckEnteredDepositCode("", "UJEJ79VIE!", false), Does.Contain("isn't an M-Pesa code"));
        }

        [Test]
        public void CodeOnAnotherOrder_Rejected()
        {
            Assert.That(DeviceLockRules.CheckEnteredDepositCode("", "UJEJ79VIED", true), Does.Contain("another order"));
        }

        [Test]
        public void OrderAlreadyHasACode_CantBeChangedHere()
        {
            Assert.That(DeviceLockRules.CheckEnteredDepositCode("QWE1234567", "UJEJ79VIED", false), Does.Contain("already has"));
            Assert.That(DeviceLockRules.CheckEnteredDepositCode("UJEJ79VIED", "UJEJ79VIED", false), Is.Null);
        }
    }
}
