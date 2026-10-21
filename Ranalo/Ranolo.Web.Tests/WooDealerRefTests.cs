using Newtonsoft.Json.Linq;
using Ranalo.Woocommece.Api.Services;

namespace Ranolo.Web.Tests
{
    // The dealer on a WooCommerce order comes from the checkout's
    // "Select a Dealer" field (order meta dealer_code), 2026-10-21.
    public class WooDealerRefTests
    {
        [Test]
        public void DealerChosenAtCheckout_WinsOverReferralCode()
        {
            var order = JObject.Parse(@"{
                ""billing"": { ""billing_referral_code"": ""Mercy"" },
                ""meta_data"": [ { ""id"": 1, ""key"": ""dealer_code"", ""value"": ""7639"" } ] }");

            Assert.That(SyncService.DealerRefFromWoo(order), Is.EqualTo("7639"));
        }

        [Test]
        public void NoDealerCode_FallsBackToReferralCode()
        {
            var order = JObject.Parse(@"{ ""billing"": { ""billing_referral_code"": ""9085"" }, ""meta_data"": [] }");
            Assert.That(SyncService.DealerRefFromWoo(order), Is.EqualTo("9085"));
        }

        [Test]
        public void NothingAtAll_Empty()
        {
            Assert.That(SyncService.DealerRefFromWoo(JObject.Parse("{}")), Is.EqualTo(""));
        }
    }
}
