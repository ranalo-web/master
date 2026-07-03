using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Ranalo.Configuration;
using Ranalo.DataStore.DataModels;
using Ranalo.Services;

namespace Ranalo.Controllers
{
    [LoadUserSettingsFromCookie]
    public class AgentsController : Controller
    {
        private readonly IApplicationReportService _applicationReportService;
        private readonly IContractService _contractService;
        private readonly IUserService _userService;
        public AgentsController(IApplicationReportService applicationReportService, IUserService userService, IContractService contractService)
        {
            _applicationReportService = applicationReportService;
            _userService = userService;
            _contractService = contractService;
        }

        [HttpGet]
        [Route("agent/{page:int?}")]
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

        [HttpGet]
        [Route("assigned-accounts/{page:int?}")]
        public async Task<IActionResult> AssignedCollections(string searchTerm = "", int page = 1, int pageSize = 10)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }
            //Page Origin ViewBad
            ViewBag.PageOrigin = "assigned";
            await SetViewBags(settings, "approver");

            //Set debt collectors
            var collectors = await _userService.GetDebtCollectors();

            ViewBag.Collectors = collectors
                .Select(x => new SelectListItem
                {
                    Value = x.UserId.ToString(),
                    Text = $"{x.Name} {x.LastName}"
                })
                .ToList();

            if (settings.RoleId == UserRole.Admin || settings.RoleId == UserRole.Approver)
            {
                //var allPaymentSummaries = await _applicationReportService.GetStatusReportByDealer(null,null);

                var allPaymentSummaries = await _applicationReportService.CallQualifyingFunc(false, true, true, null, null, page, pageSize, searchTerm.Trim());


                ViewData["OrdersStatus"] = "Waiting Approval";

                return View(allPaymentSummaries);
            }

            var dealer = await _userService.GetDealerByUserId(settings.UserId);

            var dealerId = Convert.ToInt32(dealer.DealerReference);

            var allDelaerStatusReport = await _applicationReportService.CallQualifyingFunc(false, true, true, null, dealerId, page, pageSize, searchTerm.Trim());

            return View(allDelaerStatusReport);

        }


        [HttpGet]
        [Route("unassigned-accounts/{page:int?}")]
        public async Task<IActionResult> UnAssignedCollections(string searchTerm = "", int page = 1, int pageSize = 10)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }
            ViewBag.PageOrigin = "unassigned";

            await SetViewBags(settings, "approver");

            if (settings.RoleId == UserRole.Admin || settings.RoleId == UserRole.Approver)
            {

                //Set debt collectors
                var agents = await _userService.GetAgents();

                ViewBag.Collectors = agents
                    .Select(x => new SelectListItem
                    {
                        Value = x.UserId.ToString(),
                        Text = $"{x.Name} {x.LastName}"
                    })
                    .ToList();


                var allPaymentSummaries = await _applicationReportService.CallQualifyingFunc(false, true, false, null, null, page, pageSize, searchTerm.Trim());


                ViewData["OrdersStatus"] = "Waiting Approval";

                return View("AssignedCollections", allPaymentSummaries);
            }

            var dealer = await _userService.GetDealerByUserId(settings.UserId);

            if(dealer != null)
            {
                var dealerId = Convert.ToInt32(dealer.DealerReference);
                //Set debt collectors
                var agents = await _userService.GetAgentsByDealer(dealer.DealerId);

                ViewBag.Collectors = agents
                    .Select(x => new SelectListItem
                    {
                        Value = x.UserId.ToString(),
                        Text = $"{x.Name} {x.LastName}"
                    })
                    .ToList();
                var records = await _contractService.GetAccountsByDealer(dealerId, page, pageSize, searchTerm.Trim());

                return View("Unassigned", records);
            }

            return View("Index");

        }

        [HttpPost]
        [Route("assign-collector")]
        public async Task<IActionResult> AssignAccountCollector(long displayDeviceId,
            string deposit,
            string oldName,
            int debtCollectorUserId)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            await SetViewBags(settings, "collector");

            //await _contractorService.AssignContractToCollector((int)displayDeviceId, debtCollectorUserId);

            return RedirectToAction("Collections", "Reports");
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
