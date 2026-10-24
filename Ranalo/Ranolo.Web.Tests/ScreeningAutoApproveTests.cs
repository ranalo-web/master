using Ranalo.Models;
using Ranalo.Services;

namespace Ranolo.Web.Tests
{
    // The call that completes screening approves the order (2026-10-24).
    public class ScreeningAutoApproveTests
    {
        [Test]
        public void Passed_AndWaiting_Approves()
        {
            Assert.That(ScreeningRules.ShouldAutoApprove("approval-waiting", ScreeningStatus.Passed), Is.True);
            Assert.That(ScreeningRules.ShouldAutoApprove("Approval-Waiting", ScreeningStatus.Passed), Is.True);
        }

        [TestCase(ScreeningStatus.InProgress)]
        [TestCase(ScreeningStatus.NotStarted)]
        [TestCase(ScreeningStatus.NotRequired)]
        public void NotPassed_DoesNotApprove(ScreeningStatus status)
        {
            Assert.That(ScreeningRules.ShouldAutoApprove("approval-waiting", status), Is.False);
        }

        [TestCase("approved")]
        [TestCase("rejected")]
        [TestCase("cancelled")]
        [TestCase(null)]
        public void OnlyAWaitingOrderIsApproved(string? status)
        {
            Assert.That(ScreeningRules.ShouldAutoApprove(status, ScreeningStatus.Passed), Is.False);
        }

        [TestCase("approval-waiting", true)]
        [TestCase("approved", true)]
        [TestCase("processing", false)]
        [TestCase("pending", false)]
        [TestCase(null, false)]
        public void ApprovalList_ShowsWaitingAndApprovedOnly(string? status, bool listed)
        {
            Assert.That(ScreeningRules.IsOnApprovalList(status), Is.EqualTo(listed));
        }
    }
}
