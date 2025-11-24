using Microsoft.AspNetCore.Mvc;
using Ranalo.Calculator.Logic.Models;
using Ranalo.Configuration;
using Ranalo.DataStore.DataModels;
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

        public CollectionsController(IApplicationReportService applicationReportService,
            IUserService userService,
            IContractService contractorService)
        {
            _applicationReportService = applicationReportService;
            _userService = userService;
            _contractorService = contractorService;
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

            if (settings.RoleId == UserRole.Admin || settings.RoleId == UserRole.Collector)
            {
                //var allPaymentSummaries = await _applicationReportService.GetStatusReportByDealer(null,null);

                var allPaymentSummaries = await _contractorService.GetCollectorsContractSummaryAsync(settings.UserId, null, 0, page, pageSize, searchTerm.Trim());

                ViewData["OrdersStatus"] = "Waiting Approval";

                return View(allPaymentSummaries);
            }

            var dealer = await _userService.GetDealerByUserId(settings.UserId);

            var dealerId = Convert.ToInt32(dealer.DealerReference);

            var allDelaerStatusReport = await _applicationReportService.CallQualifyingFunc(false, true, null, dealerId, page, pageSize, searchTerm.Trim());

            return View(allDelaerStatusReport);

        }

        [HttpPost]
        [Route("recover-contract")]
        public async Task<IActionResult> RecoverAccount(long displayDeviceId,
            string deposit,
            string oldName,
            string newName,
            string startDate,
            string interval,
            decimal totalCost)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            await SetViewBags(settings, "collector");

            var contractToUpdate = new ContractCreateDto()
            {
                AccountNo = displayDeviceId.ToString(),
                FirstName = newName,
                TotalAmount = totalCost,
            };

            var update = await _contractorService.CreateRecoveredAccountAsync(contractToUpdate);

            return RedirectToAction("NewCollections", "Contract");
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
            var customerId = customerDetails.OrderID == 0 ? long.Parse(customerDetails.Payments.FirstOrDefault().AccountNo) : customerDetails.OrderID;
            var notes = await _applicationReportService.GetNotesByOrderIdAsync(customerId);
            customerDetails.AccountId = (int)accountId;
            customerDetails.CustomerId = customerId.ToString();
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
