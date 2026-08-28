using Microsoft.AspNetCore.Mvc;
using Ranalo.Configuration;
using Ranalo.DataStore.DataModels;
using Ranalo.Models;

namespace Ranalo.Controllers
{
    [LoadUserSettingsFromCookie]
    public class DealerDashboardController : Controller
    {
        [HttpGet]
        [Route("dealer-dashboard")]
        public IActionResult Index()
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            if (settings.RoleId != UserRole.Dealer && settings.RoleId != UserRole.Admin)
            {
                return RedirectToAction("Index", "Home");
            }

            ViewBag.BackLink = "dealer-dashboard";
            ViewBag.IsAdmin = settings.RoleId == UserRole.Admin;
            ViewBag.IsApprover = false;
            ViewBag.IsDealer = settings.RoleId == UserRole.Dealer;
            ViewBag.UserName = settings.KnownAs;

            var model = DealerDashboardSampleData.Build();

            return View(model);
        }
    }

    // Sample data matching the agreed design mockup. Wiring to
    // IApplicationReportService / ICommissionsReportsService is a follow-up step.
    public static class DealerDashboardSampleData
    {
        public static DealerDashboardViewModel Build()
        {
            return new DealerDashboardViewModel
            {
                DealerName = "Nairobi Mobile Hub",

                RevenueThisMonth = 412300m,
                RevenueGrowthPct = 14.8m,
                AvgPerAccount = 2904m,

                TotalAccounts = 142,
                ActivePct = 91,
                NewThisMonth = 12,
                InDefault = 3,
                DefaultRatePct = 2.1m,

                CommissionReceived = 18600m,
                CommissionPaidToAgents = 12200m,
                CommissionOutstanding = 2000m,

                ActiveRateVsTargetPct = 114,

                GrowthMonths = new List<string> { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug" },
                RevenueByMonth = new List<decimal> { 258000, 264000, 271000, 296000, 322000, 348000, 371000, 412300 },

                PortfolioGoodPct = 82,
                PortfolioSlowPct = 10,
                PortfolioArrearsPct = 6,
                PortfolioNonPayingPct = 2,

                NonPayers = new List<DealerWatchlistEntry>
                {
                    new() { CustomerName = "Peter Wanjohi", AgentName = "Brian Kiplangat", Detail = "45 days" },
                    new() { CustomerName = "Alice Mumbi", AgentName = "Nancy Cherop", Detail = "38 days" },
                },
                SlowPayers = new List<DealerWatchlistEntry>
                {
                    new() { CustomerName = "Collins Mwas", AgentName = "Brian Kiplangat", Detail = "KES 4,800" },
                    new() { CustomerName = "Faith Achieng", AgentName = "Nancy Cherop", Detail = "KES 3,900" },
                    new() { CustomerName = "Esther Nyambura", AgentName = "Brian Kiplangat", Detail = "KES 2,200" },
                },
                GoodPayers = new List<DealerWatchlistEntry>
                {
                    new() { CustomerName = "James Odhiambo", AgentName = "Faith Wangari", Detail = "4 pmts ahead" },
                    new() { CustomerName = "Susan Wanjala", AgentName = "Faith Wangari", Detail = "3 pmts ahead" },
                    new() { CustomerName = "Michael Otieno", AgentName = "Brian Kiplangat", Detail = "2 pmts ahead" },
                    new() { CustomerName = "Daniel Kiprotich", AgentName = "Nancy Cherop", Detail = "2 pmts ahead" },
                },

                Contracts = new List<DealerContract>
                {
                    new() { CustomerName = "James Odhiambo", AgentName = "Faith Wangari", Device = "Samsung A15", MonthlyPayment = 3500, Status = "On Track", NextDue = "05 Sep 2026" },
                    new() { CustomerName = "Susan Wanjala", AgentName = "Faith Wangari", Device = "iPhone 12", MonthlyPayment = 5200, Status = "On Track", NextDue = "10 Sep 2026" },
                    new() { CustomerName = "Michael Otieno", AgentName = "Brian Kiplangat", Device = "Redmi Note 13", MonthlyPayment = 2900, Status = "On Track", NextDue = "12 Sep 2026" },
                    new() { CustomerName = "Esther Nyambura", AgentName = "Brian Kiplangat", Device = "Tecno Spark 20", MonthlyPayment = 2200, Status = "Late", NextDue = "01 Sep 2026" },
                    new() { CustomerName = "Daniel Kiprotich", AgentName = "Nancy Cherop", Device = "Samsung A15", MonthlyPayment = 3500, Status = "On Track", NextDue = "18 Sep 2026" },
                },

                AgentPerformance = new List<DealerAgentPerformance>
                {
                    new() { Rank = 1, AgentName = "Faith Wangari", Accounts = 58, ActivePct = 95, PctOfTarget = 119 },
                    new() { Rank = 2, AgentName = "Brian Kiplangat", Accounts = 49, ActivePct = 90, PctOfTarget = 113 },
                    new() { Rank = 3, AgentName = "Nancy Cherop", Accounts = 35, ActivePct = 83, PctOfTarget = 104 },
                },

                ContractsEndingSoon = new List<DealerContract>
                {
                    new() { CustomerName = "Grace Otieno", AgentName = "Faith Wangari", Device = "Samsung A15", NextDue = "03 Sep 2026", DaysLeft = "8 days" },
                    new() { CustomerName = "Kevin Mwangi", AgentName = "Nancy Cherop", Device = "iPhone 12", NextDue = "15 Sep 2026", DaysLeft = "20 days" },
                },

                CommissionsReceived = new List<DealerCommissionReceived>
                {
                    new() { Date = "15 Aug 2026", CustomerName = "Susan Wanjala", Amount = 3200, Status = "Paid" },
                    new() { Date = "10 Aug 2026", CustomerName = "Michael Otieno", Amount = 4500, Status = "Paid" },
                    new() { Date = "03 Aug 2026", CustomerName = "Esther Nyambura", Amount = 2800, Status = "Paid" },
                    new() { Date = "28 Jul 2026", CustomerName = "Daniel Kiprotich", Amount = 3900, Status = "Paid" },
                    new() { Date = "22 Jul 2026", CustomerName = "James Odhiambo", Amount = 4200, Status = "Paid" },
                },

                CommissionsPaid = new List<DealerCommissionPaid>
                {
                    new() { AgentName = "Faith Wangari", Accounts = 58, Due = 5800, Paid = 5800, Outstanding = 0, Status = "Settled" },
                    new() { AgentName = "Brian Kiplangat", Accounts = 49, Due = 4900, Paid = 3900, Outstanding = 1000, Status = "Outstanding" },
                    new() { AgentName = "Nancy Cherop", Accounts = 35, Due = 3500, Paid = 2500, Outstanding = 1000, Status = "Outstanding" },
                },

                DeviceStock = new List<DealerDeviceStock>
                {
                    new() { Device = "Samsung A15", Units = 52, AvgValue = 34000, GoodPct = 88, ArrearsPct = 12 },
                    new() { Device = "iPhone 12", Units = 18, AvgValue = 62000, GoodPct = 79, ArrearsPct = 21 },
                    new() { Device = "Redmi Note 13", Units = 34, AvgValue = 28000, GoodPct = 90, ArrearsPct = 10 },
                    new() { Device = "Tecno Spark 20", Units = 38, AvgValue = 19000, GoodPct = 93, ArrearsPct = 7 },
                },
            };
        }
    }
}
