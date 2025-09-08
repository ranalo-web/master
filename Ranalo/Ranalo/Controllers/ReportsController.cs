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
                var paymentSummaries = await _applicationReportService.GetStatusReportByDealer(null);
                var pagedAllData = paymentSummaries.Paginate(page, pageSize);
                var responseData = new StatusReportViewModel()
                {
                    CurrentPage = page,
                    StatusReports = pagedAllData.ToList(),
                    TotalPages = (int)Math.Ceiling((double)paymentSummaries.Count() / pageSize)
                };

                var user = await _userService.GetUserByCustomerIdAsync(settings.UserId);
                ViewData["OrdersStatus"] = "Waiting Approval";

                return View(responseData);
            }

            var dealer = await _userService.GetDealerByUserId(settings.UserId);

            var dealerId = Convert.ToInt32(dealer.DealerReference); 

            var delaerStatusReport = await _applicationReportService.GetStatusReportByDealer(dealerId);

            var pagedData = delaerStatusReport.Paginate(page, pageSize);

            var veiwDetails = new StatusReportViewModel()
            {
                CurrentPage = page,
                StatusReports = pagedData.ToList(),
                TotalPages = (int)Math.Ceiling((double)delaerStatusReport.Count() / pageSize)
            };

            return View(veiwDetails);

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
                var allPaymentSummaries = await _applicationReportService.GetStatusReportByDealer(null);
                var paymentSummaries = allPaymentSummaries.Where(x=>x.Arrears < 0).ToList();
                var pagedAllData = paymentSummaries.Paginate(page, pageSize);
                var responseData = new StatusReportViewModel()
                {
                    CurrentPage = page,
                    StatusReports = pagedAllData.ToList(),
                    TotalPages = (int)Math.Ceiling((double)paymentSummaries.Count() / pageSize)
                };

                var user = await _userService.GetUserByCustomerIdAsync(settings.UserId);
                ViewData["OrdersStatus"] = "Waiting Approval";

                return View(responseData);
            }

            var dealer = await _userService.GetDealerByUserId(settings.UserId);

            var dealerId = Convert.ToInt32(dealer.DealerReference);

            var allDelaerStatusReport = await _applicationReportService.GetStatusReportByDealer(dealerId);
            var delaerStatusReport = allDelaerStatusReport.Where(x => x.Arrears < 0).ToList();
            var pagedData = delaerStatusReport.Paginate(page, pageSize);

            var veiwDetails = new StatusReportViewModel()
            {
                CurrentPage = page,
                StatusReports = pagedData.ToList(),
                TotalPages = (int)Math.Ceiling((double)delaerStatusReport.Count() / pageSize)
            };

            return View(veiwDetails);

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
                var paymentSummaries = await _applicationReportService.GetStatusReportByDealer(null);

                viewDetails = paymentSummaries.FirstOrDefault(x=>x.AccountNo == accountId);

                var user = await _userService.GetUserByCustomerIdAsync(settings.UserId);
                ViewData["OrdersStatus"] = "Waiting Approval";
            }
            else
            {
                var dealer = await _userService.GetDealerByUserId(settings.UserId);

                var dealerId = Convert.ToInt32(dealer.DealerReference);
                var delaerStatusReport = await _applicationReportService.GetStatusReportByDealer(dealerId);

                viewDetails = delaerStatusReport.FirstOrDefault(x => x.AccountNo == accountId);

            }

            return View(viewDetails);
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
