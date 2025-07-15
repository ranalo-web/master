using Microsoft.AspNetCore.Mvc;
using Ranalo.Configuration;
using Ranalo.DataStore;
using Ranalo.DataStore.DataModels;
using Ranalo.Services;
using System.Drawing.Printing;

namespace Ranalo.Controllers
{
    [LoadUserSettingsFromCookie]
    public class HomeController : Controller
    {
        private readonly IApplicationReportService _applicationReportService;
        private readonly IUserService _userService;
        public HomeController(IApplicationReportService applicationReportService, IUserService userService)
        {
            _applicationReportService = applicationReportService;
            _userService = userService;
        }

        [HttpGet]
        [Route("dashboard/{page:int?}")]
        public async Task<IActionResult> Index(int page = 1, int pageSize = 10)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }
            ViewBag.BackLink = "dashboard";
            ViewBag.IsAdmin = settings.RoleId == 1;
            ViewBag.IsApprover = settings.RoleId == 5;
            if (settings.RoleId == 1)
            {
                var allAwaitngApproval = await _applicationReportService.GetAwaitingApprovalOrders(page:page, pageSize:pageSize);
                ViewData["OrdersStatus"] = "Waiting Approval";
                
                return View("~/Views/Home/Index.cshtml", allAwaitngApproval);
            }

            if(settings.RoleId == 5)
            {
                return RedirectToAction("Index", "Approver");
            }

            var dealer = await _userService.GetDealerByUserId(settings.UserId);
            ViewBag.UserName = dealer.CompanyName;
            var waitingApprovalByUser = await _applicationReportService.GetAwaitingApprovalOrdersByUser(settings.UserId, page:page, pageSize:pageSize);
            ViewData["OrdersStatus"] = "All Orders";
            return View(waitingApprovalByUser);

        }

        [Route("orphanedpayments")]
        public async Task<IActionResult> OrphanedPayments()
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }
            var dealer = await _userService.GetDealerByUserId(settings.UserId);
            ViewBag.UserName = dealer.CompanyName;
            ViewBag.IsAdmin = settings.RoleId == 1;
            ViewBag.IsApprover = settings.RoleId == 5;
            var allAwaitngApproval = await _applicationReportService.GetOrphanedPaymentsAsync();

            return View(allAwaitngApproval);
        }

        [Route("allpayments/{page:int?}")]
        public async Task<IActionResult> AllPayments(int page = 1, int pageSize = 10)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }
            
            if (settings.RoleId == 2)
            {
                var dealer = await _userService.GetDealerByUserId(settings.UserId);
                ViewBag.UserName = dealer.CompanyName;
            }
            else
            {
                var user = await _userService.GetUserByCustomerIdAsync(settings.UserId);
                ViewBag.UserName = user.KnownAs;
            }


                ViewBag.IsAdmin = settings.RoleId == 1;
            ViewBag.IsApprover = settings.RoleId == 5;
            if (settings.RoleId == 1 || settings.RoleId == 5)
            {
                var allPayments = await _applicationReportService.GetAllPaymentsAsync(page:page, pageSize:pageSize);

                return View(allPayments);
            }

            var allPaymentsByUser = await _applicationReportService.GetAllPaymentsAsync(settings.UserId, page: page, pageSize: pageSize);

            return View(allPaymentsByUser);
        }

        [Route("paymentsummary")]
        public async Task<IActionResult> PaymentSummary()
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }
            var dealer = await _userService.GetDealerByUserId(settings.UserId);
            ViewBag.UserName = dealer.CompanyName;
            ViewBag.IsAdmin = settings.RoleId == 1;
            ViewBag.IsApprover = settings.RoleId == 5;
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

            if (settings.RoleId == 2)
            {
                var dealer = await _userService.GetDealerByUserId(settings.UserId);
                ViewBag.UserName = dealer.CompanyName;
            }
            else
            {
                var user = await _userService.GetUserByCustomerIdAsync(settings.UserId);
                ViewBag.UserName = user.KnownAs;
            }

            ViewBag.IsAdmin = settings.RoleId == 1;
            ViewBag.IsApprover = settings.RoleId == 5;

            if (settings.RoleId == 1 || settings.RoleId == 5)
            {
                var allPayments = await _applicationReportService.GetAllPaymentsAsync(searchTerm);

                return View("AllPayments", allPayments);
            }

            var allPaymentsByUser = await _applicationReportService.GetAllPaymentsAsync(settings.UserId, searchTerm);

            return View("AllPayments", allPaymentsByUser);
        }

        [HttpPost]
        [Route("dashboard")]
        public async Task<IActionResult> SearchDashBoard(string searchTerm)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            ViewBag.IsAdmin = settings.RoleId == 1;
            ViewBag.IsApprover = settings.RoleId == 5;
            if (settings.RoleId == 1)
            {
                var allAwaitngApproval = await _applicationReportService.GetAwaitingApprovalOrders(searchTerm);
                allAwaitngApproval.SearchTerm = searchTerm;
                ViewData["OrdersStatus"] = "Waiting Approval";

                return View("~/Views/Home/Index.cshtml", allAwaitngApproval);
            }


            if (settings.RoleId == 5)
            {
                var dealer = await _userService.GetDealerByUserId(settings.UserId);
                ViewBag.UserName = dealer.CompanyName;
            }
            else
            {
                var user = await _userService.GetUserByCustomerIdAsync(settings.UserId);
                ViewBag.UserName = user.KnownAs;
            }


            var waitingApprovalByUser = await _applicationReportService.GetAwaitingApprovalOrdersByUser(settings.UserId, searchTerm);
            ViewData["OrdersStatus"] = "All Orders";
            waitingApprovalByUser.SearchTerm = searchTerm;
            return View(waitingApprovalByUser);
        }


    }
}
