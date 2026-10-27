using Ranalo.Models;
using Ranalo.ScheduledServices;
using Ranalo.Services;

namespace Ranolo.Web.Tests
{
    // Restructure rules as the R script ran them (2026-10-19).
    public class RestructureTests
    {
        private static readonly DateTime Now = new(2026, 10, 19, 9, 0, 0, DateTimeKind.Utc);

        // ---- Auto restructure ----------------------------------------------

        [Test]
        public void AutoNewDaily_SpreadsArrearsOverDaysLeftPlusTenPercent()
        {
            // 100 days left, 1,000 behind: 1,000 / 100 x 1.1 = 11 on top of the 60 daily.
            var newDaily = RestructureCalculator.AutoNewDaily(-1000m, 60m, Now.AddDays(100.5), Now);

            Assert.That(newDaily, Is.EqualTo(71m));
        }

        [Test]
        public void AutoNewDaily_PastContractEnd_NoAutoRestructure()
        {
            Assert.That(RestructureCalculator.AutoNewDaily(-8710m, 61.49m, Now.AddDays(-158), Now), Is.Null);
            Assert.That(RestructureCalculator.AutoNewDaily(-8710m, 61.49m, Now.AddHours(12), Now), Is.Null);
        }

        [Test]
        public void AutoNewDaily_NotInArrears_Null()
        {
            Assert.That(RestructureCalculator.AutoNewDaily(0m, 60m, Now.AddDays(100), Now), Is.Null);
        }

        [Test]
        public void DailyPaymentAll_WeeklyAndMonthlyAsDays()
        {
            Assert.That(RestructureCalculator.DailyPaymentAll(10m, 70m, 300m), Is.EqualTo(30m));
        }

        [Test]
        public void AutoLockDate_PaidTheNewDaily_OpenThirtyHoursFromLastPayment()
        {
            var lastPaid = Now.AddHours(-2);

            Assert.That(RestructureCalculator.AutoLockDate(71m, 71m, lastPaid, Now), Is.EqualTo(lastPaid.AddHours(30)));
        }

        [Test]
        public void AutoLockDate_PaidLess_Nothing()
        {
            Assert.That(RestructureCalculator.AutoLockDate(71m, 70m, Now.AddHours(-2), Now), Is.Null);
            Assert.That(RestructureCalculator.AutoLockDate(null, 500m, Now.AddHours(-2), Now), Is.Null);
        }

        // ---- Manual restructure --------------------------------------------

        [Test]
        public void ManualArrears_CountsFromTheAgreementDay()
        {
            // Agreed 10 days ago, 75/day: 750 due, 900 paid -> 150 ahead.
            Assert.That(RestructureCalculator.ManualArrears(900m, Now.AddDays(-10), 75m, Now, 5000m), Is.EqualTo(150m));
            Assert.That(RestructureCalculator.ManualDaysRestructured(Now.AddDays(-10), Now), Is.EqualTo(10));
        }

        [Test]
        public void ManualArrears_NoPaymentYet_StillDueFromTheAgreementDay()
        {
            // Agreed 4 days ago, nothing paid: 300 behind.
            Assert.That(RestructureCalculator.ManualArrears(0m, Now.AddDays(-4), 75m, Now, 5000m), Is.EqualTo(-300m));
        }

        [Test]
        public void ManualArrears_AgreedForALaterDay_NothingDueYet()
        {
            Assert.That(RestructureCalculator.ManualArrears(0m, Now.AddDays(2), 75m, Now, 5000m), Is.EqualTo(0m));
            Assert.That(RestructureCalculator.ManualDaysRestructured(Now.AddDays(2), Now), Is.EqualTo(0));
        }

        [Test]
        public void ManualArrears_BehindIsCappedAtTheLoanBalance()
        {
            // 7252849: 329 days at 81/day is 26,649 due, 11,339 paid, but only 3,102.20 left on the loan.
            Assert.That(RestructureCalculator.ManualArrears(11339m, Now.AddDays(-329), 81m, Now, 3102.20m), Is.EqualTo(-3102.20m));
        }

        [Test]
        public void ManualArrears_BehindLessThanTheLoanBalance_Unchanged()
        {
            // 10 days at 75/day = 750 due, 600 paid -> 150 behind, 5,000 left.
            Assert.That(RestructureCalculator.ManualArrears(600m, Now.AddDays(-10), 75m, Now, 5000m), Is.EqualTo(-150m));
        }

        [Test]
        public void ManualArrears_LoanOverpaid_NeverBehind()
        {
            Assert.That(RestructureCalculator.ManualArrears(0m, Now.AddDays(-10), 75m, Now, -200m), Is.EqualTo(0m));
        }

        [Test]
        public void ManualLockDate_AheadOpensForTheUnitsAtTwentyFourHours()
        {
            // 150 ahead at 75/day = 2 days.
            Assert.That(RestructureCalculator.ManualLockDate(150m, 75m, Now), Is.EqualTo(Now.AddHours(48)));
        }

        [Test]
        public void ManualLockDate_BehindOrLevel_LocksNow()
        {
            Assert.That(RestructureCalculator.ManualLockDate(-10m, 75m, Now), Is.EqualTo(Now));
            Assert.That(RestructureCalculator.ManualLockDate(0m, 75m, Now), Is.EqualTo(Now));
        }

        // ---- The manual restructure job --------------------------------------

        private static RestructuredRecord Record(decimal arrearsR, decimal paid24, decimal? autoNewDaily) => new()
        {
            AccountNo = 7166137,
            AmountRes = 75m,
            ArrearsR = arrearsR,
            AutoLockDatePmtR = Now.AddHours(48),
            PaidLast24Hours = paid24,
            AutoNewDaily = autoNewDaily,
            LastPaymentDate = Now.AddHours(-1),
        };

        [Test]
        public void ManualJob_AheadOnPlan_UnitsDate()
        {
            Assert.That(ScheduledLockRestructured.NextLockDate(Record(150m, 0m, null), Now), Is.EqualTo(Now.AddHours(48)));
        }

        [Test]
        public void ManualJob_OneShillingWhileBehind_StaysLocked()
        {
            // 7166137: agreed 75/day, paying 1 ksh every 2-3 days, contract
            // past its end -- this used to open the phone for 30h each time.
            Assert.That(ScheduledLockRestructured.NextLockDate(Record(-12900m, 1m, null), Now), Is.Null);
            Assert.That(ScheduledLockRestructured.NextLockDate(Record(-12900m, 1m, 80m), Now), Is.Null);
        }

        [Test]
        public void ManualJob_BehindButPaysTheAutoNewDaily_FallsBackToAuto()
        {
            // LastPaymentDate (an hour ago) is Kenya time, 3 hours ahead of
            // UTC: 30h from it is 26h from now in UTC.
            Assert.That(ScheduledLockRestructured.NextLockDate(Record(-500m, 80m, 80m), Now), Is.EqualTo(Now.AddHours(26)));
        }

        [Test]
        public void ManualJob_QualifyingLeavesTheRestToLock()
        {
            var records = new RestructuredViewModel
            {
                Records = new List<RestructuredRecord> { Record(150m, 0m, null), Record(-12900m, 1m, null), Record(-500m, 80m, 80m) },
            };

            Assert.That(ScheduledLockRestructured.GetQualifyingRecords(records, Now).Count, Is.EqualTo(2));
        }
    }
}
