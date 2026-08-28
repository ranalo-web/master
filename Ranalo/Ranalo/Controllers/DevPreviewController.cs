using Microsoft.AspNetCore.Mvc;
using Ranalo.Models;

namespace Ranalo.Controllers
{
    // Local-only helper for previewing dashboard layouts with mock data, bypassing
    // login and the database. Not wired to any real service/repository. Only
    // responds when running in Development, and should be removed before this
    // branch merges to main.
    public class DevPreviewController : Controller
    {
        private readonly IWebHostEnvironment _env;

        public DevPreviewController(IWebHostEnvironment env)
        {
            _env = env;
        }

        [HttpGet]
        [Route("dev-preview/admin-dashboard")]
        public IActionResult AdminDashboard()
        {
            if (!_env.IsDevelopment())
            {
                return NotFound();
            }

            ViewBag.BackLink = "index";
            ViewBag.IsAdmin = true;
            ViewBag.IsApprover = false;
            ViewBag.IsDealer = false;
            ViewBag.UserName = "Preview Admin";
            ViewData["IsAdmin"] = true;

            var model = new AwaitingApprovalViewModel
            {
                CurrentPage = 1,
                TotalPages = 3,
                SearchTerm = "",
                AwaitingApprovals = new List<AwaitingApprovalDto>
                {
                    new AwaitingApprovalDto { Id = 1, OrderId = 10231, Status = "approval-waiting", FirstName = "Jane", LastName = "Wanjiru", Email = "jane.wanjiru@example.com", Phone = "0712 345 678", DealerRef = "Nairobi Mobile Hub", MpesaDepositRef = "QGH7XJ2K", DateCreated = DateTime.Now.AddHours(-3), DaysUnpaid = 0 },
                    new AwaitingApprovalDto { Id = 2, OrderId = 10232, Status = "approved", FirstName = "Brian", LastName = "Otieno", Email = "brian.otieno@example.com", Phone = "0722 456 789", DealerRef = "Kisumu Electronics", MpesaDepositRef = "QGH8YK3L", DateCreated = DateTime.Now.AddHours(-6), DaysUnpaid = 0 },
                    new AwaitingApprovalDto { Id = 3, OrderId = 10233, Status = "cancelled", FirstName = "Amina", LastName = "Hassan", Email = "amina.hassan@example.com", Phone = "0733 567 890", DealerRef = "Mombasa Devices Ltd", MpesaDepositRef = "QGH9ZM4N", DateCreated = DateTime.Now.AddDays(-1), DaysUnpaid = 2 },
                    new AwaitingApprovalDto { Id = 4, OrderId = 10234, Status = "rejected", FirstName = "Peter", LastName = "Kamau", Email = "peter.kamau@example.com", Phone = "0744 678 901", DealerRef = "Nakuru Phone Shop", MpesaDepositRef = "QGH1AB5P", DateCreated = DateTime.Now.AddDays(-2), DaysUnpaid = 5 },
                    new AwaitingApprovalDto { Id = 5, OrderId = 10235, Status = "approval-waiting", FirstName = "Grace", LastName = "Njeri", Email = "grace.njeri@example.com", Phone = "0755 789 012", DealerRef = "Eldoret Tech", MpesaDepositRef = "QGH2CD6Q", DateCreated = DateTime.Now.AddHours(-1), DaysUnpaid = 0 },
                }
            };

            return View("~/Views/Home/Index.cshtml", model);
        }

        [HttpGet]
        [Route("dev-preview/admin-dashboard-live")]
        public IActionResult AdminDashboardLive()
        {
            if (!_env.IsDevelopment())
            {
                return NotFound();
            }

            ViewBag.BackLink = "admin-dashboard";
            ViewBag.IsAdmin = true;
            ViewBag.IsApprover = false;
            ViewBag.IsDealer = false;
            ViewBag.UserName = "Preview Admin";

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

            return View("~/Views/AdminDashboard/Index.cshtml", model);
        }
    }
}
