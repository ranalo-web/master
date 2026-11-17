using Ranalo.ScheduledServices;

namespace Ranolo.Web.Tests
{
    // The commission snapshot runs at 06:00, 12:00 and 18:00 Kenya time.
    public class CommissionSnapshotScheduleTests
    {
        [TestCase("2026-11-17 01:00", "2026-11-17 03:00")] // before 06:00 Nairobi
        [TestCase("2026-11-17 03:00", "2026-11-17 09:00")] // exactly on a run: the next one
        [TestCase("2026-11-17 10:30", "2026-11-17 15:00")]
        [TestCase("2026-11-17 19:05", "2026-11-18 03:00")] // after the last run: tomorrow morning
        public void NextRun(string nowUtc, string expectedUtc)
        {
            Assert.That(ScheduledCommissionSnapshot.NextRunUtc(DateTime.Parse(nowUtc)), Is.EqualTo(DateTime.Parse(expectedUtc)));
        }
    }
}
