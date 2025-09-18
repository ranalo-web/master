using Microsoft.AspNetCore.Mvc;
using Ranalo.Configuration;
using Ranalo.DataStore;
using Ranalo.DataStore.DataModels;
using Ranalo.Models;
using Ranalo.Services;
using System.Drawing.Printing;

namespace Ranalo.Controllers
{
    [LoadUserSettingsFromCookie]
    public class HomeController : Controller
    {
        private readonly IApplicationReportService _applicationReportService;
        private readonly IUserService _userService;
        private readonly IStatementService _statementService;
        public HomeController(IApplicationReportService applicationReportService, IUserService userService, IStatementService statementService)
        {
            _applicationReportService = applicationReportService;
            _userService = userService;
            _statementService = statementService;
        }

        [HttpGet]
        [Route("orders/{page:int?}")]
        public async Task<IActionResult> Index(int page = 1, int pageSize = 10)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }
            await SetViewBags(settings, "index");

            if (settings.RoleId == UserRole.Admin)
            {
                var allAwaitngApproval = await _applicationReportService.GetAwaitingApprovalOrders(page:page, pageSize:pageSize);
                ViewData["OrdersStatus"] = "Waiting Approval";
                
                return View("~/Views/Home/Index.cshtml", allAwaitngApproval);
            }

            if(settings.RoleId == UserRole.Approver)
            {
                return RedirectToAction("Index", "Approver");
            }

            var dealer = await _userService.GetDealerByUserId(settings.UserId);
            var waitingApprovalByUser = await _applicationReportService.GetAwaitingApprovalOrdersByUser(settings.UserId, page:page, pageSize:pageSize);
            ViewData["OrdersStatus"] = "All Orders";
            return View(waitingApprovalByUser);

        }

        [HttpGet]
        [Route("never-paid/{page:int?}")]
        public async Task<IActionResult> NeverPaid(int page = 1, int pageSize = 10)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }
            await SetViewBags(settings, "index");

            if (settings.RoleId == UserRole.Admin)
            {
                var neverPaid = await _applicationReportService.GetAllNeverPaidOrdersAsync(page: page, pageSize: pageSize);
                ViewData["OrdersStatus"] = "Waiting Approval";

                return View("~/Views/Reports/NeverPaid.cshtml", neverPaid);
            }

            if (settings.RoleId == UserRole.Approver)
            {
                return RedirectToAction("Index", "Approver");
            }

            return RedirectToAction("Index", "Login");
        }

        [HttpPost]
        [Route("never-paid")]
        public async Task<IActionResult> NeverPaid(string searchTerm)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }
            await SetViewBags(settings, "index");

            if (settings.RoleId == UserRole.Admin)
            {
                var neverPaid = await _applicationReportService.GetAllNeverPaidOrdersAsync(searchTerm: searchTerm);
                ViewData["OrdersStatus"] = "Waiting Approval";

                return View("~/Views/Reports/NeverPaid.cshtml", neverPaid);
            }

            if (settings.RoleId == UserRole.Approver)
            {
                return RedirectToAction("Index", "Approver");
            }

            return RedirectToAction("Index", "Login");
        }

        [Route("orphanedpayments/{page:int?}")]
        public async Task<IActionResult> OrphanedPayments(int page = 1, int pageSize = 10)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }
            await SetViewBags(settings, "index");

            var orphanedPayments = await _applicationReportService.GetOrphanedPaymentsAsync(page, pageSize);

            return View(orphanedPayments);
        }

        [Route("allpayments/{page:int?}")]
        public async Task<IActionResult> AllPayments(int page = 1, int pageSize = 10)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            await SetViewBags(settings, "index");

            if (settings.RoleId == UserRole.Admin || settings.RoleId == UserRole.Approver)
            {
                var allPayments = await _applicationReportService.GetAllPaymentsAsync(page:page, pageSize:pageSize);

                return View(allPayments);
            }

            var allPaymentsByUser = await _applicationReportService.GetAllPaymentsAsync(settings.UserId, page: page, pageSize: pageSize);

            return View(allPaymentsByUser);
        }

        [Route("statements/{page:int?}")]
        public async Task<IActionResult> Statements(int page = 1, int pageSize = 10)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            await SetViewBags(settings, "index");

            var statement = await _statementService.GetStatementForDealerWithTransactionsAsync(settings.DealerId, settings.DealerId);

            return View(statement);
        }

        [HttpPost("upload")]
        public async Task<IActionResult> Statements(IFormFile file, CancellationToken cancellationToken)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            await SetViewBags(settings, "index");

            using (var stream = file.OpenReadStream()) // IFormFile from upload
            {
                var mapper = new BankStatementMapper();
                var statement = mapper.MapFromExcel(stream, settings.DealerId, file.FileName);

                // Now you can save with your Dapper insert methods
                await _statementService.CreateNewStatementAsync(statement);
            }

            //Now get the latest staements

            return RedirectToAction("Statements", "Home");
        }


        [Route("paymentsummary")]
        public async Task<IActionResult> PaymentSummary()
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }
            await SetViewBags(settings, "index");

            var allAwaitngApproval = await _applicationReportService.PaymentsSummary();

            return View(allAwaitngApproval.ToList());
        }

        [HttpPost]
        [Route("allpayments")]
        public async Task<IActionResult> Search(string searchTerm)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            await SetViewBags(settings, "index");

            if (settings.RoleId == UserRole.Admin || settings.RoleId == UserRole.Approver)
            {
                var allPayments = await _applicationReportService.GetAllPaymentsAsync(searchTerm);

                return View("AllPayments", allPayments);
            }

            var allPaymentsByUser = await _applicationReportService.GetAllPaymentsAsync(settings.UserId, searchTerm);

            return View("AllPayments", allPaymentsByUser);
        }

        [HttpPost]
        [Route("orders")]
        public async Task<IActionResult> SearchDashBoard(string searchTerm)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }
            await SetViewBags(settings, "index");

            if (settings.RoleId == UserRole.Admin)
            {
                var allAwaitngApproval = await _applicationReportService.GetAwaitingApprovalOrders(searchTerm);
                allAwaitngApproval.SearchTerm = searchTerm;
                ViewData["OrdersStatus"] = "Waiting Approval";

                return View("~/Views/Home/Index.cshtml", allAwaitngApproval);
            }

            var waitingApprovalByUser = await _applicationReportService.GetAwaitingApprovalOrdersByUser(settings.UserId, searchTerm);
            ViewData["OrdersStatus"] = "All Orders";
            waitingApprovalByUser.SearchTerm = searchTerm;
            return View(waitingApprovalByUser);
        }

        [Route("users")]
        public async Task<IActionResult> Users()
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            await SetViewBags(settings, "index");

            if (settings.RoleId == UserRole.Admin || settings.RoleId == UserRole.Approver)
            {
                var users = new UsersViewModel()
                {
                    Users = await _userService.GetAllUsersAsync()
                };

                return View(users);
            }

            var dealerUsers = new UsersViewModel()
            {
                Users = await _userService.GetUsersByDealerIdAsync(settings.DealerId)
            };

            return View(dealerUsers);
        }


        [Route("adduser")]
        public async Task<IActionResult> AddUser()
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            await SetViewBags(settings, "index");

            return View();
        }

        [HttpPost]
        [Route("adduser")]
        public async Task<IActionResult> AddUserSubmit(User userDetails)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            await SetViewBags(settings, "index");

            try
            {
                userDetails.Status = UserStatus.Active;
                userDetails.DealerId = settings.DealerId;
                userDetails.IsActive = true;
                await _userService.AddUserAsync(userDetails);
            }
            catch (Exception)
            {

                return View("AddUser");
            }

            return RedirectToAction("Users");
        }

        private async Task SetViewBags(User settings, string backLink)
        {
            ViewBag.BackLink = backLink;
            ViewBag.IsAdmin = settings.RoleId == UserRole.Admin;
            ViewBag.IsApprover = settings.RoleId == UserRole.Approver;
            ViewBag.UserName = settings.KnownAs;
            if (settings.RoleId == UserRole.Dealer)
            {
                var dealer = await _userService.GetDealerByUserId(settings.UserId);
                ViewBag.UserName = dealer.CompanyName;
                settings.DealerId = dealer.DealerId;
            }
        }

    }
}
