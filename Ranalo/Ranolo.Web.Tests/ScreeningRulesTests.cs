using Ranalo.Models;
using Ranalo.Services;

namespace Ranolo.Web.Tests
{
    // Customer screening and welfare calls (2026-10-21).
    public class ScreeningRulesTests
    {
        private static readonly DateTime Start = new(2026, 10, 21, 9, 0, 0, DateTimeKind.Utc);

        private static ScreeningCall CustomerCall(bool reached = true, bool identity = true, bool terms = true, bool lockUnderstood = true) =>
            new() { CallType = ScreeningCall.Customer, Reached = reached, IdentityConfirmed = identity, TermsConfirmed = terms, LockUnderstood = lockUnderstood };

        private static ScreeningCall KinCall(bool reached = true, bool knows = true, bool relationship = true) =>
            new() { CallType = ScreeningCall.NextOfKin, NextOfKinNo = 1, Reached = reached, KnowsCustomer = knows, RelationshipConfirmed = relationship };

        // ---- Which orders ---------------------------------------------------

        [Test]
        public void OrdersFromTheStartNeedScreening_EarlierOnesDont()
        {
            Assert.That(ScreeningRules.RequiresScreening(Start.AddMinutes(1), Start), Is.True);
            Assert.That(ScreeningRules.RequiresScreening(Start.AddDays(-1), Start), Is.False);
        }

        [Test]
        public void BeforeTheMigrationRuns_NothingNeedsScreening()
        {
            Assert.That(ScreeningRules.RequiresScreening(Start, null), Is.False);
        }

        // ---- Passing --------------------------------------------------------

        [Test]
        public void CustomerAndOneNextOfKin_Passes()
        {
            var calls = new[] { CustomerCall(), KinCall() };
            Assert.That(ScreeningRules.Status(true, calls), Is.EqualTo(ScreeningStatus.Passed));
            Assert.That(ScreeningRules.CannotApprove(ScreeningStatus.Passed), Is.Null);
        }

        [Test]
        public void CustomerOnly_NotPassed()
        {
            var calls = new[] { CustomerCall() };
            Assert.That(ScreeningRules.Status(true, calls), Is.EqualTo(ScreeningStatus.InProgress));
            Assert.That(ScreeningRules.CannotApprove(ScreeningStatus.InProgress), Is.Not.Null);
        }

        [Test]
        public void NoCalls_NotStarted_CannotApprove()
        {
            Assert.That(ScreeningRules.Status(true, Array.Empty<ScreeningCall>()), Is.EqualTo(ScreeningStatus.NotStarted));
            Assert.That(ScreeningRules.CannotApprove(ScreeningStatus.NotStarted), Is.Not.Null);
        }

        [TestCase(false, true, true, true)]
        [TestCase(true, false, true, true)]
        [TestCase(true, true, false, true)]
        [TestCase(true, true, true, false)]
        public void CustomerCallMissingAnything_DoesntCount(bool reached, bool identity, bool terms, bool lockUnderstood)
        {
            Assert.That(ScreeningRules.CustomerCallPassed(CustomerCall(reached, identity, terms, lockUnderstood)), Is.False);
        }

        [TestCase(false, true, true)]
        [TestCase(true, false, true)]
        [TestCase(true, true, false)]
        public void NextOfKinCallMissingAnything_DoesntCount(bool reached, bool knows, bool relationship)
        {
            Assert.That(ScreeningRules.NextOfKinCallPassed(KinCall(reached, knows, relationship)), Is.False);
        }

        [Test]
        public void UnansweredCallsThenAPass_Passes()
        {
            var calls = new[] { CustomerCall(reached: false), KinCall(reached: false), CustomerCall(), KinCall() };
            Assert.That(ScreeningRules.Status(true, calls), Is.EqualTo(ScreeningStatus.Passed));
        }

        [Test]
        public void OldOrder_NotRequired_CanApprove()
        {
            Assert.That(ScreeningRules.Status(false, Array.Empty<ScreeningCall>()), Is.EqualTo(ScreeningStatus.NotRequired));
            Assert.That(ScreeningRules.CannotApprove(ScreeningStatus.NotRequired), Is.Null);
        }

        // ---- Enrolment ------------------------------------------------------

        [Test]
        public void NewOrder_MustBeApprovedToEnrol()
        {
            Assert.That(ScreeningRules.CannotEnrol("approval-waiting", required: true), Is.Not.Null);
            Assert.That(ScreeningRules.CannotEnrol("approved", required: true), Is.Null);
        }

        [Test]
        public void OldOrder_EnrolsAsBefore()
        {
            Assert.That(ScreeningRules.CannotEnrol("approval-waiting", required: false), Is.Null);
        }

        // ---- Welfare calls --------------------------------------------------

        [TestCase(3, WelfareStatus.NotDue)]
        [TestCase(7, WelfareStatus.Due)]
        [TestCase(30, WelfareStatus.Due)]
        [TestCase(31, WelfareStatus.Overdue)]
        public void WelfareCall_DueFromDay7_OverdueAfterDay30(int days, WelfareStatus expected)
        {
            Assert.That(ScreeningRules.Welfare(Start, Start.AddDays(days).AddHours(1), Array.Empty<WelfareCall>()), Is.EqualTo(expected));
        }

        [Test]
        public void WelfareCall_NoAnswer_StillDue_AnsweredIsDone()
        {
            var now = Start.AddDays(10);
            Assert.That(ScreeningRules.Welfare(Start, now, new[] { new WelfareCall { Outcome = WelfareCall.NoAnswer } }), Is.EqualTo(WelfareStatus.Due));
            Assert.That(ScreeningRules.Welfare(Start, now, new[] { new WelfareCall { Outcome = WelfareCall.Issue } }), Is.EqualTo(WelfareStatus.Done));
        }

        // ---- Not paying -----------------------------------------------------

        [Test]
        public void NotPaying_InArrearsInTheFirstMonthOnly()
        {
            Assert.That(ScreeningRules.NotPaying(Start.AddDays(-10), -50m, Start), Is.True);
            Assert.That(ScreeningRules.NotPaying(Start.AddDays(-10), 0m, Start), Is.False);
            Assert.That(ScreeningRules.NotPaying(Start.AddDays(-45), -50m, Start), Is.False);
            Assert.That(ScreeningRules.NotPaying(null, -50m, Start), Is.False);
        }
    }
}
