using Ranalo.ScheduledServices;

namespace Ranolo.Web.Tests
{
    // The commission snapshot runs at 00:00, 06:00 and 18:00 Kenya time.
    public class CommissionSnapshotScheduleTests
    {
        [TestCase("2026-11-17 01:00", "2026-11-17 03:00")] // before 06:00 Nairobi
        [TestCase("2026-11-17 03:00", "2026-11-17 15:00")] // exactly on a run: the next one, 18:00 Nairobi
        [TestCase("2026-11-17 10:30", "2026-11-17 15:00")] // nothing at midday any more
        [TestCase("2026-11-17 19:05", "2026-11-17 21:00")] // midnight Nairobi
        [TestCase("2026-11-17 21:30", "2026-11-18 03:00")] // after midnight: 06:00 Nairobi
        public void NextRun(string nowUtc, string expectedUtc)
        {
            Assert.That(ScheduledCommissionSnapshot.NextRunUtc(DateTime.Parse(nowUtc)), Is.EqualTo(DateTime.Parse(expectedUtc)));
        }
    }
}
