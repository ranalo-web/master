using Ranalo.Services;
using Ranalo.SumsungKnox.Models;

namespace Ranolo.Web.Tests
{
    // Knox's own status next to the service we send through (2026-11-10).
    public class KnoxStatusTests
    {
        [TestCase("Enrolled", true)]
        [TestCase("Locked", true)]
        [TestCase("active", true)]
        [TestCase("Accepted", false)]
        [TestCase("Activating", false)]
        [TestCase("Completed", false)]
        [TestCase(null, false)]
        public void Active_only_when_knox_can_lock_it(string? status, bool expected)
        {
            Assert.That(KnoxStatusRules.Active(status), Is.EqualTo(expected));
        }

        // 6607056 on 10 Nov: Knox "Activating", we send to Nuovo, in Recovery.
        [Test]
        public void Phone_in_recovery_is_not_listed_while_knox_activates_it()
        {
            Assert.That(KnoxStatusRules.Check(1, "Activating", openRecovery: true), Is.EqualTo(KnoxStatusRules.Problem.None));
            Assert.That(KnoxStatusRules.Check(1, "Activating", openRecovery: false), Is.EqualTo(KnoxStatusRules.Problem.None));
        }

        [Test]
        public void Active_in_knox_but_sent_to_nuovo_is_listed_unless_recovery_is_moving_it()
        {
            Assert.That(KnoxStatusRules.Check(1, "Enrolled", openRecovery: false), Is.EqualTo(KnoxStatusRules.Problem.ActiveInKnoxButSentElsewhere));
            Assert.That(KnoxStatusRules.Check(3, "Locked", openRecovery: false), Is.EqualTo(KnoxStatusRules.Problem.ActiveInKnoxButSentElsewhere));
            Assert.That(KnoxStatusRules.Check(1, "Enrolled", openRecovery: true), Is.EqualTo(KnoxStatusRules.Problem.None));
        }

        // 41109528 on 10 Nov: we send to Knox, Knox says "Accepted".
        [TestCase("Accepted")]
        [TestCase("Activating")]
        [TestCase("Completed")]
        [TestCase(KnoxStatusRules.NotInKnox)]
        public void Sent_to_knox_but_not_active_is_listed(string status)
        {
            Assert.That(KnoxStatusRules.Check(2, status, openRecovery: false), Is.EqualTo(KnoxStatusRules.Problem.SentToKnoxButNotActive));
        }

        [Test]
        public void Knox_phone_that_knox_has_active_is_fine_and_unread_is_not_a_problem()
        {
            Assert.That(KnoxStatusRules.Check(2, "Enrolled", false), Is.EqualTo(KnoxStatusRules.Problem.None));
            Assert.That(KnoxStatusRules.Check(2, null, false), Is.EqualTo(KnoxStatusRules.Problem.None));
        }

        [Test]
        public void Explain_in_plain_words()
        {
            Assert.That(KnoxStatusRules.Explain("Accepted"), Does.Contain("can't be locked"));
            Assert.That(KnoxStatusRules.Explain("Activating"), Does.Contain("online"));
            Assert.That(KnoxStatusRules.Explain("Weird"), Is.EqualTo("Weird: not active"));
        }

        [Test]
        public void From_knox_list_reads_times_and_ignores_zero()
        {
            var s = KnoxStatusRules.From(new DeviceDto { Imei = "352485227145007", Status = " Activating ", LastSeen = 0, RelockTimestamp = 1794256000000, IsDeviceOffline = true });
            Assert.That(s.Status, Is.EqualTo("Activating"));
            Assert.That(s.LastSeenUtc, Is.Null);
            Assert.That(s.RelockUtc, Is.EqualTo(DateTimeOffset.FromUnixTimeMilliseconds(1794256000000).UtcDateTime));
            Assert.That(s.Offline, Is.True);
        }
    }
}
