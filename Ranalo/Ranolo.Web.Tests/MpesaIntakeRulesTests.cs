using Ranalo.Services;

namespace Ranolo.Web.Tests
{
    // Checks on payments posted to api/mpesa/transaction (2026-10-21).
    public class MpesaIntakeRulesTests
    {
        [Test]
        public void NoSecretConfigured_NotEnforcedYet()
        {
            Assert.That(MpesaIntakeRules.SecretOk(null, null), Is.True);
            Assert.That(MpesaIntakeRules.SecretOk("", "anything"), Is.True);
        }

        [Test]
        public void SecretConfigured_MustMatch()
        {
            Assert.That(MpesaIntakeRules.SecretOk("s3cret-value", "s3cret-value"), Is.True);
            Assert.That(MpesaIntakeRules.SecretOk("s3cret-value", "wrong"), Is.False);
            Assert.That(MpesaIntakeRules.SecretOk("s3cret-value", null), Is.False);
            Assert.That(MpesaIntakeRules.SecretOk("s3cret-value", ""), Is.False);
        }

        [Test]
        public void RealLookingPayment_Accepted()
        {
            Assert.That(MpesaIntakeRules.Invalid("UJK2DB4M46", "140", "7005299", "20261020133631"), Is.Null);
            Assert.That(MpesaIntakeRules.Invalid("UJK2DB4M46", "140.00", "7005299", "20261020133631"), Is.Null);
        }

        [TestCase("", "140", "7005299", "20261020133631")]
        [TestCase("UJK2'; DROP", "140", "7005299", "20261020133631")]
        [TestCase("UJK2DB4M46", "0", "7005299", "20261020133631")]
        [TestCase("UJK2DB4M46", "-5", "7005299", "20261020133631")]
        [TestCase("UJK2DB4M46", "abc", "7005299", "20261020133631")]
        [TestCase("UJK2DB4M46", "140", "", "20261020133631")]
        [TestCase("UJK2DB4M46", "140", "7005299", "2026-10-20")]
        [TestCase("UJK2DB4M46", "140", "7005299", null)]
        public void BadPayment_Refused(string? id, string? amount, string? account, string? time)
        {
            Assert.That(MpesaIntakeRules.Invalid(id, amount, account, time), Is.Not.Null);
        }
    }
}
