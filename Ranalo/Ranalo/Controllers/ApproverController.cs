using Microsoft.AspNetCore.Mvc;
using Ranalo.Configuration;
using Ranalo.DataStore.DataModels;
using Ranalo.Services;

namespace Ranalo.Controllers
{
    [LoadUserSettingsFromCookie]
    public class ApproverController : Controller
    {
        private readonly IApplicationReportService _applicationReportService;
        private readonly IUserService _userService;
        public ApproverController(IApplicationReportService applicationReportService, IUserService userService)
        {
            _applicationReportService = applicationReportService;
            _userService = userService;
        }

        [HttpGet]
        [Route("approver/{page:int?}")]
        public async Task<IActionResult> Index(int page = 1, int pageSize = 10)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }
            ViewBag.BackLink = "approver";
            ViewBag.IsAdmin = settings.RoleId == 1;
            ViewBag.IsApprover = settings.RoleId == 5;

            if (settings.RoleId == 1 || settings.RoleId == 5)
            {
                var allAwaitngApproval = await _applicationReportService.GetAwaitingApprovalOrders(page: page, pageSize: pageSize);
                var user = await _userService.GetUserByCustomerIdAsync(settings.UserId);
                ViewBag.UserName = user.KnownAs;
                ViewData["OrdersStatus"] = "Waiting Approval";

                return View("~/Views/Approver/Index.cshtml", allAwaitngApproval);
            }

            var dealer = await _userService.GetDealerByUserId(settings.UserId);
            ViewBag.UserName = dealer.CompanyName;
            

            var waitingApprovalByUser = await _applicationReportService.GetAwaitingApprovalOrdersByUser(settings.UserId, page: page, pageSize: pageSize);
            ViewData["OrdersStatus"] = "All Orders";
            return View(waitingApprovalByUser);

        }

        [HttpPost]
        [Route("reject-order")]
        public async Task<IActionResult> Reject(long orderId)
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
            var isApproved = await _applicationReportService.RejectOrderAsync(orderId);
            //var allPaymentsByUser = await _applicationReportService.GetAllPaymentsAsync(settings.UserId, "searchTerm");
            return Redirect($"/customer-details/{orderId}");
        }

        [HttpPost]
        [Route("approve-order")]
        public async Task<IActionResult> Approve(long orderId)
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
            var isApproved = await _applicationReportService.ApproveOrderAsync(orderId);

            return Redirect($"/customer-details/{orderId}");
        }
    }
}
