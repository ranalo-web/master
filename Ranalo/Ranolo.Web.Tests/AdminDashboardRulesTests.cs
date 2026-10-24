using Ranalo.Models;
using Ranalo.Services;

namespace Ranolo.Web.Tests
{
    // Admin Dashboard periods, profit and "what to do" (2026-10-24).
    public class AdminDashboardRulesTests
    {
        private static readonly DateTime Today = new(2026, 10, 24);

        [Test]
        public void Month_ComparedWithTheSameDaysLastMonth()
        {
            var w = AdminDashboardRules.ResolvePeriod("month", Today);
            Assert.That(w.From, Is.EqualTo(new DateTime(2026, 10, 1)));
            Assert.That(w.ToExclusive, Is.EqualTo(new DateTime(2026, 10, 25)));
            Assert.That(w.PriorFrom, Is.EqualTo(new DateTime(2026, 9, 1)));
            Assert.That(w.PriorToExclusive, Is.EqualTo(new DateTime(2026, 9, 25)));
        }

        [Test]
        public void Month_On31st_PriorStopsAtTheMonthStart()
        {
            var w = AdminDashboardRules.ResolvePeriod("month", new DateTime(2026, 3, 31));
            Assert.That(w.PriorFrom, Is.EqualTo(new DateTime(2026, 2, 1)));
            Assert.That(w.PriorToExclusive, Is.EqualTo(new DateTime(2026, 3, 1)));
        }

        [TestCase(null, "month")]
        [TestCase("junk", "month")]
        [TestCase("WEEK", "week")]
        [TestCase("ytd", "ytd")]
        [TestCase("year", "year")]
        public void Period_Cleaned(string? value, string expected)
        {
            Assert.That(AdminDashboardRules.ResolvePeriod(value, Today).Period, Is.EqualTo(expected));
        }

        [Test]
        public void Week_IsTheLast7DaysAndThe7Before()
        {
            var w = AdminDashboardRules.ResolvePeriod("week", Today);
            Assert.That(w.From, Is.EqualTo(new DateTime(2026, 10, 18)));
            Assert.That(w.PriorFrom, Is.EqualTo(new DateTime(2026, 10, 11)));
            Assert.That(w.PriorToExclusive, Is.EqualTo(w.From));
        }

        [Test]
        public void Profit_TaxOnlyOnAProfit()
        {
            var profit = new AdminProfit { Revenue = 100_000, CostOfDevices = 40_000, Commissions = 10_000, OperatingExpenses = 10_000 };
            Assert.That(profit.ProfitBeforeTax, Is.EqualTo(40_000m));
            Assert.That(profit.Tax, Is.EqualTo(12_000m));
            Assert.That(profit.NetProfit, Is.EqualTo(28_000m));
            Assert.That(profit.MarginPct, Is.EqualTo(28.0m));

            var loss = new AdminProfit { Revenue = 10_000, CostOfDevices = 40_000 };
            Assert.That(loss.Tax, Is.EqualTo(0m));
            Assert.That(loss.NetProfit, Is.EqualTo(-30_000m));
            Assert.That(loss.BiggestCost.Name, Is.EqualTo("Device cost"));
        }

        [Test]
        public void Actions_LossComesFirst_AndNamesTheBiggestCost()
        {
            var m = new AdminDashboardViewModel
            {
                Profit = new AdminProfit { Revenue = 10_000, CostOfDevices = 40_000, OperatingExpenses = 1 },
                WriteOffsPendingCount = 2,
            };
            var actions = AdminDashboardRules.BuildActions(m);
            Assert.That(actions[0].Level, Is.EqualTo("danger"));
            Assert.That(actions[0].Text, Does.Contain("Making a loss").And.Contain("device cost"));
            Assert.That(actions.Last().Text, Does.Contain("write-offs waiting"));
        }

        [Test]
        public void Actions_FlagNoExpensesThisMonth_AndLateDealers()
        {
            var m = new AdminDashboardViewModel
            {
                Period = "month",
                Profit = new AdminProfit { Revenue = 100_000 },
                DealerPerformance = new()
                {
                    new() { DealerName = "Risky", Accounts = 20, OpenCount = 20, BehindPct = 45, BehindAmount = 90_000 },
                    new() { DealerName = "Small", Accounts = 30, OpenCount = 3, BehindPct = 90 },
                    new() { DealerName = "Solid", Accounts = 50, OpenCount = 50, BehindPct = 5, Revenue = 300_000 },
                },
            };
            var texts = AdminDashboardRules.BuildActions(m).Select(a => a.Text).ToList();
            Assert.That(texts, Has.Some.Contains("No operating expenses logged"));
            Assert.That(texts, Has.Some.StartsWith("Risky: 45%"));
            Assert.That(texts, Has.None.StartsWith("Small"), "too few accounts to judge");
            Assert.That(texts, Has.Some.Contains("Grow with Solid"));
        }

        [Test]
        public void Actions_ThinCushionBetweenRepaymentAndBreakEven_IsUrgent()
        {
            var m = new AdminDashboardViewModel { RepaymentRatePct = 60, BreakEvenCollectionPct = 55 };
            var action = AdminDashboardRules.BuildActions(m).Single(a => a.Text.StartsWith("Customers have paid"));
            Assert.That(action.Level, Is.EqualTo("danger"));
        }
    }
}
