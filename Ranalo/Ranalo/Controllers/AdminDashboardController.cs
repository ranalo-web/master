using Microsoft.AspNetCore.Mvc;
using Ranalo.Configuration;
using Ranalo.DataStore.DataModels;
using Ranalo.Models;

namespace Ranalo.Controllers
{
    [LoadUserSettingsFromCookie]
    public class AdminDashboardController : Controller
    {
        [HttpGet]
        [Route("admin-dashboard")]
        public IActionResult Index()
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            if (settings.RoleId != UserRole.Admin)
            {
                return RedirectToAction("Index", "Home");
            }

            ViewBag.BackLink = "admin-dashboard";
            ViewBag.IsAdmin = true;
            ViewBag.IsApprover = false;
            ViewBag.IsDealer = false;
            ViewBag.UserName = settings.KnownAs;

            // Sample data matching the agreed design mockup. Wiring to
            // IApplicationReportService and friends is a follow-up step.
            var model = new AdminDashboardViewModel
            {
                RevenueThisMonth = 412300m,
                RevenueGrowthPct = 14.8m,

                TotalAccounts = 1842,
                GoodAccounts = 1583,
                BadAccounts = 259,

                PayingAccounts = 1691,
                NonPayingAccounts = 151,
                ArrearsTotal = 618400m,

                GrowthMonths = new List<string> { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug" },
                RevenueByMonth = new List<decimal> { 298000, 312000, 305000, 334000, 356000, 372000, 391000, 412300 },
                AccountsByMonth = new List<int> { 1520, 1568, 1601, 1640, 1685, 1722, 1780, 1842 },

                PortfolioGoodPct = 82,
                PortfolioSlowPct = 10,
                PortfolioArrearsPct = 6,
                PortfolioNonPayingPct = 2,

                NonPayers = new List<AdminWatchlistEntry>
                {
                    new() { CustomerName = "Peter Wanjohi", DealerName = "Nairobi Mobile Hub", Detail = "45 days" },
                    new() { CustomerName = "Alice Mumbi", DealerName = "Nairobi Mobile Hub", Detail = "38 days" },
                },
                SlowPayers = new List<AdminWatchlistEntry>
                {
                    new() { CustomerName = "Collins Mwas", DealerName = "Nairobi Mobile Hub", Detail = "KES 4,800" },
                    new() { CustomerName = "Faith Achieng", DealerName = "Nairobi Mobile Hub", Detail = "KES 3,900" },
                    new() { CustomerName = "Esther Nyambura", DealerName = "Nairobi Mobile Hub", Detail = "KES 2,200" },
                },
                GoodPayers = new List<AdminWatchlistEntry>
                {
                    new() { CustomerName = "James Odhiambo", DealerName = "Nairobi Mobile Hub", Detail = "4 pmts ahead" },
                    new() { CustomerName = "Susan Wanjala", DealerName = "Nairobi Mobile Hub", Detail = "3 pmts ahead" },
                },

                DealerPerformance = new List<AdminDealerPerformance>
                {
                    new() { Rank = 1, DealerName = "Nairobi Mobile Hub", Accounts = 142, ActivePct = 91, Revenue = 412300, CommissionPaid = 18600, CommissionDue = 18600, PctOfTarget = 114 },
                    new() { Rank = 2, DealerName = "Kisumu Electronics", Accounts = 98, ActivePct = 87, Revenue = 289500, CommissionPaid = 12100, CommissionDue = 12100, PctOfTarget = 109 },
                    new() { Rank = 3, DealerName = "Mombasa Devices Ltd", Accounts = 76, ActivePct = 79, Revenue = 214800, CommissionPaid = 8900, CommissionDue = 9200, PctOfTarget = 99 },
                },

                AgentPerformance = new List<AdminAgentPerformance>
                {
                    new() { Rank = 1, AgentName = "Faith Wangari", DealerName = "Nairobi Mobile Hub", Accounts = 58, ActivePct = 95, PctOfTarget = 119 },
                    new() { Rank = 2, AgentName = "Brian Kiplangat", DealerName = "Nairobi Mobile Hub", Accounts = 49, ActivePct = 90, PctOfTarget = 113 },
                    new() { Rank = 3, AgentName = "Nancy Cherop", DealerName = "Nairobi Mobile Hub", Accounts = 35, ActivePct = 83, PctOfTarget = 104 },
                },
            };

            return View(model);
        }
    }
}
