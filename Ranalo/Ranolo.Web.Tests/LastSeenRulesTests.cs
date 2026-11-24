using NUnit.Framework;
using Ranalo.Services;

namespace Ranolo.Web.Tests
{
    public class LastSeenRulesTests
    {
        // 24 Nov 2026 12:00 UTC = 15:00 in Kenya.
        private static readonly DateTime Now = new(2026, 11, 24, 12, 0, 0, DateTimeKind.Utc);

        [Test]
        public void ShouldStamp_AtMostEveryFewMinutes()
        {
            Assert.That(LastSeenRules.ShouldStamp(null, Now), Is.True);
            Assert.That(LastSeenRules.ShouldStamp(Now.AddMinutes(-4), Now), Is.False);
            Assert.That(LastSeenRules.ShouldStamp(Now.AddMinutes(-5), Now), Is.True);
        }

        [Test]
        public void Describe_InKenyaTime()
        {
            Assert.That(LastSeenRules.Describe(null, Now), Is.EqualTo("Never"));
            Assert.That(LastSeenRules.Describe(Now.AddMinutes(-2), Now), Is.EqualTo("Online now"));
            Assert.That(LastSeenRules.Describe(Now.AddMinutes(-12), Now), Is.EqualTo("12 min ago"));
            Assert.That(LastSeenRules.Describe(Now.AddHours(-3), Now), Is.EqualTo("3 hr ago"));
            // 23 Nov 20:30 UTC = 23:30 Kenya: yesterday.
            Assert.That(LastSeenRules.Describe(new DateTime(2026, 11, 23, 20, 30, 0), Now), Is.EqualTo("Yesterday 23:30"));
            // 23 Nov 22:30 UTC = 24 Nov 01:30 Kenya: today already.
            Assert.That(LastSeenRules.Describe(new DateTime(2026, 11, 23, 22, 30, 0), Now), Is.EqualTo("13 hr ago"));
            Assert.That(LastSeenRules.Describe(new DateTime(2026, 11, 20, 6, 5, 0), Now), Is.EqualTo("20 Nov 2026 09:05"));
        }
    }
}
