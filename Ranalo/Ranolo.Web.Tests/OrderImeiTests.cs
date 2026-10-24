using Ranalo.Services.DeviceLock;

namespace Ranolo.Web.Tests
{
    // Customers are screened before choosing a phone, so the WooCommerce
    // order usually has no IMEI at enrolment (2026-10-24).
    public class OrderImeiTests
    {
        private const string Imei = "351065613492352";

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void NoImeiOnTheOrder_Allowed(string? orderImei)
        {
            Assert.That(DeviceLockRules.CheckOrderImei(orderImei, Imei), Is.Null);
            Assert.That(DeviceLockRules.OrderHasImei(orderImei), Is.False);
        }

        [Test]
        public void SameImeiOnTheOrder_Allowed()
        {
            Assert.That(DeviceLockRules.CheckOrderImei(" " + Imei + " ", Imei), Is.Null);
        }

        [Test]
        public void DifferentImeiOnTheOrder_Refused()
        {
            Assert.That(DeviceLockRules.CheckOrderImei("356938035643809", Imei), Does.Contain("356938035643809").And.Contain(Imei));
        }
    }
}
