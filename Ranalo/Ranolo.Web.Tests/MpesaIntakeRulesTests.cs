using Ranalo.Services;
using System.Text.Json;

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
        public void Read_NoFirstNameAndNumericAmount_StillReadable()
        {
            var body = JsonDocument.Parse("{\"transid\":\"UJK2DB4M46\",\"TransAmount\":10,\"BillRefNumber\":\"test\",\"TransTime\":\"20261021180000\"}").RootElement;
            var t = MpesaIntakeRules.Read(body)!;
            Assert.That(t.TransID, Is.EqualTo("UJK2DB4M46"));
            Assert.That(t.TransAmount, Is.EqualTo("10"));
            Assert.That(t.FirstName, Is.Null);
            Assert.That(MpesaIntakeRules.Invalid(t.TransID, t.TransAmount, t.BillRefNumber, t.TransTime), Is.Null);
        }

        [Test]
        public void Read_NotAnObject_Null()
        {
            Assert.That(MpesaIntakeRules.Read(JsonDocument.Parse("[]").RootElement), Is.Null);
        }

        [Test]
        public void RealLookingPayment_Accepted()
        {
            Assert.That(MpesaIntakeRules.Invalid("UJK2DB4M46", "140", "7005299", "20261020133631"), Is.Null);
            Assert.That(MpesaIntakeRules.Invalid("UJK2DB4M46", "140.00", "7005299", "20261020133631"), Is.Null);
        }

        [TestCase(null, false)]
        [TestCase("", false)]
        [TestCase("Monitor", false)]
        [TestCase("anything", false)]
        [TestCase("Enforce", true)]
        [TestCase(" enforce ", true)]
        public void OnlyEnforceModeRefuses(string? mode, bool enforce)
        {
            Assert.That(MpesaIntakeRules.Enforce(mode), Is.EqualTo(enforce));
        }

        [Test]
        public void NoIpListConfigured_AnyIp()
        {
            Assert.That(MpesaIntakeRules.IpAllowed(null, "203.0.113.9"), Is.True);
            Assert.That(MpesaIntakeRules.IpAllowed(" ", null), Is.True);
        }

        [Test]
        public void IpListConfigured_MustBeOnIt()
        {
            const string allowed = "198.51.100.7, 203.0.113.9";
            Assert.That(MpesaIntakeRules.IpAllowed(allowed, "203.0.113.9"), Is.True);
            Assert.That(MpesaIntakeRules.IpAllowed(allowed, "203.0.113.9:51234"), Is.True);
            Assert.That(MpesaIntakeRules.IpAllowed(allowed, "203.0.113.10"), Is.False);
            Assert.That(MpesaIntakeRules.IpAllowed(allowed, null), Is.False);
        }

        [Test]
        public void SenderIp_LastForwardedEntry_NotWhatTheSenderWrote()
        {
            Assert.That(MpesaIntakeRules.SenderIp("198.51.100.7, 203.0.113.9:51234", "10.0.0.1"), Is.EqualTo("203.0.113.9"));
            Assert.That(MpesaIntakeRules.SenderIp("", "10.0.0.1"), Is.EqualTo("10.0.0.1"));
            Assert.That(MpesaIntakeRules.SenderIp(null, "2001:db8::1"), Is.EqualTo("2001:db8::1"));
            Assert.That(MpesaIntakeRules.SenderIp("[2001:db8::1]:443", null), Is.EqualTo("2001:db8::1"));
        }

        [Test]
        public void Read_RawJsonOrForm()
        {
            var json = MpesaIntakeRules.Read("{\"TransID\":\"UJK2DB4M46\",\"TransAmount\":140,\"BillRefNumber\":\"7005299\",\"TransTime\":\"20261020133631\"}")!;
            Assert.That(json.TransID, Is.EqualTo("UJK2DB4M46"));
            Assert.That(json.TransAmount, Is.EqualTo("140"));

            var form = MpesaIntakeRules.Read("TransID=UJK2DB4M46&TransAmount=140&BillRefNumber=7005299&TransTime=20261020133631")!;
            Assert.That(form.TransID, Is.EqualTo("UJK2DB4M46"));
            Assert.That(form.BillRefNumber, Is.EqualTo("7005299"));

            Assert.That(MpesaIntakeRules.Read(""), Is.Null);
            Assert.That(MpesaIntakeRules.Read("not a payment"), Is.Null);
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
