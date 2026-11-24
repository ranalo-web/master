using NUnit.Framework;
using Ranalo.Models;
using Ranalo.Services;

namespace Ranolo.Web.Tests
{
    public class SalesTeamRulesTests
    {
        private static readonly DateTime Today = new(2026, 11, 24);

        private static CommissionAccount Sale(int? agent, int dealer, DateTime start, double daysPastLock = 0, bool noCommission = false) => new()
        {
            AgentId = agent,
            DealerId = dealer,
            StartDate = start,
            DaysPastLock = daysPastLock,
            Commission = new CommissionBreakdown { NoCommission = noCommission },
        };

        [Test]
        public void CountsSalesThisMonthAndArrears()
        {
            var accounts = new[]
            {
                Sale(28, 14, new DateTime(2026, 11, 3)),
                Sale(28, 14, new DateTime(2026, 10, 30), daysPastLock: 20),   // more than 7 days past lock
                Sale(28, 14, new DateTime(2026, 11, 20)),
                Sale(null, 14, new DateTime(2026, 11, 21)),                    // direct sale: dealer only
            };

            var agent = SalesTeamRules.ByAgent(accounts, Today)[28];
            Assert.That(agent.Sales, Is.EqualTo(3));
            Assert.That(agent.SalesThisMonth, Is.EqualTo(2));
            Assert.That(agent.InArrears, Is.EqualTo(1));
            Assert.That(agent.DefaultRatePct, Is.EqualTo(33.3m));
            Assert.That(agent.CommissionSuspended, Is.True);

            var dealer = SalesTeamRules.ByDealer(accounts, Today)[14];
            Assert.That(dealer.Sales, Is.EqualTo(4));
            Assert.That(dealer.SalesThisMonth, Is.EqualTo(3));
            Assert.That(dealer.InArrears, Is.EqualTo(1));
        }

        [Test]
        public void NoCommissionSales_CountAsSalesOnly()
        {
            var accounts = new[] { Sale(31, 14, new DateTime(2026, 9, 1), daysPastLock: 30, noCommission: true) };
            var agent = SalesTeamRules.ByAgent(accounts, Today)[31];
            Assert.That(agent.Sales, Is.EqualTo(1));
            Assert.That(agent.InArrears, Is.EqualTo(0));
            Assert.That(agent.DefaultRatePct, Is.EqualTo(0));
        }
    }
}
