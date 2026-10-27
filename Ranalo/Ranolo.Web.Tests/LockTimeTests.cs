using Ranalo.ScheduledServices;
using Ranalo.Services;

namespace Ranolo.Web.Tests
{
    public class LockTimeTests
    {
        private static readonly DateTime UtcNoon = new(2026, 10, 27, 12, 0, 0, DateTimeKind.Utc);

        [Test]
        public void Nuovo_gets_Kenya_time()
        {
            // 12:00 UTC is 3 PM in Kenya; Nuovo reads its date as Kenya time.
            Assert.That(LockTime.ForNuovo(UtcNoon), Is.EqualTo("27/10/2026T15:00:00"));
        }

        [Test]
        public void Nuovo_never_lock_date_is_sent_as_it_is()
        {
            Assert.That(LockTime.ForNuovo(DateTime.MaxValue), Is.EqualTo("31/12/9999T23:59:59"));
        }

        [Test]
        public void Knox_and_Transsion_get_the_same_moment_as_Nuovo()
        {
            var expected = new DateTimeOffset(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

            Assert.That(LockTime.UnixSeconds(UtcNoon), Is.EqualTo(expected.ToUnixTimeSeconds()));
            Assert.That(LockTime.UnixMillis(UtcNoon), Is.EqualTo(expected.ToUnixTimeMilliseconds()));
        }

        [Test]
        public void A_date_without_a_kind_is_UTC_whatever_the_server_clock()
        {
            var unspecified = new DateTime(2026, 10, 27, 12, 0, 0);

            Assert.That(LockTime.UnixSeconds(unspecified), Is.EqualTo(LockTime.UnixSeconds(UtcNoon)));
            Assert.That(LockTime.ForNuovo(unspecified), Is.EqualTo("27/10/2026T15:00:00"));
        }

        [Test]
        public void Restructured_30h_opening_counts_from_the_Kenya_payment_time()
        {
            // Paid 2:10 PM Kenya = 11:10 UTC. Open 30h: 17:10 UTC next day,
            // 8:10 PM in Kenya, for every locker.
            var paidKenya = new DateTime(2026, 10, 27, 14, 10, 0);
            var lockAt = LockTime.KenyaToUtc(paidKenya).AddHours(30);

            Assert.That(lockAt, Is.EqualTo(new DateTime(2026, 10, 28, 17, 10, 0, DateTimeKind.Utc)));
            Assert.That(LockTime.ForNuovo(lockAt), Is.EqualTo("28/10/2026T20:10:00"));
        }

        [Test]
        public void Picked_lock_date_is_midnight_in_Kenya()
        {
            var lockAt = LockTime.KenyaToUtc(DateTime.Parse("2026-10-30"));

            Assert.That(LockTime.ForNuovo(lockAt), Is.EqualTo("30/10/2026T00:00:00"));
        }

        [TestCase("2026-10-27T12:00:00", "2026-11-01T15:00:00")] // Tuesday -> Sunday
        [TestCase("2026-11-01T14:59:00", "2026-11-01T15:00:00")] // Sunday before the run
        [TestCase("2026-11-01T15:00:00", "2026-11-08T15:00:00")] // Sunday at the run -> next week
        public void Weekly_update_runs_Sunday_6PM_Kenya(string now, string expected)
        {
            Assert.That(ScheduledDailyPaymentSummary.NextRunUtc(DateTime.Parse(now)), Is.EqualTo(DateTime.Parse(expected)));
        }
    }
}
