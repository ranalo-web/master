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
            bool locked = false, string status = "enrolled", int? recoveryId = null, string? lastAction = null, bool? lastOk = null) => new()
        {
            AccountNo = 7004982, ContractId = 600, Make = make, Model = model, NuovoStatus = status, Locked = locked,
            Deposit = 2000, Daily = 100, TermInMonths = 12, StartDate = Today.AddDays(-100), Paid = paid,
            RecoveryId = recoveryId, LastAction = lastAction, LastActionSuccess = lastOk,
        };

        private static LockCheckRow Evaluated(LockCheckRow r, int minDays = 7)
        {
            LockCheckRules.Evaluate(r, Today, minDays);
            return r;
        }

        [Test]
        public void TenDaysBehindAndUnlocked_SamsungNeedsANewLocker()
        {
            var r = Evaluated(Phone());
            Assert.Multiple(() =>
            {
                Assert.That(r.DaysBehind, Is.EqualTo(10));
                Assert.That(r.Arrears, Is.EqualTo(1000m));
                Assert.That(r.Brand, Is.EqualTo(LockCheckBrand.Samsung));
                Assert.That(r.Stage, Is.EqualTo(LockCheckStage.NeedsNewLocker));
            });
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
        public void LessBehindThanTheThreshold_NothingToDo()
        {
            var r = Phone(paid: 2000 + 100 * 95);   // 5 days behind
            Assert.That(Evaluated(r).Stage, Is.EqualTo(LockCheckStage.None));
            Assert.That(Evaluated(Phone(paid: 2000 + 100 * 95), minDays: 3).Stage, Is.EqualTo(LockCheckStage.NeedsNewLocker), "the threshold is adjustable");
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
