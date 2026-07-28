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
        private readonly ILogger<CollectionsController> _logger;

        public CollectionsController(IApplicationReportService applicationReportService,
            IUserService userService,
            IContractService contractorService,
            IDeviceProcessor deviceProcessor, ILogger<CollectionsController> logger)
        {
            _applicationReportService = applicationReportService;
            _userService = userService;
            _contractorService = contractorService;
            _deviceProcessor = deviceProcessor;
            _logger = logger;
        }

        [Route("collections-home/{page:int?}")]
        public async Task<IActionResult> Index()
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            await SetViewBags(settings, "collector");

            return View();
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

            var dealer = await _userService.GetDealerByUserId(settings.UserId);

            var dealerId = Convert.ToInt32(dealer.DealerReference);

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

            var dealer = await _userService.GetDealerByUserId(settings.UserId);

            var dealerId = Convert.ToInt32(dealer.DealerReference);

            var allDelaerStatusReport = await _applicationReportService.CallQualifyingFunc(false, true, false, null, dealerId, page, pageSize, searchTerm.Trim());

            return View("AssignedCollections", allDelaerStatusReport);
            return View(allDelaerStatusReport);

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

            await _contractorService.AssignContractToCollector((int)displayDeviceId, debtCollectorUserId);

            return RedirectToAction("Collections", "Reports");
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
