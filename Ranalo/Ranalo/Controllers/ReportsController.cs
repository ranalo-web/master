using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Mvc;
using Ranalo.Configuration;
using Ranalo.DataStore.DataModels;
using Ranalo.Models;
using Ranalo.Services;
using Ranalo.UiHelpers;
using System.Drawing.Printing;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Ranalo.Controllers
{
    [LoadUserSettingsFromCookie]
    public class ReportsController : Controller
    {
        private readonly IApplicationReportService _applicationReportService;
        private readonly IUserService _userService;

        public ReportsController(IApplicationReportService applicationReportService, IUserService userService)
        {
            _applicationReportService = applicationReportService;
            _userService = userService;
        }

        [HttpGet]
        [Route("statusreport/{page:int?}")]
        public async Task<IActionResult> Index(int page = 1, int pageSize = 10, string searchTerm = "")
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            await SetViewBags(settings, "approver");

            if (settings.RoleId == UserRole.Admin || settings.RoleId == UserRole.Approver)
            {
                var paymentSummaries = await _applicationReportService.GetStatusReportByDealer(null, null, page, pageSize, searchTerm);

                ViewData["OrdersStatus"] = "Waiting Approval";

                return View(paymentSummaries);
            }

            var dealer = await _userService.GetDealerByUserId(settings.UserId);

            var dealerId = Convert.ToInt32(dealer.DealerReference); 

            var delaerStatusReport = await _applicationReportService.GetStatusReportByDealer(null, dealerId, page, pageSize, searchTerm);

            return View(delaerStatusReport);

        }

        [HttpGet]
        [Route("arrearsreport/{page:int?}")]
        public async Task<IActionResult> ArreasReport(int page = 1, int pageSize = 10)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            await SetViewBags(settings, "approver");

            if (settings.RoleId == UserRole.Admin || settings.RoleId == UserRole.Approver)
            {
                //var allPaymentSummaries = await _applicationReportService.GetStatusReportByDealer(null,null);

                var allPaymentSummaries = await _applicationReportService.CallQualifyingFunc(null, null, page, pageSize, "");


                ViewData["OrdersStatus"] = "Waiting Approval";

                return View(allPaymentSummaries);
            }

            var dealer = await _userService.GetDealerByUserId(settings.UserId);

            var dealerId = Convert.ToInt32(dealer.DealerReference);

            var allDelaerStatusReport = await _applicationReportService.CallQualifyingFunc(null, dealerId, page, pageSize, "");

            return View(allDelaerStatusReport);

        }

        [HttpGet]
        [Route("missingmpesacode/{page:int?}")]
        public async Task<IActionResult> MissingMpesa(int page = 1, int pageSize = 10)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }
            await SetViewBags(settings, "index");

            if (settings.RoleId == UserRole.Admin || settings.RoleId == UserRole.Approver)
            {
                var allAwaitngApproval = await _applicationReportService.GetMissingMpesaOrders(page: page, pageSize: pageSize);
                ViewData["OrdersStatus"] = "Waiting Approval";

                return View("~/Views/Reports/MissingMpesa.cshtml", allAwaitngApproval);
            }

            return RedirectToAction("Index", "Home");

        }

        [HttpGet]
        [Route("statussummary/{accountId:int}")]
        public async Task<IActionResult> StatusSummary(int accountId)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            var viewDetails = new MobileStatusReport();

            await SetViewBags(settings, "approver"); 

            if (settings.RoleId == UserRole.Admin || settings.RoleId == UserRole.Approver)
            {
                var paymentSummaries = await _applicationReportService.GetStatusReportByDealer(accountId, null);

                viewDetails = paymentSummaries?.StatusReports?.First();
                ViewData["OrdersStatus"] = "Waiting Approval";
            }
            else
            {
                var dealer = await _userService.GetDealerByUserId(settings.UserId);

                var dealerId = Convert.ToInt32(dealer.DealerReference);
                var delaerStatusReport = await _applicationReportService.GetStatusReportByDealer(accountId, dealerId);

                viewDetails = delaerStatusReport?.StatusReports?.First();

            }

            var restructured = await _applicationReportService.GetAllRestructuredForAccount(accountId);
            if(restructured != null)
            {
                viewDetails.RestructuredRecords.AddRange(restructured);
            }

            viewDetails.CustomerDetails = await _applicationReportService.GetCustomerDetailsByAccountIdAsync(accountId);


            return View(viewDetails);
        }

        [HttpPost]
        [Route("restructure-payment")]
        public async Task<IActionResult> StatusSummary(int accountNo, decimal newRate, DateTime dateFrom)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            DateTime date24HoursFromNow = DateTime.UtcNow.AddHours(24);

            var restructureToSave = new RestructuredRecord()
            {
                AccountNo = accountNo,
                AmountRes = newRate,
                DateAgreed = dateFrom,
                DaysRestructured = 0, // dateFrom to today 
                ArrearsR = 0, //TotalDueR - TotalPaidR
                TotalDueR = 0,  //AmountRes * SystemDate - DateAgreed
                TotalPaidR = 0, //Payments after agreedDate
                AutoLockDatePmtR = date24HoursFromNow //(ArrearsR / AmountRes) + SystemDate + 27hrs
            };

            try
            {
                await _applicationReportService.CreateRestructuredAsync(restructureToSave);
            }
            catch (Exception ex)
            {

                throw;
            }

            var viewDetails = new MobileStatusReport();

            await SetViewBags(settings, "approver");

            if (settings.RoleId == UserRole.Admin || settings.RoleId == UserRole.Approver)
            {
                var paymentSummaries = await _applicationReportService.GetStatusReportByDealer(accountNo, null);

                viewDetails = paymentSummaries?.StatusReports?.FirstOrDefault(x => x.AccountNo == accountNo);
                ViewData["OrdersStatus"] = "Waiting Approval";
            }
            else
            {
                var dealer = await _userService.GetDealerByUserId(settings.UserId);

                var dealerId = Convert.ToInt32(dealer.DealerReference);
                var delaerStatusReport = await _applicationReportService.GetStatusReportByDealer(accountNo, dealerId);

                viewDetails = delaerStatusReport?.StatusReports?.FirstOrDefault(x => x.AccountNo == accountNo);

            }

            return View(viewDetails);
        }

        [HttpGet]
        [Route("restructured/{page:int?}")]
        public async Task<IActionResult> RestructuredReport(int page = 1, int pageSize = 10)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            await SetViewBags(settings, "approver");

            if (settings.RoleId == UserRole.Admin || settings.RoleId == UserRole.Approver)
            {
                var allPaymentSummaries = await _applicationReportService.CallQualifyingFunc(null, null, page, pageSize, "");

                ViewData["OrdersStatus"] = "Waiting Approval";

                return View(allPaymentSummaries);
            }

            var dealer = await _userService.GetDealerByUserId(settings.UserId);

            var dealerId = Convert.ToInt32(dealer.DealerReference);

            var allDelaerStatusReport = await _applicationReportService.CallQualifyingFunc(null, dealerId, page, pageSize, "");

            return View(allDelaerStatusReport);
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
                settings.DealerId = dealer.DealerId;
            }
        }
    }
}
