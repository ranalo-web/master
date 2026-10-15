using Ranalo.Models;
using Ranalo.Services;

namespace Ranolo.Web.Tests
{
    // Lock check: Nuovo phones that need attention (agreed 2026-10-15).
    public class LockCheckRulesTests
    {
        private static readonly DateTime Today = new(2026, 10, 15);

        // A 12-month Nuovo phone at 100 a day that started 100 days ago.
        private static LockCheckRow Phone(string make = "samsung", string? model = "SM-A055F", decimal paid = 2000 + 100 * 90,
            bool locked = false, string status = "enrolled", int? recoveryId = null, string? lastAction = null, bool? lastOk = null,
            int? lastPaidDaysAgo = null, bool restructured = false) => new()
        {
            AccountNo = 7004982, ContractId = 600, Make = make, Model = model, NuovoStatus = status, Locked = locked,
            Deposit = 2000, Daily = 100, TermInMonths = 12, StartDate = Today.AddDays(-100), Paid = paid,
            LastPaid = lastPaidDaysAgo.HasValue ? Today.AddDays(-lastPaidDaysAgo.Value) : null,
            RecoveryId = recoveryId, LastAction = lastAction, LastActionSuccess = lastOk, Restructured = restructured,
        };

        private static LockCheckRow Evaluated(LockCheckRow r, int minDays = LockCheckRules.DefaultMinDaysWithoutPayment)
        {
            LockCheckRules.Evaluate(r, Today, minDays);
            return r;
        }

        [Test]
        public void InArrearsAndNoPaymentFor90Days_SamsungNeedsANewLocker()
        {
            // The Collections pool rule: in arrears and no payment for 90 days.
            var r = Evaluated(Phone());   // never paid since it started 100 days ago
            Assert.Multiple(() =>
            {
                Assert.That(r.DaysSinceLastPayment, Is.EqualTo(100));
                Assert.That(r.Arrears, Is.EqualTo(1000m));
                Assert.That(r.Brand, Is.EqualTo(LockCheckBrand.Samsung));
                Assert.That(r.Stage, Is.EqualTo(LockCheckStage.NeedsNewLocker));
            });
        }

        [Test]
        public void PaidRecently_IsNotABadCustomerEvenIfBehind()
        {
            var r = Evaluated(Phone(lastPaidDaysAgo: 10));   // behind, but paying
            Assert.That(r.Arrears, Is.GreaterThan(0));
            Assert.That(r.Stage, Is.EqualTo(LockCheckStage.None));
            Assert.That(Evaluated(Phone(lastPaidDaysAgo: 10), minDays: 7).Stage, Is.EqualTo(LockCheckStage.NeedsNewLocker), "the days are adjustable");
        }

        [Test]
        public void UpToDate_IsNotABadCustomer()
        {
            Assert.That(Evaluated(Phone(paid: 2000 + 100 * 100)).Stage, Is.EqualTo(LockCheckStage.None));
        }

        // Restructure agreed 100 days ago at 50 a day: 5,000 due on it since.
        private static LockCheckRow Restructured(decimal paidSince, int? lastPaidDaysAgo, bool locked = false)
        {
            var r = Phone(restructured: true, lastPaidDaysAgo: lastPaidDaysAgo, locked: locked);
            r.RestructureAgreed = Today.AddDays(-100);
            r.RestructureDaily = 50;
            r.PaidSinceRestructure = paidSince;
            return r;
        }

        [Test]
        public void KeepingToARestructure_IsShownButNotTreatedAsBad()
        {
            var r = Evaluated(Restructured(paidSince: 5000, lastPaidDaysAgo: 2));
            Assert.That(r.Arrears, Is.EqualTo(0m), "arrears are on the restructured plan, not the contract");
            Assert.That(r.Stage, Is.EqualTo(LockCheckStage.Restructured));
            Assert.That(Evaluated(Restructured(5000, 2, locked: true)).Stage, Is.EqualTo(LockCheckStage.None));
        }

        [Test]
        public void BehindOnARestructureButStillPaying_IsNotBad()
        {
            var r = Evaluated(Restructured(paidSince: 3000, lastPaidDaysAgo: 20));
            Assert.That(r.Arrears, Is.EqualTo(2000m));
            Assert.That(r.BreachedRestructure, Is.False);
            Assert.That(r.Stage, Is.EqualTo(LockCheckStage.Restructured));
        }

        [Test]
        public void BreachedRestructure_IsABadCustomerAgain()
        {
            // Behind on the restructure and nothing paid for 90+ days.
            var r = Evaluated(Restructured(paidSince: 500, lastPaidDaysAgo: 95));
            Assert.Multiple(() =>
            {
                Assert.That(r.Arrears, Is.EqualTo(4500m));
                Assert.That(r.DaysBehind, Is.EqualTo(90), "4,500 at 50 a day");
                Assert.That(r.BreachedRestructure, Is.True);
                Assert.That(r.Stage, Is.EqualTo(LockCheckStage.NeedsNewLocker));
            });
        }

        [Test]
        public void RestructureArrearsStopAtTheTerm()
        {
            var r = Restructured(paidSince: 0, lastPaidDaysAgo: null);
            r.RestructureAgreed = Today.AddDays(-1000);   // longer ago than the 12-month term
            Assert.That(LockCheckRules.RestructureArrears(r, Today), Is.EqualTo(50m * 360));
        }

        [TestCase("TECNO", LockCheckStage.NeedsNewLocker)]
        [TestCase("INFINIX", LockCheckStage.NeedsNewLocker)]
        [TestCase("itel", LockCheckStage.NeedsNewLocker)]
        [TestCase("motorola", LockCheckStage.NuovoOnly)]
        [TestCase("TCL", LockCheckStage.NuovoOnly)]
        public void BrandDecidesTheRoute(string make, LockCheckStage stage)
        {
            Assert.That(Evaluated(Phone(make: make, model: null)).Stage, Is.EqualTo(stage));
        }

        [Test]
        public void SamsungWithNoMakeIsKnownByItsModel()
        {
            Assert.That(Evaluated(Phone(make: "", model: "SM-A165F/DS")).Brand, Is.EqualTo(LockCheckBrand.Samsung));
        }

        [Test]
        public void LockedOnNuovo_NothingToDo()
        {
            Assert.That(Evaluated(Phone(locked: true)).Stage, Is.EqualTo(LockCheckStage.None), "Nuovo is doing its job");
        }

        [Test]
        public void NuovoOnlyPhoneWithALockSent_MovesToLockSent()
        {
            Assert.That(Evaluated(Phone(make: "TCL", model: null, lastAction: LockCheckAction.NuovoLock, lastOk: true)).Stage, Is.EqualTo(LockCheckStage.LockSent));
            Assert.That(Evaluated(Phone(make: "TCL", model: null, lastAction: LockCheckAction.NuovoLock, lastOk: false)).Stage, Is.EqualTo(LockCheckStage.NuovoOnly), "a failed send doesn't count");
        }

        [Test]
        public void AlreadyInRecovery_ShowsAsInRecovery()
        {
            Assert.That(Evaluated(Phone(recoveryId: 12)).Stage, Is.EqualTo(LockCheckStage.InRecovery));
        }

        [Test]
        public void UnregisteredAndOwing_IsLostFromNuovo()
        {
            // 3516628 Nyanya: Motorola, 4,430 of 29,200 paid, un_registered.
            var r = Evaluated(Phone(make: "motorola", model: "moto e30", status: "un_registered", locked: true, paid: 4430));
            Assert.That(r.Stage, Is.EqualTo(LockCheckStage.LostFromNuovo));
        }

        [Test]
        public void PaidOff_IsFullyPaidEvenIfUnregistered()
        {
            // Nuovo releases paid-off phones, so un_registered is normal then.
            var r = Evaluated(Phone(status: "un_registered", paid: 2000 + 100 * 360));
            Assert.That(r.FullyPaid, Is.True);
            Assert.That(r.Stage, Is.EqualTo(LockCheckStage.FullyPaidOpen));
        }

        [Test]
        public void AShillingShortCountsAsPaid()
        {
            // 7004982 Centrilla: 27,797 of 27,798.
            Assert.That(Evaluated(Phone(paid: 2000 + 100 * 360 - 1)).FullyPaid, Is.True);
            Assert.That(Evaluated(Phone(paid: 2000 + 100 * 360 - 5)).FullyPaid, Is.False);
        }

        [Test]
        public void NoStartDate_IsNeverFullyPaid()
        {
            var r = Phone(paid: 0);
            r.StartDate = null;
            Assert.That(Evaluated(r).FullyPaid, Is.False);
        }

        [Test]
        public void ActionsFollowTheStage()
        {
            Assert.Multiple(() =>
            {
                Assert.That(LockCheckRules.CanSendToRecovery(LockCheckStage.NeedsNewLocker), Is.True);
                Assert.That(LockCheckRules.CanSendToRecovery(LockCheckStage.NuovoOnly), Is.False);
                Assert.That(LockCheckRules.CanSendNuovoLock(LockCheckStage.NuovoOnly), Is.True);
                Assert.That(LockCheckRules.CanSendNuovoLock(LockCheckStage.NeedsNewLocker), Is.False);
                Assert.That(LockCheckRules.CanCloseAndRelease(LockCheckStage.FullyPaidOpen), Is.True);
                Assert.That(LockCheckRules.CanCloseAndRelease(LockCheckStage.InRecovery), Is.False);
            });
        }
    }
}
