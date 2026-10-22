using Microsoft.Extensions.Logging.Abstractions;
using Ranalo.DataStore;
using Ranalo.Models;
using Ranalo.ScheduledServices;
using Ranalo.Services;
using Ranalo.Services.DeviceLock;
using MobileStatusReport = Ranalo.Models.MobileStatusReport;

namespace Ranolo.Web.Tests
{
    // LockGrace__Until: phones opened while payment callbacks were down (2026-10-22).
    public class LockGraceTests
    {
        private static readonly DateTimeOffset Now = new(2026, 10, 22, 6, 0, 0, TimeSpan.Zero);

        [Test]
        public void Until_OnlyWhileInTheFuture()
        {
            Assert.That(LockGrace.Until(null, Now), Is.Null);
            Assert.That(LockGrace.Until("", Now), Is.Null);
            Assert.That(LockGrace.Until("not a date", Now), Is.Null);
            Assert.That(LockGrace.Until("2026-10-21T09:00:00+03:00", Now), Is.Null, "past: grace over");
            Assert.That(LockGrace.Until("2026-10-24T09:00:00+03:00", Now), Is.EqualTo(new DateTimeOffset(2026, 10, 24, 6, 0, 0, TimeSpan.Zero)));
            Assert.That(LockGrace.Until("2026-10-24T06:00:00Z", Now), Is.EqualTo(new DateTimeOffset(2026, 10, 24, 6, 0, 0, TimeSpan.Zero)));
        }

        [Test]
        public void Until_NoOffset_IsNairobiTime()
        {
            Assert.That(LockGrace.Until("2026-10-24 09:00", Now), Is.EqualTo(new DateTimeOffset(2026, 10, 24, 9, 0, 0, TimeSpan.FromHours(3))));
        }

        [Test]
        public void Apply_MovesEarlierLockDatesToTheGraceEnd()
        {
            var until = new DateTimeOffset(2026, 10, 24, 6, 0, 0, TimeSpan.Zero);
            var early = new DateTime(2026, 10, 22, 8, 0, 0, DateTimeKind.Utc);
            var late = new DateTime(2026, 11, 2, 8, 0, 0, DateTimeKind.Utc);

            Assert.That(LockGrace.Apply(early, until), Is.EqualTo(until.UtcDateTime));
            Assert.That(LockGrace.Apply(late, until), Is.EqualTo(late), "paid beyond the grace: unchanged");
            Assert.That(LockGrace.Apply(DateTime.MaxValue, until), Is.EqualTo(DateTime.MaxValue));
            Assert.That(LockGrace.Apply(early, null), Is.EqualTo(early), "no grace: unchanged");
        }

        private static List<MobileStatusReport> Accounts() => new()
        {
            // Paying, a day ahead.
            new() { AccountNo = 1, FirstName = "Ahead", LockGroup = 1, Arrears = 50, LoanBalance = 5000, Daily = 50 },
            // Overdue, including a huge debt on a tiny daily.
            new() { AccountNo = 2, FirstName = "Overdue", LockGroup = 1, Arrears = -400, LoanBalance = 5000, Daily = 50 },
            new() { AccountNo = 3, FirstName = "Deep", LockGroup = 1, Arrears = -9000000, LoanBalance = 9000000, Daily = 0.01m },
            // Fully paid: not this job's business.
            new() { AccountNo = 4, FirstName = "Paid", LockGroup = 1, Arrears = 0, LoanBalance = 0, Daily = 50 },
            // Lost/stolen: stays locked.
            new() { AccountNo = 5, FirstName = "Stolen", LockGroup = 1, Arrears = -400, LoanBalance = 5000, Daily = 50 },
        };

        private static async Task<List<LockTransaction>> RunPayingJob(DateTimeOffset? graceUntil)
        {
            var records = Accounts();
            var (reports, _) = InterfaceFake<IApplicationReportService>.Create(new()
            {
                ["GetStatusReportByDealer"] = _ => Task.FromResult(new StatusReportViewModel { StatusReports = records })
            });
            List<LockTransaction> nuovo = new();
            var (processor, _) = InterfaceFake<IDeviceProcessor>.Create(new()
            {
                ["ProcessBatchesAsync"] = a => { nuovo = (List<LockTransaction>)a![0]!; return Task.FromResult(nuovo); }
            });
            var (enrolmentService, _) = InterfaceFake<IEnrolmentService>.Create(new());
            var (payments, _) = InterfaceFake<IPaymentsRepository>.Create(new());

            var job = new ScheduledLockPaying(NullLogger<ScheduledLockPaying>.Instance, null!);
            await job.Process(processor, reports, payments, enrolmentService, new HashSet<long> { 5 }, graceUntil);
            return nuovo;
        }

        [Test]
        public async Task NoGrace_OnlyPayingPhonesGetALockDate()
        {
            var sent = await RunPayingJob(null);
            Assert.That(sent.Select(d => d.AccountId), Is.EqualTo(new long[] { 1 }));
        }

        [Test]
        public async Task Grace_OverduePhonesOpenUntilItEnds_StolenStaysLocked()
        {
            var until = DateTimeOffset.UtcNow.AddHours(48);
            var sent = await RunPayingJob(until);

            Assert.That(sent.Select(d => d.AccountId), Is.EqualTo(new long[] { 1, 2, 3 }));
            Assert.That(sent.All(d => d.AutoLockDate >= until.LocalDateTime.AddSeconds(-1)), Is.True,
                "nobody locks before the grace ends");
        }
    }
}
