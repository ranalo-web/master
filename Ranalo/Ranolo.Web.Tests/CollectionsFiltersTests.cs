using Ranalo.Models;
using Ranalo.Services;

namespace Ranolo.Web.Tests
{
    // My Collections filters: worked on / not worked on / follow up (2026-10-23).
    public class CollectionsFiltersTests
    {
        private static readonly DateTime Today = new(2026, 10, 23);
        private const int Me = 7;

        private static CollectionCaseRow Held(CollectionCallNote? call = null, int flags = 0, DateTime? lastPayment = null) => new()
        {
            CollectorUserId = Me,
            HeldSince = new DateTime(2026, 10, 1),
            LastCall = call,
            OpenFlagCount = flags,
            LastPaymentAt = lastPayment,
        };

        private static CollectionCallNote Call(DateTime at, DateTime? promised = null, int by = Me) => new()
        {
            CollectorUserId = by,
            CreatedAt = at,
            PromisedDate = promised,
            Note = "spoke",
        };

        [Test]
        public void NoCallNoFlag_NotWorked()
        {
            var c = Held();
            Assert.That(CollectionsRules.IsWorked(c), Is.False);
            Assert.That(CollectionsRules.MatchesFilter(c, CollectionsRules.Filter.NotWorked, Today), Is.True);
        }

        [Test]
        public void MyCall_Worked()
        {
            Assert.That(CollectionsRules.IsWorked(Held(Call(new DateTime(2026, 10, 20)))), Is.True);
        }

        [Test]
        public void Flagged_Worked()
        {
            Assert.That(CollectionsRules.IsWorked(Held(flags: 1)), Is.True);
        }

        [Test]
        public void PreviousCollectorsCall_NotWorkedByMe()
        {
            Assert.That(CollectionsRules.IsWorked(Held(Call(new DateTime(2026, 10, 20), by: 99))), Is.False);
            Assert.That(CollectionsRules.IsWorked(Held(Call(new DateTime(2026, 9, 20)))), Is.False);
        }

        [Test]
        public void NoPromise_FollowUpAfter7Days()
        {
            Assert.That(CollectionsRules.IsDueForFollowUp(Held(Call(new DateTime(2026, 10, 17, 15, 0, 0))), Today), Is.False);
            Assert.That(CollectionsRules.IsDueForFollowUp(Held(Call(new DateTime(2026, 10, 16, 15, 0, 0))), Today), Is.True);
        }

        [Test]
        public void Promise_FollowUpOnceTheDateHasPassed()
        {
            var call = new DateTime(2026, 10, 2);
            Assert.That(CollectionsRules.IsDueForFollowUp(Held(Call(call, promised: Today)), Today), Is.False);
            Assert.That(CollectionsRules.IsDueForFollowUp(Held(Call(call, promised: Today.AddDays(-1))), Today), Is.True);
        }

        [Test]
        public void PromiseKept_PaymentAfterTheCall()
        {
            var call = new DateTime(2026, 10, 10, 9, 0, 0);
            Assert.That(CollectionsRules.PaidSinceCall(Held(Call(call), lastPayment: call.AddDays(2))), Is.True);
            Assert.That(CollectionsRules.PaidSinceCall(Held(Call(call), lastPayment: call.AddDays(-2))), Is.False);
            Assert.That(CollectionsRules.PaidSinceCall(Held(Call(call))), Is.False);
        }

        [TestCase(null, "all")]
        [TestCase("", "all")]
        [TestCase("junk", "all")]
        [TestCase("worked", "worked")]
        [TestCase("notworked", "notworked")]
        [TestCase("followup", "followup")]
        public void Filter_Clean(string? value, string expected)
        {
            Assert.That(CollectionsRules.Filter.Clean(value), Is.EqualTo(expected));
        }
    }
}
