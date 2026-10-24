using Ranalo.Models;

namespace Ranalo.Services
{
    // Admin Dashboard periods and its "what to do" list (2026-10-24).
    public static class AdminDashboardRules
    {
        public record PeriodWindow(string Period, DateTime From, DateTime ToExclusive,
            DateTime PriorFrom, DateTime PriorToExclusive, string Label, string PriorLabel);

        // Each period runs to the end of today and is compared with the same
        // length of time just before it -- a month so far against the same
        // days of last month, so a half-month is never set against a full one.
        public static PeriodWindow ResolvePeriod(string? period, DateTime today)
        {
            today = today.Date;
            var tomorrow = today.AddDays(1);
            switch ((period ?? "").ToLowerInvariant())
            {
                case "week":
                    return new("week", today.AddDays(-6), tomorrow, today.AddDays(-13), today.AddDays(-6),
                        "the last 7 days", "the 7 days before");
                case "ytd":
                    var yearStart = new DateTime(today.Year, 1, 1);
                    return new("ytd", yearStart, tomorrow, yearStart.AddYears(-1), tomorrow.AddYears(-1),
                        "year to date", "the same period last year");
                case "year":
                    return new("year", tomorrow.AddYears(-1), tomorrow, tomorrow.AddYears(-2), tomorrow.AddYears(-1),
                        "the last 12 months", "the 12 months before");
                default:
                    var monthStart = new DateTime(today.Year, today.Month, 1);
                    var priorStart = monthStart.AddMonths(-1);
                    var priorEnd = priorStart.AddDays(today.Day);
                    return new("month", monthStart, tomorrow, priorStart, priorEnd < monthStart ? priorEnd : monthStart,
                        "this month so far", "the same days last month");
            }
        }

        public static decimal? ChangePct(decimal current, decimal prior) =>
            prior == 0 ? null : Math.Round((current - prior) / Math.Abs(prior) * 100, 1);

        // Dealers big enough for their arrears share to mean something.
        public const int MinDealerAccounts = 10;
        public const decimal DealerBehindAlertPct = 30;

        public static List<AdminAction> BuildActions(AdminDashboardViewModel m)
        {
            var actions = new List<AdminAction>();
            var p = m.Profit;

            if (p.Revenue > 0 || p.TotalCosts > 0)
            {
                if (p.NetProfit < 0)
                {
                    var (name, amount) = p.BiggestCost;
                    actions.Add(new()
                    {
                        Level = "danger",
                        Text = $"Making a loss {m.PeriodLabel}: costs KES {p.TotalCosts:N0} against KES {p.Revenue:N0} collected. Biggest cost is {name.ToLowerInvariant()} (KES {amount:N0})."
                            + (name == "Device cost" ? " Device cost is paid upfront, so a month of strong sales looks like a loss until the customers pay." : ""),
                        Link = "/financials",
                        LinkText = "Financials",
                    });
                }
                else
                {
                    actions.Add(new()
                    {
                        Level = "success",
                        Text = $"Profitable {m.PeriodLabel}: KES {p.NetProfit:N0} after tax, a {p.MarginPct:0.0}% margin.",
                    });
                }
            }

            var revenueChange = ChangePct(p.Revenue, m.PriorProfit.Revenue);
            if (revenueChange <= -10)
            {
                actions.Add(new()
                {
                    Level = "warning",
                    Text = $"Cash collected is down {Math.Abs(revenueChange.Value):0}% on {m.PriorLabel}. Push collections on accounts that have just gone late.",
                    Link = "/collections",
                    LinkText = "Collections",
                });
            }

            var newChange = ChangePct(m.NewInPeriod, m.NewPriorPeriod);
            if (newChange <= -20)
            {
                actions.Add(new()
                {
                    Level = "warning",
                    Text = $"New sales are down {Math.Abs(newChange.Value):0}% ({m.NewInPeriod:N0} vs {m.NewPriorPeriod:N0}). Check stock and dealer activity.",
                });
            }

            if (m.Period == "month" && p.OperatingExpenses == 0)
            {
                actions.Add(new()
                {
                    Level = "warning",
                    Text = "No operating expenses logged this month yet, so profit is overstated.",
                    Link = "/operating-expenses",
                    LinkText = "Log expenses",
                });
            }

            if (m.MissingDeviceCostCount > 0)
            {
                actions.Add(new()
                {
                    Level = "warning",
                    Text = $"{m.MissingDeviceCostCount:N0} accounts have no device cost recorded, so their cost is missing from profit.",
                    Link = "/cost-backfill",
                    LinkText = "Device Cost Backfill",
                });
            }

            if (m.RepaymentRatePct > 0 && m.BreakEvenCollectionPct > 0)
            {
                var cushion = m.RepaymentRatePct - m.BreakEvenCollectionPct;
                actions.Add(new()
                {
                    Level = cushion < 10 ? "danger" : "info",
                    Text = $"Customers have paid {m.RepaymentRatePct:0}% of what's due so far. A phone is paid for once {m.BreakEvenCollectionPct:0}% of its contract is collected"
                        + (cushion < 10 ? " -- too little cushion: tighten approvals and deposits." : $", a {cushion:0}-point cushion."),
                });
            }

            foreach (var d in m.DealerPerformance
                .Where(d => d.Accounts >= MinDealerAccounts && d.BehindPct >= DealerBehindAlertPct)
                .OrderByDescending(d => d.BehindPct)
                .Take(2))
            {
                actions.Add(new()
                {
                    Level = "danger",
                    Text = $"{d.DealerName}: {d.BehindPct:0}% of their {d.Accounts:N0} accounts are more than 7 days late (KES {d.BehindAmount:N0}), {d.BadCount:N0} of them bad customers over 90 days. Review their sales before sending more stock.",
                });
            }

            var best = m.DealerPerformance
                .Where(d => d.Accounts >= MinDealerAccounts && d.BehindPct < 20 && d.Revenue > 0)
                .OrderByDescending(d => d.Revenue)
                .FirstOrDefault();
            if (best != null)
            {
                actions.Add(new()
                {
                    Level = "success",
                    Text = $"Grow with {best.DealerName}: KES {best.Revenue:N0} collected this month with only {best.BehindPct:0}% late. A good dealer to give more stock.",
                });
            }

            if (m.NewInPeriod > 0 && m.PortfolioAtRiskPct >= 20)
            {
                actions.Add(new()
                {
                    Level = "warning",
                    Text = $"{m.PortfolioAtRiskPct:0}% of accounts sold {m.PeriodLabel} are already 30+ days late. Look at who approved them.",
                });
            }

            var restructuredNotPaying = m.RestructuredCount - m.RestructuredPayingCount;
            if (restructuredNotPaying > 0)
            {
                actions.Add(new()
                {
                    Level = "warning",
                    Text = $"{restructuredNotPaying:N0} of {m.RestructuredCount:N0} restructured accounts haven't paid in 30 days. The new plan isn't working for them.",
                });
            }

            if (m.Collections.Count > 0)
            {
                actions.Add(new()
                {
                    Level = "warning",
                    Text = $"{m.Collections.Count:N0} accounts need collection (KES {m.Collections.Sum(c => c.OverdueAmount):N0} overdue).",
                    Link = "#collections",
                    LinkText = "See list",
                });
            }

            if (m.WriteOffsPendingCount > 0)
            {
                actions.Add(new()
                {
                    Level = "info",
                    Text = $"{m.WriteOffsPendingCount:N0} write-offs waiting for review (KES {m.WriteOffsPendingBalance:N0}).",
                    Link = "/write-offs",
                    LinkText = "Review",
                });
            }

            if (m.DealerCommissionPayable > 0)
            {
                actions.Add(new()
                {
                    Level = "info",
                    Text = $"KES {m.DealerCommissionPayable:N0} dealer commission is payable now.",
                    Link = "/commission-payouts",
                    LinkText = "Pay commissions",
                });
            }

            return actions.OrderBy(a => a.SortOrder).ToList();
        }
    }
}
