using Ranalo.Models;
using Ranalo.Services;

namespace Ranolo.Web.Tests
{
    // Self-service restructure in the customer app (2026-11-06).
    public class SelfRestructureTests
    {
        private static SelfRestructureRules.Facts Facts() => new()
        {
            HasOpenContract = true, Balance = 7262m, Behind = 400m, DailyAll = 68.16m,
        };

        [Test]
        public void A_customer_who_is_behind_is_offered_our_plans()
        {
            var options = SelfRestructureRules.Options(Facts());

            Assert.That(options.Select(o => o.Name), Is.EqualTo(new[] { "Standard", "Faster" }));
            Assert.That(options[0].Daily, Is.EqualTo(69m), "the original daily, rounded up: never below it");
            Assert.That(options[0].Days, Is.EqualTo(106), "until paid");
            Assert.That(options[1].Daily, Is.EqualTo(103m));
        }

        [Test]
        public void Faster_is_only_offered_when_it_is_really_shorter()
        {
            var f = Facts();
            f.Balance = 300m;
            Assert.That(SelfRestructureRules.Options(f).Select(o => o.Name), Is.EqualTo(new[] { "Standard" }));
        }

        [Test]
        public void Past_the_contract_end_is_still_offered()
        {
            // Nothing in the rules looks at the contract end: "until paid".
            Assert.That(SelfRestructureRules.Refusal(Facts()), Is.Null);
        }

        [TestCase("open")]
        [TestCase("paid")]
        [TestCase("used")]
        [TestCase("lost")]
        [TestCase("recovery")]
        [TestCase("daily")]
        [TestCase("behind")]
        [TestCase("onplan")]
        public void Each_gap_is_closed(string gap)
        {
            var f = Facts();
            switch (gap)
            {
                case "open": f.HasOpenContract = false; break;
                case "paid": f.FullyPaid = true; break;
                case "used": f.UsedBefore = true; break;
                case "lost": f.LostOrStolen = true; break;
                case "recovery": f.InRecovery = true; break;
                case "daily": f.DailyAll = 0m; break;
                case "behind": f.Behind = 0m; break;
                case "onplan": f.OnPlan = true; break;
            }

            Assert.That(SelfRestructureRules.Refusal(f), Is.Not.Null);
            Assert.That(SelfRestructureRules.Options(f), Is.Empty);
            Assert.That(SelfRestructureRules.Find(f, "Standard"), Is.Null);
        }

        [Test]
        public void Already_on_a_plan_they_have_to_call()
        {
            var f = Facts();
            f.OnPlan = true;
            Assert.That(SelfRestructureRules.Refusal(f), Is.EqualTo("You're already on a payment plan. To change it, call us."));
        }

        [Test]
        public void After_one_plan_they_have_to_call()
        {
            var f = Facts();
            f.UsedBefore = true;
            Assert.That(SelfRestructureRules.Refusal(f), Does.Contain("Call us"));
        }

        [Test]
        public void Only_a_plan_we_offered_can_be_chosen()
        {
            Assert.That(SelfRestructureRules.Find(Facts(), "faster")!.Daily, Is.EqualTo(103m));
            Assert.That(SelfRestructureRules.Find(Facts(), "Cheap"), Is.Null);
            Assert.That(SelfRestructureRules.Find(Facts(), null), Is.Null);
        }

        [Test]
        public void A_chosen_plan_starts_on_its_first_day_paid_or_expires_after_24_hours()
        {
            var now = new DateTime(2026, 11, 6, 12, 0, 0, DateTimeKind.Utc);
            var plan = new SelfRestructure { Daily = 69m, ChosenAtUtc = now.AddHours(-2), ExpiresAtUtc = now.AddHours(22) };

            Assert.That(SelfRestructureRules.Decide(plan, 50m, now), Is.EqualTo(SelfRestructureRules.PendingOutcome.Wait));
            Assert.That(SelfRestructureRules.Decide(plan, 69m, now), Is.EqualTo(SelfRestructureRules.PendingOutcome.Start));
            Assert.That(SelfRestructureRules.Decide(plan, 50m, now.AddHours(23)), Is.EqualTo(SelfRestructureRules.PendingOutcome.Expire));
            // Paid in time (only the 24-hour window is counted) still starts if checked late.
            Assert.That(SelfRestructureRules.Decide(plan, 69m, now.AddHours(30)), Is.EqualTo(SelfRestructureRules.PendingOutcome.Start));
        }
    }
}
