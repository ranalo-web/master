using Ranalo.DataStore;
using Ranalo.Models;
using Ranalo.ScheduledServices;
using Ranalo.Services;

namespace Ranolo.Web.Tests
{
    // Pool accounts go to the Approvers first (2026-11-06).
    public class ApproverFollowUpTests
    {
        private static CollectionContractStanding Standing(long contract, decimal shortfall, int days) =>
            new() { AccountNo = contract * 10, ContractId = contract, Shortfall = shortfall, DaysSinceLastPayment = days };

        [Test]
        public void A_pool_account_goes_to_the_approvers_once()
        {
            var s = Standing(1, 5000m, 120);
            Assert.That(ApproverFollowUpRules.ShouldAdd(s, hasOpenCase: false, hasActiveFollowUp: false), Is.True);
            Assert.That(ApproverFollowUpRules.ShouldAdd(s, hasOpenCase: false, hasActiveFollowUp: true), Is.False);
            Assert.That(ApproverFollowUpRules.ShouldAdd(s, hasOpenCase: true, hasActiveFollowUp: false), Is.False);
            Assert.That(ApproverFollowUpRules.ShouldAdd(Standing(1, 5000m, 30), false, false), Is.False, "paid within 90 days");
            Assert.That(ApproverFollowUpRules.ShouldAdd(Standing(1, 0m, 200), false, false), Is.False, "not behind");
        }

        [Test]
        public void Follow_ups_close_only_from_the_facts()
        {
            Assert.That(ApproverFollowUpRules.CloseReason(Standing(1, 5000m, 120), hasOpenCase: true), Is.EqualTo(ApproverFollowUpRules.InCollections));
            Assert.That(ApproverFollowUpRules.CloseReason(null, hasOpenCase: false), Is.EqualTo(ApproverFollowUpRules.ContractEnded));
            Assert.That(ApproverFollowUpRules.CloseReason(Standing(1, 5000m, 3), hasOpenCase: false), Is.EqualTo(ApproverFollowUpRules.Paying));
            Assert.That(ApproverFollowUpRules.CloseReason(Standing(1, 5000m, 120), hasOpenCase: false), Is.Null);
        }

        [Test]
        public void The_admin_pool_leaves_out_only_what_approvers_are_working_on()
        {
            Assert.That(ApproverFollowUpRules.InAdminPool(true, null), Is.True);
            Assert.That(ApproverFollowUpRules.InAdminPool(true, ApproverFollowUpRules.Open), Is.False);
            Assert.That(ApproverFollowUpRules.InAdminPool(true, ApproverFollowUpRules.ToCollections), Is.True);
            Assert.That(ApproverFollowUpRules.InAdminPool(false, null), Is.False);
        }

        [Test]
        public async Task The_sync_adds_new_pool_accounts_and_closes_finished_ones()
        {
            var standings = new List<CollectionContractStanding>
            {
                Standing(1, 5000m, 120),   // new to the pool -> added
                Standing(2, 5000m, 120),   // already with approvers -> left
                Standing(3, 5000m, 2),     // with approvers, just paid -> closed Paying
                Standing(4, 5000m, 150),   // collections case open -> not added
                Standing(6, 5000m, 150),   // with approvers, now a collections case -> notes copied, closed
            };
            var cases = new List<CollectionCaseRow>
            {
                new() { CaseId = 400, ContractId = 4, Status = CollectionCaseStatus.Open },
                new() { CaseId = 600, ContractId = 6, Status = CollectionCaseStatus.Open },
            };
            var active = new List<ApproverFollowUp>
            {
                new() { Id = 20, ContractId = 2, Status = ApproverFollowUpRules.Open },
                new() { Id = 30, ContractId = 3, Status = ApproverFollowUpRules.Open },
                new() { Id = 50, ContractId = 5, Status = ApproverFollowUpRules.ToCollections }, // contract ended
                new() { Id = 60, ContractId = 6, Status = ApproverFollowUpRules.ToCollections },
            };

            var (collections, _) = InterfaceFake<ICollectionsRepository>.Create(new()
            {
                ["GetStandingsAsync"] = _ => Task.FromResult(standings),
                ["GetCasesAsync"] = _ => Task.FromResult(cases),
            });
            var addedContracts = new List<long>();
            var closed = new List<(int, string)>();
            var copied = new List<(int FollowUp, int Case)>();
            var (followUps, _) = InterfaceFake<IApproverFollowUpRepository>.Create(new()
            {
                ["GetActiveAsync"] = _ => Task.FromResult(active),
                ["CloseAsync"] = a => { closed.Add(((int)a![0]!, (string)a[1]!)); return Task.CompletedTask; },
                ["AddAsync"] = a => { addedContracts.Add((long)a![1]!); return Task.FromResult(true); },
                ["CopyNotesToCaseAsync"] = a => { copied.Add(((int)a![0]!, (int)a[1]!)); return Task.FromResult(3); },
            });

            var (added, closedCount) = await ScheduledApproverFollowUps.SyncAsync(collections, followUps);

            Assert.That(addedContracts, Is.EqualTo(new long[] { 1 }));
            Assert.That(closed, Is.EquivalentTo(new[] { (30, ApproverFollowUpRules.Paying), (50, ApproverFollowUpRules.ContractEnded), (60, ApproverFollowUpRules.InCollections) }));
            Assert.That(copied, Is.EqualTo(new[] { (60, 600) }), "the approvers' notes go onto the new case");
            Assert.That((added, closedCount), Is.EqualTo((1, 3)));
        }
    }
}
