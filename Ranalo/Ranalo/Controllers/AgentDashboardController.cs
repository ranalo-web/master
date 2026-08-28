using Microsoft.AspNetCore.Mvc;
using Ranalo.Configuration;
using Ranalo.DataStore.DataModels;
using Ranalo.Models;

namespace Ranalo.Controllers
{
    [LoadUserSettingsFromCookie]
    public class AgentDashboardController : Controller
    {
        [HttpGet]
        [Route("agent-dashboard")]
        public IActionResult Index()
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            if (settings.RoleId != UserRole.Agent && settings.RoleId != UserRole.Admin)
            {
                return RedirectToAction("Index", "Home");
            }

            ViewBag.BackLink = "agent-dashboard";
            ViewBag.IsAdmin = settings.RoleId == UserRole.Admin;
            ViewBag.IsApprover = false;
            ViewBag.IsDealer = false;
            ViewBag.IsAgent = settings.RoleId == UserRole.Agent;
            ViewBag.UserName = settings.KnownAs;

            var model = AgentDashboardSampleData.Build();

            return View(model);
        }
    }

    // Sample data matching the agreed design mockup. Wiring to real services is a follow-up step.
    public static class AgentDashboardSampleData
    {
        public static AgentDashboardViewModel Build()
        {
            return new AgentDashboardViewModel
            {
                AgentName = "Faith Wangari",
                DealerName = "Nairobi Mobile Hub",

                RevenueThisMonth = 168400m,
                RevenueGrowthPct = 16.2m,
                AvgPerAccount = 2903m,

                TotalAccounts = 58,
                ActivePct = 95,
                NewThisMonth = 5,
                InDefault = 2,
                DefaultRatePct = 3.4m,

                CommissionReceived = 5800m,
                ActiveRateVsTargetPct = 119,

                GrowthMonths = new List<string> { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug" },
                RevenueByMonth = new List<decimal> { 105000, 109000, 112000, 122000, 133000, 144000, 153000, 168400 },

                PortfolioGoodPct = 88,
                PortfolioSlowPct = 7,
                PortfolioArrearsPct = 4,
                PortfolioNonPayingPct = 1,

                NonPayers = new List<AgentWatchlistEntry>
                {
                    new() { CustomerName = "Dennis Kamau", Detail = "34 days" },
                    new() { CustomerName = "Ruth Chebet", Detail = "31 days" },
                },
                SlowPayers = new List<AgentWatchlistEntry>
                {
                    new() { CustomerName = "Beatrice Njeri", Detail = "KES 2,100" },
                    new() { CustomerName = "Samuel Kiptoo", Detail = "KES 3,400" },
                },
                GoodPayers = new List<AgentWatchlistEntry>
                {
                    new() { CustomerName = "James Odhiambo", Detail = "4 pmts ahead" },
                    new() { CustomerName = "Susan Wanjala", Detail = "3 pmts ahead" },
                    new() { CustomerName = "Grace Otieno", Detail = "2 pmts ahead" },
                    new() { CustomerName = "Peter Njoroge", Detail = "1 pmt ahead" },
                },

                Customers = new List<AgentContract>
                {
                    new() { CustomerName = "James Odhiambo", Device = "Samsung A15", MonthlyPayment = 3500, Status = "On Track", NextDue = "05 Sep 2026" },
                    new() { CustomerName = "Susan Wanjala", Device = "iPhone 12", MonthlyPayment = 5200, Status = "On Track", NextDue = "10 Sep 2026" },
                    new() { CustomerName = "Grace Otieno", Device = "Samsung A15", MonthlyPayment = 3200, Status = "On Track", NextDue = "01 Sep 2026" },
                    new() { CustomerName = "Peter Njoroge", Device = "Redmi Note 13", MonthlyPayment = 2600, Status = "On Track", NextDue = "14 Sep 2026" },
                    new() { CustomerName = "Beatrice Njeri", Device = "Tecno Spark 20", MonthlyPayment = 2100, Status = "Late", NextDue = "02 Sep 2026" },
                    new() { CustomerName = "Dennis Kamau", Device = "Samsung A15", MonthlyPayment = 3000, Status = "Default", NextDue = "Overdue" },
                },

                ContractsEndingSoon = new List<AgentContract>
                {
                    new() { CustomerName = "Grace Otieno", Device = "Samsung A15", NextDue = "03 Sep 2026", DaysLeft = "8 days" },
                    new() { CustomerName = "Peter Njoroge", Device = "Redmi Note 13", NextDue = "20 Sep 2026", DaysLeft = "25 days" },
                },

                Commissions = new List<AgentCommission>
                {
                    new() { Date = "14 Aug 2026", CustomerName = "James Odhiambo", Amount = 1300, Status = "Paid" },
                    new() { Date = "09 Aug 2026", CustomerName = "Susan Wanjala", Amount = 1200, Status = "Paid" },
                    new() { Date = "02 Aug 2026", CustomerName = "Grace Otieno", Amount = 1100, Status = "Paid" },
                    new() { Date = "27 Jul 2026", CustomerName = "Peter Njoroge", Amount = 1000, Status = "Paid" },
                    new() { Date = "21 Jul 2026", CustomerName = "Beatrice Njeri", Amount = 1200, Status = "Paid" },
                },

                DeviceStock = new List<AgentDeviceStock>
                {
                    new() { Device = "Samsung A15", Units = 24, AvgValue = 34000, GoodPct = 90, ArrearsPct = 10 },
                    new() { Device = "iPhone 12", Units = 8, AvgValue = 62000, GoodPct = 82, ArrearsPct = 18 },
                    new() { Device = "Redmi Note 13", Units = 16, AvgValue = 28000, GoodPct = 92, ArrearsPct = 8 },
                    new() { Device = "Tecno Spark 20", Units = 10, AvgValue = 19000, GoodPct = 95, ArrearsPct = 5 },
                },
            };
        }
    }
}
