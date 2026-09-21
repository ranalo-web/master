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
        private readonly IDashboardReportService _dashboardReportService;
        public ApproverController(IApplicationReportService applicationReportService, IUserService userService, IDashboardReportService dashboardReportService)
        {
            _applicationReportService = applicationReportService;
            _userService = userService;
            _dashboardReportService = dashboardReportService;
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

            // Approver's own landing page: a real KPI dashboard, same
            // card-grid view as Dealer/Agent (reused, not duplicated -- see
            // GetApproverDashboardAsync's doc comment for what's included/
            // skipped and why), system-wide across every dealer. The
            // previous "All Orders" awaiting-approval table this route used
            // to render directly moved to its own page -- see Orders() below.
            if (settings.RoleId == UserRole.Admin || settings.RoleId == UserRole.Approver)
            {
                // SetViewBags only sets IsApprover for the Approver role
                // itself; this dashboard is the same for Admin visiting the
                // same route, so force it here too -- every role-gate on
                // DealerDashboard/Index.cshtml keys off ViewBag.IsApprover,
                // not the user's actual RoleId.
                ViewBag.IsApprover = true;
                var model = await _dashboardReportService.GetApproverDashboardAsync();
                return View("~/Views/DealerDashboard/Index.cshtml", model);
            }

            var waitingApprovalByUser = await _applicationReportService.GetAwaitingApprovalOrdersByUser(settings.UserId, page: page, pageSize: pageSize);
            ViewData["OrdersStatus"] = "All Orders";
            return View(waitingApprovalByUser);

        }

        [HttpGet]
        [Route("approver-orders/{page:int?}")]
        public async Task<IActionResult> Orders(int page = 1, int pageSize = 10)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }
            await SetViewBags(settings, "approver");

            if (settings.RoleId != UserRole.Admin && settings.RoleId != UserRole.Approver)
            {
                return RedirectToAction("Index", "Approver");
            }

            var allAwaitngApproval = await _applicationReportService.GetAwaitingApprovalOrders(page: page, pageSize: pageSize);
            ViewData["OrdersStatus"] = "Waiting Approval";
            return View("~/Views/Approver/Index.cshtml", allAwaitngApproval);
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
            ViewBag.IsDealer = settings.RoleId == UserRole.Dealer;
            ViewBag.UserName = settings.KnownAs;
            if (settings.RoleId == UserRole.Dealer)
            {
                var dealer = await _userService.GetDealerByUserId(settings.UserId);
                ViewBag.UserName = dealer.CompanyName;
            }
        }
    }
}
