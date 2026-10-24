using Ranalo.DataStore.DataModels;
using Ranalo.Models;
using Ranalo.Services;

namespace Ranolo.Web.Tests
{
    // Legacy (no-commission) accounts: nothing earned or counted, and with
    // Recover arrears on, the shortfall is deducted from the dealer only.
    public class CommissionExclusionTests
    {
        private static CommissionInputs Inputs(decimal arrears = 0m, decimal agentPaid = 0m, decimal dealerPaid = 0m,
            CommissionExclusion? exclusion = null, bool hasAgent = true) => new()
        {
            Deposit = 4_000m,
            DaysSinceStart = 100,
            Arrears = arrears,
            TotalPaid = 20_000m,
            BuyingPrice = 10_000m,
            HasAgent = hasAgent,
            HasWooOrder = true,
            AgentPaid = agentPaid,
            DealerPaid = dealerPaid,
            Exclusion = exclusion,
        };

        private static CommissionExclusion Legacy(bool recover = false) =>
            new() { Reason = CommissionExclusionReason.Legacy, RecoverArrears = recover };

        private static CommissionAccount Account(long id, CommissionInputs inputs, double daysPastLock = 0, int agentId = 7) => new()
        {
            AccountId = id,
            ContractId = id.ToString(),
            AgentId = inputs.HasAgent ? agentId : null,
            DealerId = 1,
            DealerName = "Lunich",
            AgentName = "Agent",
            BuyingPrice = inputs.BuyingPrice,
            DaysPastLock = daysPastLock,
            StartDate = new DateTime(2026, 6, 1).AddDays(id),
            Commission = CommissionCalculator.Calculate(inputs),
        };

        [Test]
        public void LegacyAccount_EarnsNothingForAnyone()
        {
            var c = CommissionCalculator.Calculate(Inputs(agentPaid: 2_000m, dealerPaid: 1_500m, exclusion: Legacy()));

            Assert.That(c.NoCommission, Is.True);
            Assert.That(c.AgentEarned, Is.EqualTo(0m));
            Assert.That(c.AgentNet, Is.EqualTo(0m));
            Assert.That(c.DealerCommission, Is.EqualTo(0m));
            Assert.That(c.DealerBalance, Is.EqualTo(0m));
            Assert.That(c.BonusAtRisk, Is.False);
        }

        [Test]
        public void LegacyAccount_OldPaymentsKeptForDisplayButOutOfTotals()
        {
            var c = CommissionCalculator.Calculate(Inputs(agentPaid: 2_000m, dealerPaid: 1_500m, exclusion: Legacy()));

            Assert.That(c.AgentPaid, Is.EqualTo(0m));
            Assert.That(c.DealerPaid, Is.EqualTo(0m));
            Assert.That(c.AgentPaidBeforeRules, Is.EqualTo(2_000m));
            Assert.That(c.DealerPaidBeforeRules, Is.EqualTo(1_500m));
        }

        [Test]
        public void LegacyAccount_ArrearsIgnoredUnlessRecovering()
        {
            var off = CommissionCalculator.Calculate(Inputs(arrears: -3_000m, exclusion: Legacy()));
            var on = CommissionCalculator.Calculate(Inputs(arrears: -3_000m, exclusion: Legacy(recover: true)));

            Assert.That(off.DealerArrearsDeducted, Is.EqualTo(0m));
            Assert.That(on.DealerArrearsDeducted, Is.EqualTo(3_000m));
            Assert.That(on.DealerBalance, Is.EqualTo(-3_000m));
            Assert.That(on.AgentArrearsDeducted, Is.EqualTo(0m), "legacy arrears are recovered from the dealer only");
        }

        [Test]
        public void Recovering_ReducesWhatTheDealerIsOwedOnOtherAccounts()
        {
            var earning = CommissionCalculator.Calculate(Inputs());
            var recovering = CommissionCalculator.Calculate(Inputs(arrears: -500m, dealerPaid: 9_999m, exclusion: Legacy(recover: true)));

            var alone = CommissionCalculator.PoolDealer(new[] { earning });
            var pooled = CommissionCalculator.PoolDealer(new[] { earning, recovering });

            Assert.That(pooled.Owed, Is.EqualTo(alone.Owed - 500m));
            Assert.That(pooled.Paid, Is.EqualTo(alone.Paid), "old payments don't count against new commission");
        }

        [Test]
        public void Recovering_NeverTakesMoreThanTheDealerEarned()
        {
            var earning = CommissionCalculator.Calculate(Inputs());
            var huge = CommissionCalculator.Calculate(Inputs(arrears: -1_000_000m, exclusion: Legacy(recover: true)));

            var pool = CommissionCalculator.PoolDealer(new[] { earning, huge });

            Assert.That(pool.Owed, Is.EqualTo(0m));
            Assert.That(pool.Withheld, Is.EqualTo(pool.Earned));
        }

        [Test]
        public void LegacyAccountsDontCountTowardTheDefaultRate()
        {
            // Two earning accounts, both fine; three legacy accounts badly past lock.
            var accounts = new List<CommissionAccount>
            {
                Account(1, Inputs()),
                Account(2, Inputs()),
                Account(3, Inputs(exclusion: Legacy()), daysPastLock: 60),
                Account(4, Inputs(exclusion: Legacy(recover: true)), daysPastLock: 60),
                Account(5, Inputs(exclusion: Legacy(recover: true)), daysPastLock: 60),
            };

            var dealer = CommissionPayees.Summarize(CommissionPayeeType.Dealer, 1, accounts);

            Assert.That(dealer.DefaultRatePct, Is.EqualTo(0m));
            Assert.That(dealer.IsSuspended, Is.False);
            Assert.That(dealer.Accounts, Is.EqualTo(2));
        }

        [Test]
        public void PayeeLists_DropLegacy_ExceptRecoveringOnTheDealerSide()
        {
            var accounts = new List<CommissionAccount>
            {
                Account(1, Inputs()),
                Account(2, Inputs(exclusion: Legacy()), agentId: 8),
                Account(3, Inputs(exclusion: Legacy(recover: true)), agentId: 9),
            };

            Assert.That(CommissionPayees.Agents(accounts).Select(a => a.PayeeId), Is.EqualTo(new[] { 7 }));
            Assert.That(CommissionPayees.CountsFor(CommissionPayeeType.Dealer, accounts[1]), Is.False);
            Assert.That(CommissionPayees.CountsFor(CommissionPayeeType.Dealer, accounts[2]), Is.True);
            Assert.That(CommissionPayees.CountsFor(CommissionPayeeType.Agent, accounts[2]), Is.False);
        }

        [Test]
        public void LegacyAccount_HasNothingToPay()
        {
            var a = Account(1, Inputs(agentPaid: 2_000m, exclusion: Legacy()));
            var p = CommissionPayees.ToPayoutAccount(CommissionPayeeType.Agent, a);

            Assert.That(CommissionPayoutRules.Unpaid(p), Is.EqualTo(0m));
            Assert.That(CommissionPayoutRules.UnpaidUpfront(p), Is.EqualTo(0m));
            Assert.That(CommissionPayoutRules.UnpaidBonus(p), Is.EqualTo(0m));
        }

        [Test]
        public void NoAgent_DealerBaseStillLosesTheAgentShare()
        {
            var direct = CommissionCalculator.Calculate(Inputs(hasAgent: false));
            var withAgent = CommissionCalculator.Calculate(Inputs());

            Assert.That(direct.AgentEarned, Is.EqualTo(0m));
            Assert.That(direct.DealerCommission, Is.EqualTo(withAgent.DealerCommission));
        }

        [Test]
        public void OnlyAdminsCanChangeStatus()
        {
            Assert.That(CommissionExclusionRules.Check(UserRole.Admin, 5, "Recover", "Legacy", "Lunich arrears"), Is.Null);
            Assert.That(CommissionExclusionRules.Check(UserRole.Dealer, 5, "Recover", "Legacy", "x"), Is.Not.Null);
            Assert.That(CommissionExclusionRules.Check(UserRole.Approver, 5, "Qualifies", null, "x"), Is.Not.Null);
        }

        [Test]
        public void ChangeNeedsANoteAndAReason()
        {
            Assert.That(CommissionExclusionRules.Check(UserRole.Admin, 5, "NoCommission", "Legacy", "  "), Is.Not.Null);
            Assert.That(CommissionExclusionRules.Check(UserRole.Admin, 5, "NoCommission", "Because", "x"), Is.Not.Null);
            Assert.That(CommissionExclusionRules.Check(UserRole.Admin, 5, "Qualifies", null, "Mali Mali account"), Is.Null);
            Assert.That(CommissionExclusionRules.Check(UserRole.Admin, 5, "Whatever", "Legacy", "x"), Is.Not.Null);
            Assert.That(CommissionExclusionRules.Check(UserRole.Admin, 0, "Qualifies", null, "x"), Is.Not.Null);
            Assert.That(CommissionExclusionRules.Check(UserRole.Admin, 5, "Recover", "Legacy", new string('x', 501)), Is.Not.Null);
        }

        [Test]
        public void StatusFilterMatches()
        {
            var earning = CommissionCalculator.Calculate(Inputs());
            var legacy = CommissionCalculator.Calculate(Inputs(exclusion: Legacy()));
            var recovering = CommissionCalculator.Calculate(Inputs(exclusion: Legacy(recover: true)));

            Assert.That(CommissionExclusionRules.Matches(earning, "Qualifies"), Is.True);
            Assert.That(CommissionExclusionRules.Matches(legacy, "Qualifies"), Is.False);
            Assert.That(CommissionExclusionRules.Matches(legacy, "NoCommission"), Is.True);
            Assert.That(CommissionExclusionRules.Matches(recovering, "NoCommission"), Is.False);
            Assert.That(CommissionExclusionRules.Matches(recovering, "recover"), Is.True);
            Assert.That(CommissionExclusionRules.Matches(legacy, null), Is.True);
        }
    }
}
