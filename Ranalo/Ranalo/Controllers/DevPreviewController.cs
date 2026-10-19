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
        [Route("dev-preview/dealer-dashboard")]
        public IActionResult DealerDashboard()
        {
            if (!_env.IsDevelopment())
            {
                return NotFound();
            }

            ViewBag.BackLink = "dealer-dashboard";
            ViewBag.IsAdmin = false;
            ViewBag.IsApprover = false;
            ViewBag.IsDealer = true;
            ViewBag.UserName = "Preview Dealer";

            var model = DealerDashboardSampleData.Build();

            return View("~/Views/DealerDashboard/Index.cshtml", model);
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
                RevenueTargetThisMonth = 490000m,
                TotalAccounts = 1842,
                GoodAccounts = 1583,
                BadAccounts = 259,
                PayingAccounts = 1691,
                NonPayingAccounts = 151,
                NonPayingAccountsChange = -18,
                ArrearsTotal = 618400m,
                ArrearsChangePct = 6.5m,
                GrowthMonths = new List<string> { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug" },
                RevenueByMonth = new List<decimal> { 298000, 312000, 305000, 334000, 356000, 372000, 391000, 412300 },
                AccountsByMonth = new List<int> { 1520, 1568, 1601, 1640, 1685, 1722, 1780, 1842 },
                PortfolioGoodPct = 82,
                PortfolioSlowPct = 10,
                PortfolioArrearsPct = 6,
                PortfolioNonPayingPct = 2,

                CollectionRatePct = 91.4m,
                PortfolioAtRiskPct = 8.2m,

                PortfolioGoodPctChange = 1.5m,
                CollectionRateChangePct = 2.3m,
                PortfolioAtRiskChangePct = -0.9m,

                CostOfDevicesThisMonth = 38000m,
                BadDebtThisMonth = 28500m,
                NetProfitChangePct = 9.2m,
                ProfitMarginChangePct = 1.8m,
                ProfitMarginTargetPct = 65m,
                CommissionsChangePct = 6.1m,
                BadDebtChangePct = -12.4m,

                OperatingExpensesThisMonth = 45000m,
                TaxRatePct = 30m,
                DividendsPaidThisMonth = 60000m,

                TotalCustomers = 1798,
                NewCustomersThisMonth = 187,
                RepeatCustomerRatePct = 23.4m,
                AvgCustomerLifetimeValue = 34200m,
                ChurnRatePct = 3.2m,

                CompletedContractsThisMonth = 156,
                CompletedContractsChangePct = 12.6m,
                ContractCompletionRatePct = 84.7m,
                ContractCompletionRateChangePct = 1.2m,
                AvgTimeToCompletionMonths = 7.8m,
                TotalValueCompletedThisMonth = 2340000m,
                NonPayers = new List<AdminWatchlistEntry>
                {
                    new() { CustomerName = "Peter Wanjohi", DealerName = "Nairobi Mobile Hub", Phone = "0712 345 678", Detail = "60 days" },
                    new() { CustomerName = "Alice Mumbi", DealerName = "Nairobi Mobile Hub", Phone = "0722 456 789", Detail = "56 days" },
                    new() { CustomerName = "Dennis Kiptoo", DealerName = "Kisumu Electronics", Phone = "0733 567 890", Detail = "52 days" },
                    new() { CustomerName = "Mercy Adhiambo", DealerName = "Mombasa Devices Ltd", Phone = "0744 678 901", Detail = "49 days" },
                    new() { CustomerName = "Samuel Kariuki", DealerName = "Nairobi Mobile Hub", Phone = "0755 789 012", Detail = "47 days" },
                    new() { CustomerName = "Josephine Wafula", DealerName = "Kisumu Electronics", Phone = "0766 890 123", Detail = "45 days" },
                    new() { CustomerName = "Daniel Mwangi", DealerName = "Nakuru Phone Shop", Phone = "0779 901 234", Detail = "43 days" },
                    new() { CustomerName = "Lucy Chepkoech", DealerName = "Eldoret Tech", Phone = "0701 012 345", Detail = "41 days" },
                    new() { CustomerName = "Anthony Njoroge", DealerName = "Nairobi Mobile Hub", Phone = "0712 123 456", Detail = "39 days" },
                    new() { CustomerName = "Beatrice Auma", DealerName = "Mombasa Devices Ltd", Phone = "0722 234 567", Detail = "37 days" },
                    new() { CustomerName = "Charles Kimutai", DealerName = "Kisumu Electronics", Phone = "0733 345 678", Detail = "35 days" },
                    new() { CustomerName = "Winnie Akinyi", DealerName = "Nairobi Mobile Hub", Phone = "0744 456 789", Detail = "34 days" },
                    new() { CustomerName = "George Barasa", DealerName = "Nakuru Phone Shop", Phone = "0755 567 890", Detail = "33 days" },
                    new() { CustomerName = "Purity Wangui", DealerName = "Eldoret Tech", Phone = "0766 678 901", Detail = "32 days" },
                    new() { CustomerName = "Moses Langat", DealerName = "Kisumu Electronics", Phone = "0779 789 012", Detail = "31 days" },
                    new() { CustomerName = "Sarah Nyaguthii", DealerName = "Nairobi Mobile Hub", Phone = "0701 890 123", Detail = "30 days" },
                    new() { CustomerName = "Felix Omwenga", DealerName = "Mombasa Devices Ltd", Phone = "0712 901 234", Detail = "29 days" },
                    new() { CustomerName = "Diana Chelangat", DealerName = "Nakuru Phone Shop", Phone = "0722 012 345", Detail = "28 days" },
                    new() { CustomerName = "Patrick Muriuki", DealerName = "Eldoret Tech", Phone = "0733 123 456", Detail = "27 days" },
                    new() { CustomerName = "Agnes Wambui", DealerName = "Nairobi Mobile Hub", Phone = "0744 234 567", Detail = "26 days" },
                },
                SlowPayers = new List<AdminWatchlistEntry>
                {
                    new() { CustomerName = "Collins Mwas", DealerName = "Nairobi Mobile Hub", Phone = "0712 111 222", Detail = "KES 4,800" },
                    new() { CustomerName = "Faith Achieng", DealerName = "Nairobi Mobile Hub", Phone = "0722 222 333", Detail = "KES 3,900" },
                    new() { CustomerName = "Esther Nyambura", DealerName = "Nairobi Mobile Hub", Phone = "0733 333 444", Detail = "KES 3,600" },
                    new() { CustomerName = "Kevin Otieno", DealerName = "Kisumu Electronics", Phone = "0744 444 555", Detail = "KES 3,300" },
                    new() { CustomerName = "Ruth Chebet", DealerName = "Mombasa Devices Ltd", Phone = "0755 555 666", Detail = "KES 3,000" },
                    new() { CustomerName = "Victor Mutua", DealerName = "Nairobi Mobile Hub", Phone = "0766 666 777", Detail = "KES 2,800" },
                    new() { CustomerName = "Irene Nafula", DealerName = "Nakuru Phone Shop", Phone = "0779 777 888", Detail = "KES 2,600" },
                    new() { CustomerName = "Stephen Kiprotich", DealerName = "Eldoret Tech", Phone = "0701 888 999", Detail = "KES 2,400" },
                    new() { CustomerName = "Caroline Njeri", DealerName = "Kisumu Electronics", Phone = "0712 999 000", Detail = "KES 2,200" },
                    new() { CustomerName = "Emmanuel Odera", DealerName = "Nairobi Mobile Hub", Phone = "0722 100 200", Detail = "KES 2,100" },
                    new() { CustomerName = "Linet Moraa", DealerName = "Mombasa Devices Ltd", Phone = "0733 200 300", Detail = "KES 2,000" },
                    new() { CustomerName = "Bernard Kiplagat", DealerName = "Nakuru Phone Shop", Phone = "0744 300 400", Detail = "KES 1,900" },
                    new() { CustomerName = "Joyce Wanjiku", DealerName = "Eldoret Tech", Phone = "0755 400 500", Detail = "KES 1,800" },
                    new() { CustomerName = "Hillary Simiyu", DealerName = "Kisumu Electronics", Phone = "0766 500 600", Detail = "KES 1,700" },
                    new() { CustomerName = "Rose Atieno", DealerName = "Nairobi Mobile Hub", Phone = "0779 600 700", Detail = "KES 1,600" },
                    new() { CustomerName = "Peterson Maina", DealerName = "Mombasa Devices Ltd", Phone = "0701 700 800", Detail = "KES 1,500" },
                    new() { CustomerName = "Consolata Mueni", DealerName = "Nakuru Phone Shop", Phone = "0712 800 900", Detail = "KES 1,400" },
                    new() { CustomerName = "Erick Ochieng", DealerName = "Eldoret Tech", Phone = "0722 900 001", Detail = "KES 1,300" },
                    new() { CustomerName = "Naomi Jepkosgei", DealerName = "Kisumu Electronics", Phone = "0733 001 102", Detail = "KES 1,250" },
                    new() { CustomerName = "David Kamau", DealerName = "Nairobi Mobile Hub", Phone = "0744 102 203", Detail = "KES 1,200" },
                },
                GoodPayers = new List<AdminWatchlistEntry>
                {
                    new() { CustomerName = "James Odhiambo", DealerName = "Nairobi Mobile Hub", Phone = "0712 987 654", Detail = "6 pmts ahead" },
                    new() { CustomerName = "Susan Wanjala", DealerName = "Nairobi Mobile Hub", Phone = "0722 876 543", Detail = "5 pmts ahead" },
                    new() { CustomerName = "Brian Kiplangat", DealerName = "Kisumu Electronics", Phone = "0733 765 432", Detail = "5 pmts ahead" },
                    new() { CustomerName = "Nancy Cherop", DealerName = "Mombasa Devices Ltd", Phone = "0744 654 321", Detail = "4 pmts ahead" },
                    new() { CustomerName = "Michael Omondi", DealerName = "Nairobi Mobile Hub", Phone = "0755 543 210", Detail = "4 pmts ahead" },
                    new() { CustomerName = "Grace Nekesa", DealerName = "Kisumu Electronics", Phone = "0766 432 109", Detail = "4 pmts ahead" },
                    new() { CustomerName = "Edwin Karanja", DealerName = "Nakuru Phone Shop", Phone = "0779 321 098", Detail = "3 pmts ahead" },
                    new() { CustomerName = "Mary Chepngeno", DealerName = "Eldoret Tech", Phone = "0701 210 987", Detail = "3 pmts ahead" },
                    new() { CustomerName = "Vincent Ouma", DealerName = "Nairobi Mobile Hub", Phone = "0712 109 876", Detail = "3 pmts ahead" },
                    new() { CustomerName = "Cynthia Wairimu", DealerName = "Mombasa Devices Ltd", Phone = "0722 098 765", Detail = "3 pmts ahead" },
                    new() { CustomerName = "Robert Kiplimo", DealerName = "Kisumu Electronics", Phone = "0733 987 654", Detail = "2 pmts ahead" },
                    new() { CustomerName = "Ann Nyokabi", DealerName = "Nairobi Mobile Hub", Phone = "0744 876 543", Detail = "2 pmts ahead" },
                    new() { CustomerName = "Duncan Wekesa", DealerName = "Nakuru Phone Shop", Phone = "0755 765 432", Detail = "2 pmts ahead" },
                    new() { CustomerName = "Lilian Chepchumba", DealerName = "Eldoret Tech", Phone = "0766 654 321", Detail = "2 pmts ahead" },
                    new() { CustomerName = "Isaac Mburu", DealerName = "Nairobi Mobile Hub", Phone = "0779 543 210", Detail = "2 pmts ahead" },
                    new() { CustomerName = "Sharon Achola", DealerName = "Kisumu Electronics", Phone = "0701 432 109", Detail = "1 pmt ahead" },
                    new() { CustomerName = "Kenneth Rotich", DealerName = "Mombasa Devices Ltd", Phone = "0712 321 098", Detail = "1 pmt ahead" },
                    new() { CustomerName = "Judith Nyambura", DealerName = "Nakuru Phone Shop", Phone = "0722 210 987", Detail = "1 pmt ahead" },
                    new() { CustomerName = "Timothy Gitau", DealerName = "Eldoret Tech", Phone = "0733 109 876", Detail = "1 pmt ahead" },
                    new() { CustomerName = "Mildred Auma", DealerName = "Nairobi Mobile Hub", Phone = "0744 098 765", Detail = "1 pmt ahead" },
                },
                DealerPerformance = new List<AdminDealerPerformance>
                {
                    new() { Rank = 1, DealerName = "Nairobi Mobile Hub", Accounts = 142, ActivePct = 91, Revenue = 412300, CommissionPaid = 18600, CommissionDue = 18600, PctOfTarget = 114 },
                    new() { Rank = 2, DealerName = "Kisumu Electronics", Accounts = 98, ActivePct = 87, Revenue = 289500, CommissionPaid = 12100, CommissionDue = 12100, PctOfTarget = 109 },
                    new() { Rank = 3, DealerName = "Mombasa Devices Ltd", Accounts = 76, ActivePct = 79, Revenue = 214800, CommissionPaid = 8900, CommissionDue = 9200, PctOfTarget = 99 },
                    new() { Rank = 4, DealerName = "Nakuru Phone Shop", Accounts = 64, ActivePct = 83, Revenue = 187600, CommissionPaid = 7800, CommissionDue = 7800, PctOfTarget = 104 },
                    new() { Rank = 5, DealerName = "Eldoret Tech", Accounts = 58, ActivePct = 76, Revenue = 165200, CommissionPaid = 6900, CommissionDue = 7100, PctOfTarget = 95 },
                    new() { Rank = 6, DealerName = "Thika Gadget World", Accounts = 51, ActivePct = 88, Revenue = 148900, CommissionPaid = 6200, CommissionDue = 6200, PctOfTarget = 110 },
                    new() { Rank = 7, DealerName = "Machakos Mobile Center", Accounts = 47, ActivePct = 74, Revenue = 132400, CommissionPaid = 5500, CommissionDue = 5700, PctOfTarget = 93 },
                    new() { Rank = 8, DealerName = "Kericho Phone Palace", Accounts = 43, ActivePct = 81, Revenue = 121800, CommissionPaid = 5100, CommissionDue = 5100, PctOfTarget = 101 },
                    new() { Rank = 9, DealerName = "Nyeri Digital Hub", Accounts = 39, ActivePct = 77, Revenue = 109600, CommissionPaid = 4600, CommissionDue = 4750, PctOfTarget = 96 },
                    new() { Rank = 10, DealerName = "Kitale Communications", Accounts = 35, ActivePct = 85, Revenue = 98700, CommissionPaid = 4100, CommissionDue = 4100, PctOfTarget = 106 },
                    new() { Rank = 11, DealerName = "Malindi Mobile Zone", Accounts = 32, ActivePct = 72, Revenue = 87300, CommissionPaid = 3600, CommissionDue = 3800, PctOfTarget = 90 },
                    new() { Rank = 12, DealerName = "Kakamega Phone Store", Accounts = 29, ActivePct = 79, Revenue = 79200, CommissionPaid = 3300, CommissionDue = 3300, PctOfTarget = 99 },
                    new() { Rank = 13, DealerName = "Garissa Tech Point", Accounts = 24, ActivePct = 68, Revenue = 63400, CommissionPaid = 2600, CommissionDue = 2900, PctOfTarget = 85 },
                    new() { Rank = 14, DealerName = "Meru Mobile World", Accounts = 21, ActivePct = 82, Revenue = 58100, CommissionPaid = 2400, CommissionDue = 2400, PctOfTarget = 103 },
                    new() { Rank = 15, DealerName = "Eldama Ravine Communications", Accounts = 17, ActivePct = 70, Revenue = 45200, CommissionPaid = 1900, CommissionDue = 2100, PctOfTarget = 88 },
                },
                AgentPerformance = new List<AdminAgentPerformance>
                {
                    new() { Rank = 1, AgentName = "Faith Wangari", DealerName = "Nairobi Mobile Hub", Accounts = 58, ActivePct = 95, PctOfTarget = 119 },
                    new() { Rank = 2, AgentName = "Brian Kiplangat", DealerName = "Nairobi Mobile Hub", Accounts = 49, ActivePct = 90, PctOfTarget = 113 },
                    new() { Rank = 3, AgentName = "Nancy Cherop", DealerName = "Nairobi Mobile Hub", Accounts = 35, ActivePct = 83, PctOfTarget = 104 },
                    new() { Rank = 4, AgentName = "Peter Mwaura", DealerName = "Kisumu Electronics", Accounts = 44, ActivePct = 92, PctOfTarget = 115 },
                    new() { Rank = 5, AgentName = "Lucy Adhiambo", DealerName = "Kisumu Electronics", Accounts = 38, ActivePct = 86, PctOfTarget = 108 },
                    new() { Rank = 6, AgentName = "John Kiptum", DealerName = "Mombasa Devices Ltd", Accounts = 33, ActivePct = 79, PctOfTarget = 99 },
                    new() { Rank = 7, AgentName = "Mary Wanjiru", DealerName = "Mombasa Devices Ltd", Accounts = 29, ActivePct = 74, PctOfTarget = 93 },
                    new() { Rank = 8, AgentName = "Samuel Otieno", DealerName = "Nakuru Phone Shop", Accounts = 41, ActivePct = 88, PctOfTarget = 110 },
                    new() { Rank = 9, AgentName = "Esther Muthoni", DealerName = "Nakuru Phone Shop", Accounts = 27, ActivePct = 81, PctOfTarget = 101 },
                    new() { Rank = 10, AgentName = "Kevin Kiprono", DealerName = "Eldoret Tech", Accounts = 36, ActivePct = 77, PctOfTarget = 96 },
                    new() { Rank = 11, AgentName = "Diana Achieng", DealerName = "Eldoret Tech", Accounts = 24, ActivePct = 70, PctOfTarget = 88 },
                    new() { Rank = 12, AgentName = "Robert Njuguna", DealerName = "Thika Gadget World", Accounts = 31, ActivePct = 85, PctOfTarget = 106 },
                    new() { Rank = 13, AgentName = "Winnie Cheruto", DealerName = "Machakos Mobile Center", Accounts = 22, ActivePct = 73, PctOfTarget = 91 },
                    new() { Rank = 14, AgentName = "Dennis Wafula", DealerName = "Kericho Phone Palace", Accounts = 19, ActivePct = 80, PctOfTarget = 100 },
                    new() { Rank = 15, AgentName = "Grace Auma", DealerName = "Nyeri Digital Hub", Accounts = 17, ActivePct = 68, PctOfTarget = 85 },
                },

                ProductPerformance = new List<AdminProductPerformance>
                {
                    new() { Rank = 1, ProductName = "Samsung Galaxy A14", UnitsFinanced = 312, AvgValue = 18500, Revenue = 187400, DefaultRatePct = 4.2m },
                    new() { Rank = 2, ProductName = "Tecno Spark 10", UnitsFinanced = 268, AvgValue = 14200, Revenue = 142600, DefaultRatePct = 5.1m },
                    new() { Rank = 3, ProductName = "Infinix Hot 30", UnitsFinanced = 201, AvgValue = 13800, Revenue = 108300, DefaultRatePct = 6.3m },
                    new() { Rank = 4, ProductName = "iPhone 12", UnitsFinanced = 89, AvgValue = 42000, Revenue = 96800, DefaultRatePct = 2.1m },
                    new() { Rank = 5, ProductName = "Samsung Galaxy A54", UnitsFinanced = 156, AvgValue = 24600, Revenue = 89400, DefaultRatePct = 3.8m },
                    new() { Rank = 6, ProductName = "Xiaomi Redmi Note 12", UnitsFinanced = 134, AvgValue = 16900, Revenue = 71200, DefaultRatePct = 5.7m },
                    new() { Rank = 7, ProductName = "Oppo A78", UnitsFinanced = 98, AvgValue = 19300, Revenue = 52100, DefaultRatePct = 4.9m },
                    new() { Rank = 8, ProductName = "Tecno Camon 20", UnitsFinanced = 87, AvgValue = 17400, Revenue = 44800, DefaultRatePct = 6.8m },
                    new() { Rank = 9, ProductName = "iPhone 13", UnitsFinanced = 42, AvgValue = 58000, Revenue = 68900, DefaultRatePct = 1.8m },
                    new() { Rank = 10, ProductName = "Infinix Note 30", UnitsFinanced = 65, AvgValue = 15600, Revenue = 33400, DefaultRatePct = 5.9m },
                    new() { Rank = 11, ProductName = "Samsung Galaxy A05", UnitsFinanced = 76, AvgValue = 11200, Revenue = 31200, DefaultRatePct = 7.4m },
                    new() { Rank = 12, ProductName = "Vivo Y36", UnitsFinanced = 54, AvgValue = 16800, Revenue = 27900, DefaultRatePct = 6.1m },
                },

                CompletedContracts = new List<AdminCompletedContract>
                {
                    new() { CustomerName = "Peterson Kamau", DealerName = "Nairobi Mobile Hub", ProductName = "Samsung Galaxy A14", CompletedDate = "Aug 28", TotalPaid = 19200, DurationMonths = 8 },
                    new() { CustomerName = "Lydia Wanjiku", DealerName = "Kisumu Electronics", ProductName = "Tecno Spark 10", CompletedDate = "Aug 26", TotalPaid = 14800, DurationMonths = 6 },
                    new() { CustomerName = "Moses Otieno", DealerName = "Mombasa Devices Ltd", ProductName = "iPhone 12", CompletedDate = "Aug 25", TotalPaid = 43500, DurationMonths = 12 },
                    new() { CustomerName = "Catherine Njoki", DealerName = "Nakuru Phone Shop", ProductName = "Infinix Hot 30", CompletedDate = "Aug 24", TotalPaid = 14100, DurationMonths = 7 },
                    new() { CustomerName = "Julius Mutiso", DealerName = "Eldoret Tech", ProductName = "Samsung Galaxy A54", CompletedDate = "Aug 22", TotalPaid = 25300, DurationMonths = 9 },
                    new() { CustomerName = "Faith Nekesa", DealerName = "Thika Gadget World", ProductName = "Xiaomi Redmi Note 12", CompletedDate = "Aug 21", TotalPaid = 17400, DurationMonths = 8 },
                    new() { CustomerName = "Dennis Ochieng", DealerName = "Machakos Mobile Center", ProductName = "Oppo A78", CompletedDate = "Aug 19", TotalPaid = 19900, DurationMonths = 6 },
                    new() { CustomerName = "Grace Wambui", DealerName = "Kericho Phone Palace", ProductName = "Tecno Camon 20", CompletedDate = "Aug 18", TotalPaid = 17900, DurationMonths = 7 },
                    new() { CustomerName = "Samuel Njogu", DealerName = "Nyeri Digital Hub", ProductName = "iPhone 13", CompletedDate = "Aug 17", TotalPaid = 59200, DurationMonths = 12 },
                    new() { CustomerName = "Esther Kiplagat", DealerName = "Kitale Communications", ProductName = "Infinix Note 30", CompletedDate = "Aug 15", TotalPaid = 16000, DurationMonths = 8 },
                    new() { CustomerName = "Bernard Owino", DealerName = "Malindi Mobile Zone", ProductName = "Samsung Galaxy A05", CompletedDate = "Aug 14", TotalPaid = 11600, DurationMonths = 6 },
                    new() { CustomerName = "Winnie Achieng", DealerName = "Kakamega Phone Store", ProductName = "Vivo Y36", CompletedDate = "Aug 12", TotalPaid = 17300, DurationMonths = 7 },
                    new() { CustomerName = "Robert Kamotho", DealerName = "Garissa Tech Point", ProductName = "Samsung Galaxy A14", CompletedDate = "Aug 10", TotalPaid = 19000, DurationMonths = 8 },
                    new() { CustomerName = "Purity Chebet", DealerName = "Meru Mobile World", ProductName = "Tecno Spark 10", CompletedDate = "Aug 9", TotalPaid = 14600, DurationMonths = 6 },
                    new() { CustomerName = "Charles Wekesa", DealerName = "Eldama Ravine Communications", ProductName = "Infinix Hot 30", CompletedDate = "Aug 7", TotalPaid = 13900, DurationMonths = 7 },
                    new() { CustomerName = "Alice Muthee", DealerName = "Nairobi Mobile Hub", ProductName = "iPhone 12", CompletedDate = "Aug 5", TotalPaid = 44000, DurationMonths = 12 },
                    new() { CustomerName = "Kevin Simiyu", DealerName = "Kisumu Electronics", ProductName = "Samsung Galaxy A54", CompletedDate = "Aug 3", TotalPaid = 25800, DurationMonths = 9 },
                    new() { CustomerName = "Nancy Auma", DealerName = "Mombasa Devices Ltd", ProductName = "Xiaomi Redmi Note 12", CompletedDate = "Aug 2", TotalPaid = 17200, DurationMonths = 8 },
                    new() { CustomerName = "Timothy Karanja", DealerName = "Nakuru Phone Shop", ProductName = "Oppo A78", CompletedDate = "Jul 31", TotalPaid = 20100, DurationMonths = 6 },
                    new() { CustomerName = "Ruth Nyambura", DealerName = "Eldoret Tech", ProductName = "Tecno Camon 20", CompletedDate = "Jul 29", TotalPaid = 18100, DurationMonths = 7 },
                },
            };

            return View("~/Views/AdminDashboard/Index.cshtml", model);
        }

        // The real, live figures behind the Admin / Dealer / Agent dashboards
        // as JSON, for checking them against the database without logging in.
        // Development only, like everything in this controller.
        [HttpGet]
        [Route("dev-preview/dashboard-figures")]
        public async Task<IActionResult> DashboardFigures([FromServices] Services.IDashboardReportService dashboards,
            string view = "admin", int dealerId = 0, int? agentUserId = null)
        {
            if (!_env.IsDevelopment())
            {
                return NotFound();
            }

            object model = view switch
            {
                "dealer" => await dashboards.GetDealerDashboardAsync(dealerId),
                "agent" => await dashboards.GetDealerDashboardAsync(dealerId, agentUserId),
                _ => await dashboards.GetAdminDashboardAsync(),
            };

            // ?html=true renders the real dashboard page with this data.
            if (Request.Query["html"] == "true")
            {
                ViewBag.IsAdmin = view == "admin";
                ViewBag.IsDealer = view == "dealer";
                ViewBag.IsAgent = view == "agent";
                ViewBag.IsApprover = false;
                ViewBag.UserName = "Preview";
                ViewBag.BackLink = view == "admin" ? "admin-dashboard" : view == "agent" ? "agent" : "dealer-dashboard";
                return View(view == "admin" ? "~/Views/AdminDashboard/Index.cshtml" : "~/Views/DealerDashboard/Index.cshtml", model);
            }
            return Json(model);
        }

        // Dealer Allocation with real data, read-only (the Move form posts to
        // the real, admin-only endpoint, which needs a login).
        [HttpGet]
        [Route("dev-preview/dealer-allocation")]
        public async Task<IActionResult> DealerAllocation([FromServices] DataStore.IDealerAllocationRepository repository, string? show = null, string? q = null)
        {
            if (!_env.IsDevelopment())
            {
                return NotFound();
            }

            var unassignedOnly = show != "all";
            var (unassigned, noDevice) = await repository.GetUnassignedCountsAsync();
            var model = new DealerAllocationViewModel
            {
                Show = unassignedOnly ? "unassigned" : "all",
                Search = q,
                UnassignedCount = unassigned,
                NoDeviceCount = noDevice,
                Accounts = await repository.GetAccountsAsync(unassignedOnly, q),
                Dealers = await repository.GetDealersAsync(),
            };
            try
            {
                model.RecentChanges = await repository.GetRecentChangesAsync(20);
            }
            catch
            {
                model.LogUnavailable = true;
            }

            ViewBag.IsAdmin = true;
            ViewBag.IsApprover = false;
            ViewBag.IsDealer = false;
            ViewBag.UserName = "Preview Admin";
            ViewBag.BackLink = "dealer-allocation";
            return View("~/Views/DealerAllocation/Index.cshtml", model);
        }

        // Account Commissions with sample accounts run through the real
        // CommissionCalculator. ?agent=1 shows the agent's view; ?expand=1
        // renders the first rows' detail panels open (for screenshots).
        // Lock check page with sample data (no database).
        [HttpGet]
        [Route("dev-preview/lock-check")]
        public IActionResult LockCheck(LockCheckStage stage = LockCheckStage.NeedsNewLocker)
        {
            if (!_env.IsDevelopment())
            {
                return NotFound();
            }

            ViewBag.IsAdmin = true;
            ViewBag.UserName = "Preview Admin";
            var today = DateTime.UtcNow.Date;
            LockCheckRow Row(long acc, string name, string make, string model, decimal paid, int startDaysAgo, string status = "enrolled",
                bool locked = false, int? recovery = null, string? action = null) => new()
            {
                AccountNo = acc, ContractId = (int)(acc % 1000), CustomerName = name, Make = make, Model = model, NuovoStatus = status, Locked = locked,
                Deposit = 2000, Daily = 100, TermInMonths = 12, StartDate = today.AddDays(-startDaysAgo), Paid = paid,
                LastPaid = today.AddDays(-20), LastConnectedAt = "02-09-26 10:11:12 UTC", DealerName = "Nicholas Lunch (Lucky)",
                RecoveryId = recovery, RecoveryStatus = recovery.HasValue ? RecoveryStatus.UploadSent : null,
                LastAction = action, LastActionSuccess = action != null ? true : null, LastActionAt = action != null ? DateTime.UtcNow.AddHours(-3) : null,
                LastActionResult = action != null ? "Nuovo: sent" : null,
            };
            var rows = new List<LockCheckRow>
            {
                Row(7097395, "Esther", "samsung", "SM-A065F", 9000, 160),
                Row(7060213, "Nahason", "samsung", "SM-A057F", 12000, 200),
                Row(7087695, "Patrick", "INFINIX", "Infinix X6850", 6000, 120),
                Row(5871723, "Joyce", "OPPO", "A18", 8000, 150),
                Row(1894069, "Aggrey", "Ulefone", "Note 14", 3000, 90, action: LockCheckAction.NuovoLock),
                Row(8143984, "Customer", "samsung", "SM-A055F", 9000, 160, recovery: 4),
                Row(3516628, "Nyanya", "motorola", "moto e30", 4430, 300, status: "un_registered", locked: true),
                Row(7004982, "Centrilla", "samsung", "SM-A055F", 37999, 400, status: "un_registered"),
            };
            foreach (var r in rows) Ranalo.Services.LockCheckRules.Evaluate(r, today, Ranalo.Services.LockCheckRules.DefaultMinDaysWithoutPayment);
            return View("~/Views/LockCheck/Index.cshtml", new LockCheckViewModel
            {
                Stage = stage,
                Counts = rows.Where(r => r.Stage != LockCheckStage.None).GroupBy(r => r.Stage).ToDictionary(g => g.Key, g => g.Count()),
                Rows = rows.Where(r => r.Stage == stage).ToList(),
            });
        }

        // Completed contracts from the database (read only), as an admin sees
        // them: /dev-preview/completed-contracts and .../{contractId}.
        [HttpGet]
        [Route("dev-preview/completed-contracts")]
        public async Task<IActionResult> CompletedContracts([FromServices] DataStore.ICompletedContractRepository repository,
            string filter = CompletedFilter.PaidOff, string? q = null, int page = 1)
        {
            if (!_env.IsDevelopment())
            {
                return NotFound();
            }

            ViewBag.IsAdmin = true;
            ViewBag.UserName = "Preview Admin";
            return View("~/Views/CompletedContracts/Index.cshtml",
                Ranalo.Services.CompletedContractRules.Build(await repository.GetClosedAsync(), filter, q, page));
        }

        [HttpGet]
        [Route("dev-preview/completed-contracts/{id:int}")]
        public async Task<IActionResult> CompletedContract([FromServices] DataStore.ICompletedContractRepository repository, int id, int page = 1)
        {
            if (!_env.IsDevelopment())
            {
                return NotFound();
            }

            var contract = await repository.GetAsync(id);
            if (contract == null)
            {
                return NotFound();
            }

            Ranalo.Services.CompletedContractRules.Evaluate(contract);
            var (payments, total) = await repository.GetPaymentsAsync(id, page, Ranalo.Services.CompletedContractRules.PageSize);
            ViewBag.IsAdmin = true;
            ViewBag.UserName = "Preview Admin";
            return View("~/Views/CompletedContracts/Details.cshtml", new CompletedContractDetailViewModel
            {
                Contract = contract,
                Payments = payments,
                PaymentsPage = Math.Max(page, 1),
                PaymentsPages = Math.Max(1, (int)Math.Ceiling(total / (double)Ranalo.Services.CompletedContractRules.PageSize)),
            });
        }

        // Repo stock pages with sample data (no database).
        private static List<RepoStockItem> RepoSamples() => new()
        {
            new() { Id = 1, AccountNo = 8478363, Imei = "352367242606595", Make = "TECNO", Model = "TECNO KM5", LockGroup = 1, CurrentLockGroup = 3,
                    OldContractId = 704, OldCustomerName = "Sammy", PaymentSuffix = "_R", Reason = RepoStockReason.Repossessed,
                    Condition = "Factory reset, screen fine, no charger", Status = RepoStockStatus.InStock, IntakeDate = DateTime.UtcNow.AddDays(-3), IntakeByName = "Preview Admin" },
            new() { Id = 2, AccountNo = 41109528, Imei = "352801608547712", Make = "samsung", Model = "SM-A165F/DS", LockGroup = 2, CurrentLockGroup = 2,
                    OldContractId = 749, OldCustomerName = "Dorothy", PaymentSuffix = "_R", Reason = RepoStockReason.Returned,
                    Condition = "Cracked back glass", Status = RepoStockStatus.Sold, IntakeDate = DateTime.UtcNow.AddDays(-20), SoldDate = DateTime.UtcNow.AddDays(-2),
                    NewCustomerName = "Grace W.", NewCustomerPhone = "0712 000000", NewContractId = 780, SoldDealerName = "Renalo", WriteOffAmount = 12450.50m },
        };

        [HttpGet]
        [Route("dev-preview/repo-stock")]
        public IActionResult RepoStock(string status = "all")
        {
            if (!_env.IsDevelopment())
            {
                return NotFound();
            }

            ViewBag.IsAdmin = true;
            ViewBag.UserName = "Preview Admin";
            var items = RepoSamples().Where(i => status == "all" || (status == "sold") == (i.Status == RepoStockStatus.Sold)).ToList();
            return View("~/Views/RepoStock/Index.cshtml", new RepoStockListViewModel { Status = status, Items = items });
        }

        [HttpGet]
        [Route("dev-preview/repo-stock/{id:int}")]
        public IActionResult RepoStockDetail(int id)
        {
            if (!_env.IsDevelopment())
            {
                return NotFound();
            }

            ViewBag.IsAdmin = true;
            ViewBag.UserName = "Preview Admin";
            var item = RepoSamples().First(i => i.Id == id);
            return View("~/Views/RepoStock/Details.cshtml", new RepoStockDetailViewModel
            {
                Item = item,
                OldContract = new RepoOldContract { ContractId = item.OldContractId, ContractValue = 47800m, TotalPaid = 11630m, LastPaymentDate = DateTime.UtcNow.AddDays(-40), BuyingPrice = 14500m },
                PreviousPayments = Enumerable.Range(0, 10).Select(n => new RepoPayment
                {
                    MpesaCode = $"UJE{n}ABC12{n}", Amount = 100m + n * 10, PaymentDate = DateTime.UtcNow.AddDays(-40 - n * 7),
                }).ToList(),
                PaymentsPage = 1,
                PaymentsTotal = 38,
                Dealers = new List<Ranalo.DataStore.Dealer>
                {
                    new() { DealerReference = "0000", CompanyName = "Renalo" },
                    new() { DealerReference = "1234", CompanyName = "Newway Mobile" },
                },
            });
        }

        [HttpGet]
        [Route("dev-preview/account-commissions")]
        public IActionResult AccountCommissions(bool agent = false, bool expand = false)
        {
            if (!_env.IsDevelopment())
            {
                return NotFound();
            }

            ViewBag.BackLink = agent ? "agent" : "admin-dashboard";
            ViewBag.IsAdmin = !agent;
            ViewBag.IsApprover = false;
            ViewBag.IsDealer = false;
            ViewBag.IsAgent = agent;
            ViewBag.UserName = agent ? "Preview Agent" : "Preview Admin";
            ViewBag.PreviewExpandRows = expand ? 3 : 0;

            AccountCommissionRow Row(long id, string customer, string product, string? agentName, int days, decimal deposit,
                decimal totalPaid, decimal? buyingPrice, bool pastLock, decimal arrears, decimal agentPaid, decimal dealerPaid) => new()
            {
                AccountId = id,
                ContractId = (id % 1000).ToString(),
                CustomerName = customer,
                ProductName = product,
                Imei = "35" + id.ToString().PadLeft(13, '0'),
                AgentName = agentName,
                DealerName = "Nairobi Mobile Hub",
                DaysSinceStart = days,
                StartDate = DateTime.Now.Date.AddDays(-days),
                Deposit = deposit,
                TotalPaid = totalPaid,
                BuyingPrice = buyingPrice,
                ContractValue = deposit * 6,
                DaysPastLock = pastLock ? 12 : 0,
                Commission = Services.CommissionCalculator.Calculate(new Services.CommissionInputs
                {
                    Deposit = deposit, DaysSinceStart = days, IsPastLockDate = pastLock, Arrears = -arrears,
                    TotalPaid = totalPaid, BuyingPrice = buyingPrice, HasAgent = agentName != null, HasWooOrder = id % 2 == 1,
                    AgentPaid = agentPaid, DealerPaid = dealerPaid,
                }),
            };

            var rows = new List<AccountCommissionRow>
            {
                Row(9237895, "Mary Achieng Odhiambo", "Samsung Galaxy A15 128GB", "John Mwangi", 216, 3000, 14500, 9000, false, 0, 2250, 300),
                Row(9711946, "Kevin Otieno", "Tecno Spark 20 Pro", "John Mwangi", 104, 4000, 9800, 11000, true, 1400, 2000, 0),
                Row(9800123, "Faith Wambui", "Infinix Hot 40i", "John Mwangi", 45, 2500, 4100, 8500, false, 0, 0, 0),
                Row(9800456, "Samuel Kiprono", "Redmi 13C", null, 130, 3500, 16000, null, false, 0, 0, 0),
            };
            if (agent)
            {
                rows = rows.Where(r => r.AgentName != null).ToList();
            }

            var model = new AccountCommissionsViewModel
            {
                IsAgentView = agent,
                ShowDealer = !agent,
                Rows = rows,
                Agent = Services.CommissionCalculator.PoolAgent(rows.Where(r => r.AgentName != null).Select(r => r.Commission)),
                Dealer = Services.CommissionCalculator.PoolDealer(rows.Select(r => r.Commission)),
                MissingBuyingPriceCount = rows.Count(r => !r.BuyingPrice.HasValue),
            };

            return View("~/Views/AccountCommissions/Index.cshtml", model);
        }

        // Enrolments list as an admin sees it, one Transsion row per state
        // (incl. the never-sent 22800359) plus Samsung/Nuovo rows.
        [HttpGet]
        [Route("dev-preview/enrolments")]
        public IActionResult Enrolments()
        {
            if (!_env.IsDevelopment())
            {
                return NotFound();
            }

            ViewBag.IsAdmin = true;
            ViewBag.UserName = "Preview Admin";
            ViewBag.PageSize = 10;
            var now = DateTime.UtcNow;
            Enrolment Row(long account, string name, string brand, EnrolmentStatus status, string? payTrigger = null,
                bool callApproved = false, bool activated = false, bool unlocked = false) => new()
            {
                Id = Guid.NewGuid(), AccountId = account, OrderId = 19795, FirstName = name, LastName = "Preview",
                IMEI = "352367242606595", DeviceBrand = brand, ProductName = brand + " Spark 50", Status = status,
                Created = now.AddDays(-account % 30), Updated = now, PayTriggerResponse = payTrigger,
                CallApprovedAt = callApproved ? now : null, ActivatedAt = activated ? now : null, UnlockedAt = unlocked ? now : null,
                EnrolledByUserId = 10, EnrolledByRole = (int)Ranalo.DataStore.DataModels.UserRole.Dealer,
            };
            var rows = new List<Enrolment>
            {
                Row(22800359, "Eddie", "TECNO", EnrolmentStatus.PendingActivation, callApproved: true),
                Row(22800360, "Jane", "TECNO", EnrolmentStatus.PendingActivation, "Pre-enrolled. Waiting for the customer to switch the phone on and connect to the internet."),
                Row(22800361, "Peter", "itel", EnrolmentStatus.Enrolled, "Pre-enrolled.", activated: true),
                Row(22800362, "Mary", "Infinix", EnrolmentStatus.Error, "Transsion refused the IMEI: IMEI exists (code 123)"),
                Row(22800363, "John", "TECNO", EnrolmentStatus.Approved, "Unlocked.", true, true, true),
                Row(22800364, "Grace", "Samsung", EnrolmentStatus.Pending),
                Row(22800365, "Moses", "Samsung", EnrolmentStatus.Approved),
                Row(36637211, "Alice", "Samsung", EnrolmentStatus.Approved),
            };
            var admin = new Ranalo.DataStore.DataModels.User { UserId = 1, RoleId = Ranalo.DataStore.DataModels.UserRole.Admin, Name = "Preview Admin" };
            var model = new EnrolmentViewModel
            {
                Enrolments = rows, CurrentPage = 1, PageSize = 10, TotalCount = rows.Count, IsAdmin = true,
                CanApprove = e => Ranalo.Services.DeviceLock.DeviceLockRules.CanApproveEnrolment(admin.RoleId, admin.UserId, e),
                LockGroups = rows.ToDictionary(e => e.AccountId, e => (int?)(e.DeviceBrand == "Samsung" ? 2 : e.UnlockedAt.HasValue ? 3 : 1)),
                KnoxActive = new Dictionary<long, bool> { [22800364] = false, [22800365] = true, [36637211] = false },
            };
            return View("~/Views/Enrolments/Index.cshtml", model);
        }

        // A real recovery from the database, read-only, as the Recovery page
        // shows it -- for checking what an admin sees without logging in.
        [HttpGet]
        [Route("dev-preview/recovery-live/{id:int}")]
        public async Task<IActionResult> RecoveryLive(int id, [FromServices] Ranalo.Services.IRecoveryService recoveries)
        {
            if (!_env.IsDevelopment())
            {
                return NotFound();
            }

            var model = await recoveries.GetDetailAsync(id);
            if (model == null)
            {
                return NotFound();
            }

            ViewBag.IsAdmin = true;
            ViewBag.UserName = "Preview Admin";
            return View("~/Views/Recovery/Details.cshtml", model);
        }

        // Knox device list search, read-only -- what Knox holds for an IMEI,
        // second IMEI or serial.
        [HttpGet]
        [Route("dev-preview/knox-search/{term}")]
        public async Task<IActionResult> KnoxSearch(string term, [FromServices] Ranalo.SumsungKnox.IKnoxGuardClient knox)
        {
            if (!_env.IsDevelopment())
            {
                return NotFound();
            }

            var list = await knox.ListDevicesAsync(new Ranalo.SumsungKnox.Models.ListDevicesRequest
            {
                PageNum = 0, PageSize = 20, SortBy = "updateTime", SortOrder = "descending", Search = term,
            });
            return Json(list);
        }

        // Knox device detail by objectId, read-only (raw reply).
        [HttpGet]
        [Route("dev-preview/knox-device/{objectId}")]
        public async Task<IActionResult> KnoxDevice(string objectId, [FromServices] Ranalo.SumsungKnox.IKnoxGuardClient knox)
        {
            if (!_env.IsDevelopment())
            {
                return NotFound();
            }

            var response = await knox.GetDeviceInfoAsync(objectId);
            return Content(await response.Content.ReadAsStringAsync(), "application/json");
        }

        // The real Recovery list, read-only.
        [HttpGet]
        [Route("dev-preview/recovery-list-live")]
        public async Task<IActionResult> RecoveryListLive([FromServices] Ranalo.Services.IRecoveryService recoveries, string status = "all")
        {
            if (!_env.IsDevelopment())
            {
                return NotFound();
            }

            ViewBag.IsAdmin = true;
            ViewBag.UserName = "Preview Admin";
            return View("~/Views/Recovery/Index.cshtml", await recoveries.GetListAsync(status));
        }

        // Recovery details for a Transsion (status = step reached) or Samsung phone.
        [HttpGet]
        [Route("dev-preview/recovery/{brand}")]
        public IActionResult Recovery(string brand, string status = RecoveryStatus.Investigating)
        {
            if (!_env.IsDevelopment())
            {
                return NotFound();
            }

            ViewBag.IsAdmin = true;
            ViewBag.UserName = "Preview Admin";
            var r = new LockRecovery
            {
                Id = 5, AccountNo = 8478363, Imei = "352367242606595", Make = brand, Model = brand == "samsung" ? "SM-A065F" : "TECNO KM5",
                CustomerName = "Preview Customer", DealerName = "Preview Dealer", OriginalLockGroup = 1, CurrentLockGroup = 1,
                Reason = "Collections", Status = status, CreatedAtUtc = DateTime.UtcNow.AddDays(-3), UpdatedAtUtc = DateTime.UtcNow,
                NextLockDate = "28/05/2026", LastConnectedAt = "12-10-26 08:14:00 UTC", Notes = "Repossessed; test phone.",
            };
            var model = new RecoveryDetailViewModel
            {
                Recovery = r,
                Actions = Ranalo.Services.RecoveryRules.AllowedActions(r),
                Attempts = new List<LockRecoveryAttempt>
                {
                    new() { Step = "Added", Success = true, ByName = "Preview Admin", AtUtc = DateTime.UtcNow.AddDays(-3), Response = "On Nuovo, TECNO TECNO KM5, IMEI 352367242606595." },
                    new() { Step = "TranssionCheck", Success = false, ByName = "Preview Admin", AtUtc = DateTime.UtcNow.AddHours(-2),
                        Response = "Not found in Transsion: The device-lock not exist (code 50071)\n{\"code\":50071,\"message\":\"The device-lock not exist\",\"data\":null}" },
                },
            };
            return View("~/Views/Recovery/Details.cshtml", model);
        }

        // Stock phones with sample rows (the Enrolments stock columns need
        // Database/StockPhones/001): /dev-preview/stock-phones.
        [HttpGet]
        [Route("dev-preview/stock-phones")]
        public IActionResult StockPhones(string filter = Ranalo.Services.DeviceLock.StockPhoneRules.FilterAll)
        {
            if (!_env.IsDevelopment())
            {
                return NotFound();
            }

            var now = DateTime.UtcNow;
            var rows = new List<Enrolment>
            {
                new() { Id = Guid.NewGuid(), IMEI = "356938035643809", DeviceBrand = "TECNO", Status = EnrolmentStatus.Stock, DealerId = 4,
                        StockLockedAt = now.AddDays(-2), ActivatedAt = now.AddDays(-2).AddMinutes(20), StockVerifiedAt = now.AddDays(-2).AddMinutes(25),
                        StockCheck = "Locked by Transsion. Last connected 17/10/2026 09:25 UTC." },
                new() { Id = Guid.NewGuid(), IMEI = "350522366526910", DeviceBrand = "Samsung", Status = EnrolmentStatus.Stock,
                        StockLockedAt = now.AddHours(-1) },
                new() { Id = Guid.NewGuid(), IMEI = "350522366526928", DeviceBrand = "Samsung", Status = EnrolmentStatus.Stock, DealerId = 4,
                        StockLockedAt = now.AddHours(-3), StockKnoxApprovedAt = now.AddHours(-3),
                        StockCheck = "Active in Knox, but the lock hasn't reached the phone yet: connect it to the internet. Last seen 19/10/2026 06:02 UTC." },
                new() { Id = Guid.NewGuid(), IMEI = "356938035643817", DeviceBrand = "itel", Status = EnrolmentStatus.Stock,
                        StockLockedAt = now.AddMinutes(-30), ActivatedAt = now.AddMinutes(-10) },
            };
            var model = new StockPhonesViewModel
            {
                Filter = filter,
                Total = rows.Count,
                Unverified = rows.Count(Ranalo.Services.DeviceLock.StockPhoneRules.NeedsVerifying),
                Rows = filter == Ranalo.Services.DeviceLock.StockPhoneRules.FilterUnverified
                    ? rows.Where(Ranalo.Services.DeviceLock.StockPhoneRules.NeedsVerifying).ToList() : rows,
                Dealers = new Dictionary<int, string> { [4] = "Newway Mobiles", [7] = "Kisumu Phones" },
            };
            model.Messages.Add("356938035643817: sent to Transsion. It locks the first time it's switched on and connected.");
            ViewBag.IsAdmin = true;
            ViewBag.UserName = "Preview Admin";
            return View("~/Views/StockPhones/Index.cshtml", model);
        }
    }
}
