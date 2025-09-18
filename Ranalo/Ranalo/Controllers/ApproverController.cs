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
            await SetViewBags(settings, "approver");

            if (settings.RoleId == UserRole.Admin || settings.RoleId == UserRole.Approver)
            {
                var allAwaitngApproval = await _applicationReportService.GetAwaitingApprovalOrders(page: page, pageSize: pageSize);
                var user = await _userService.GetUserByCustomerIdAsync(settings.UserId);
                ViewData["OrdersStatus"] = "Waiting Approval";
                return View("~/Views/Approver/Index.cshtml", allAwaitngApproval);
            }

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

            await SetViewBags(settings, "approver");

            var isApproved = await _applicationReportService.RejectOrderAsync(orderId);
            //var allPaymentsByUser = await _applicationReportService.GetAllPaymentsAsync(settings.UserId, "searchTerm");
            return Redirect($"/order-details/{orderId}");
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

            await SetViewBags(settings, "approver"); await SetViewBags(settings, "approver");

            var isApproved = await _applicationReportService.ApproveOrderAsync(orderId);

            return Redirect($"/order-details/{orderId}");
        }

        [HttpPost]
        [Route("add-note")]
        public async Task<IActionResult> AddNote(long orderId, string customerNote)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            await SetViewBags(settings, "approver"); await SetViewBags(settings, "approver");

            await _applicationReportService.AddCustomerNoteAsync(settings.UserId, orderId, customerNote);

            return Redirect($"/order-details/{orderId}");
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
            }
        }
    }
}
