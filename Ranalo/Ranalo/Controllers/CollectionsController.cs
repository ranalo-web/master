using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Ranalo.Calculator.Logic.Models;
using Ranalo.Configuration;
using Ranalo.DataStore.DataModels;
using Ranalo.Models;
using Ranalo.ScheduledServices;
using Ranalo.Services;
using Ranalo.Woocommece.Api.Models;

namespace Ranalo.Controllers
{
    [LoadUserSettingsFromCookie]
    public class CollectionsController : Controller
    {
        private readonly IApplicationReportService _applicationReportService;
        private readonly IUserService _userService;
        private readonly IContractService _contractorService;
        private readonly IDeviceProcessor _deviceProcessor;
        private readonly ICollectionsService _collections;
        private readonly ICommissionPayoutService _payouts;
        private readonly ILogger<CollectionsController> _logger;

        public CollectionsController(IApplicationReportService applicationReportService,
            IUserService userService,
            IContractService contractorService,
            IDeviceProcessor deviceProcessor, ICollectionsService collections, ICommissionPayoutService payouts,
            ILogger<CollectionsController> logger)
        {
            _applicationReportService = applicationReportService;
            _userService = userService;
            _contractorService = contractorService;
            _deviceProcessor = deviceProcessor;
            _collections = collections;
            _payouts = payouts;
            _logger = logger;
        }

        // Collector dashboard: the accounts they hold, what they've recovered
        // and earned. Also reachable by dealers/agents who are collectors too.
        [Route("collections-home/{page:int?}")]
        public async Task<IActionResult> Index()
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }
            if (settings.RoleId == UserRole.Admin)
            {
                return RedirectToAction("Index", "CollectionsAdmin");
            }
            if (!CollectionsService.IsCollector(settings))
            {
                return RedirectToAction("Index", "Login");
            }

            await SetViewBags(settings, "collector");
            ViewBag.CollectorOnly = settings.RoleId == UserRole.Collector;

            var model = await _collections.GetCollectorDashboardAsync(settings.UserId);
            model.Payouts = await _payouts.GetPayoutsAsync(CommissionPayeeType.Collector, collectorUserId: settings.UserId, top: 10);
            return View(model);
        }

        // "Can't reach this customer" -- a note for the admin team.
        [HttpPost]
        [Route("collections/flag")]
        public async Task<IActionResult> FlagCase(int caseId, string? note)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            var (ok, message) = await _collections.AddFlagAsync(caseId, settings.UserId, note);
            TempData[ok ? "CollectionsSuccess" : "CollectionsError"] = message;
            return RedirectToAction("Index");
        }

        [HttpGet]
        [Route("new-collections/{page:int?}")]
        public async Task<IActionResult> NewCollections(string searchTerm = "", int page = 1, int pageSize = 10)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            await SetViewBags(settings, "collector");

            if (settings.RoleId == UserRole.Collector)
            {
                //var allPaymentSummaries = await _applicationReportService.GetStatusReportByDealer(null,null);

                var allPaymentSummaries = await _contractorService.GetCollectorsContractSummaryAsync(settings.UserId, null, 0, page, pageSize, searchTerm.Trim());

                ViewData["OrdersStatus"] = "Waiting Approval";

                return View(allPaymentSummaries);
            }

            if (settings.RoleId == UserRole.Admin)
            {
                //var allPaymentSummaries = await _applicationReportService.GetStatusReportByDealer(null,null);

                var allPaymentSummaries = await _contractorService.GetCollectorsContractSummaryAsync(0, null, 0, page, pageSize, searchTerm.Trim());

                ViewData["OrdersStatus"] = "Waiting Approval";

                return View(allPaymentSummaries);
            }

            //settings.RoleId == UserRole.Admin || 
            return View(new StatusReportViewModel());

        }

        [Route("customer-details/{accountId:int}")]
        public async Task<IActionResult> CustomerDetails(long accountId)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            await SetViewBags(settings, "customer");

            var customerDetails = await _applicationReportService.GetCustomerDetailsByAccountIdAsync(accountId);

            if (customerDetails == null)
            {
                return View(customerDetails);
            }
            //Get customer Notes
            var notes = await _applicationReportService.GetNotesByOrderIdAsync(accountId);
            customerDetails.AccountId = (int)accountId;
            customerDetails.CustomerId = accountId.ToString();
            if (notes != null)
            {
                foreach (var note in notes)
                {
                    var userDetails = await _userService.GetUserByCustomerIdAsync(note.UserId);
                    if (userDetails.RoleId == UserRole.Dealer)
                    {
                        var dealer = await _userService.GetDealerByUserId(note.UserId);
                        note.UserName = dealer.CompanyName;
                    }
                    else
                    {
                        note.UserName = userDetails.Name;
                    }

                }
                customerDetails.Notes = notes;
            }

            return View(customerDetails);
        }

        [HttpPost]
        [Route("addcollectornote")]
        public async Task<IActionResult> AddCollectorNote(string orderId, string customerNote)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            await SetViewBags(settings, "collector"); await SetViewBags(settings, "approver");

            await _applicationReportService.AddCustomerNoteAsync(settings.UserId, long.Parse(orderId), customerNote);

            return Redirect($"/customer-details/{orderId}");
        }

        [HttpGet]
        [Route("assigned-collections/{page:int?}")]
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

            // An Agent isn't a Dealer themselves, so GetDealerByUserId (which
            // looks up Dealers.UserId) never resolves for them -- their own
            // Users.DealerId already names the dealer they belong to.
            var dealer = settings.RoleId == UserRole.Agent
                ? await _userService.GetDealerByDealerId(settings.DealerId)
                : await _userService.GetDealerByUserId(settings.UserId);

            var dealerId = Convert.ToInt32(dealer.DealerReference);

            // Collections is a single table for an Agent -- just the
            // accounts assigned to them (Contract_Info.AssignedAgentId), not
            // the dealer-wide notPaid90/assigned-to-a-collector queue.
            if (settings.RoleId == UserRole.Agent)
            {
                var agentCollections = await _applicationReportService.CallQualifyingFunc(false, false, false, null, dealerId, page, pageSize, searchTerm.Trim(), agentUserId: settings.UserId);

                // Their accounts now with a collector, and the frozen arrears.
                ViewBag.InCollections = await _collections.GetInCollectionsAsync(settings.DealerId, settings.UserId);
                return View(agentCollections);
            }

            var allDelaerStatusReport = await _applicationReportService.CallQualifyingFunc(false, true, true, null, dealerId, page, pageSize, searchTerm.Trim());

            return View(allDelaerStatusReport);

        }


        [HttpGet]
        [Route("unassigned-collections/{page:int?}")]
        public async Task<IActionResult> UnAssignedCollections(string searchTerm = "", int page = 1, int pageSize = 10)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            // Collections is a single table for an Agent (their own assigned
            // accounts) -- there's no separate "unassigned" view for them.
            if (settings.RoleId == UserRole.Agent)
            {
                return RedirectToAction("AssignedCollections", new { searchTerm, page, pageSize });
            }

            ViewBag.PageOrigin = "unassigned";
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

                var allPaymentSummaries = await _applicationReportService.CallQualifyingFunc(false, true, false, null, null, page, pageSize, searchTerm.Trim());


                ViewData["OrdersStatus"] = "Waiting Approval";

                return View("AssignedCollections", allPaymentSummaries);
            }

            var dealer = settings.RoleId == UserRole.Agent
                ? await _userService.GetDealerByDealerId(settings.DealerId)
                : await _userService.GetDealerByUserId(settings.UserId);

            var dealerId = Convert.ToInt32(dealer.DealerReference);

            var allDelaerStatusReport = await _applicationReportService.CallQualifyingFunc(false, true, false, null, dealerId, page, pageSize, searchTerm.Trim());

            return View("AssignedCollections", allDelaerStatusReport);
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

            // Only admin hands accounts to collectors, and always through the
            // collections rules (frozen arrears, eligibility, no own accounts).
            if (settings.RoleId != UserRole.Admin)
            {
                return RedirectToAction("Collections", "Reports");
            }

            try
            {
                var (ok, message) = await _collections.AssignAsync(new[] { displayDeviceId }, debtCollectorUserId, settings.UserId);
                TempData[ok ? "CollectionsSuccess" : "CollectionsError"] = message;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Assigning collector to {AccountNo} failed", displayDeviceId);
                TempData["CollectionsError"] = "Nothing was changed: it could not be saved. Has Database/Collections/001_create_collections_tables.sql been run?";
            }

            return RedirectToAction("Index", "CollectionsAdmin", new { tab = "pool" });
        }

        [HttpPost]
        [Route("lock-device")]
        public async Task<IActionResult> LockCustomerDeviceAsync(long displayDeviceId,
            string customerName,
            string lockDate)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            // Agents and Approvers have view-only access to the Collections
            // tab -- no power to lock devices from here.
            if (settings.RoleId == UserRole.Agent || settings.RoleId == UserRole.Approver)
            {
                return RedirectToAction("Collections", "Collections");
            }

            await SetViewBags(settings, "collector");

            DateTime finalDate = string.IsNullOrWhiteSpace(lockDate)
            ? DateTime.Now
            : DateTime.Parse(lockDate);

            var lockTransaction = new LockTransaction()
            {
                AccountId = displayDeviceId,
                FirstName = customerName,
                AutoLockDate = finalDate
            };

            await _deviceProcessor.ProcessSingleAsync(lockTransaction, _logger);

            return RedirectToAction("Collections", "Collections");
        }

        private async Task SetViewBags(User settings, string backLink, string searchTerm = "")
        {
            ViewBag.BackLink = backLink;
            ViewBag.IsAdmin = settings.RoleId == UserRole.Admin;
            ViewBag.IsApprover = settings.RoleId == UserRole.Approver;
            ViewBag.IsDealer = settings.RoleId == UserRole.Dealer;
            ViewBag.IsAgent = settings.RoleId == UserRole.Agent;
            ViewBag.SearchTerm = searchTerm.Trim();

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
