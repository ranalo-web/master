using Microsoft.AspNetCore.Mvc;
using Ranalo.Configuration;
using Ranalo.DataStore;
using Ranalo.DataStore.DataModels;
using Ranalo.Models.Reports;
using Ranalo.Models.ViewModels;
using Ranalo.Services;

namespace Ranalo.Controllers
{
    [LoadUserSettingsFromCookie]
    public class CommissionsController : Controller
    {

        private readonly ICommissionsReportsService _commissionsService;
        private readonly IUserService _userService;

        public CommissionsController(ICommissionsReportsService commissionsService, IUserService userService)
        {
            _commissionsService = commissionsService;
            _userService = userService;
        }

        [Route("commissions")]
        public async Task<IActionResult> Index()
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }
            var filter = new CommissionsFilter();

            var fullCommissions = await _commissionsService.FullCommissionsReportAsync(filter);

            var response = new CommissionsMaster()
            {
                FullCommissions = fullCommissions
            };

            await SetViewBags(settings, "index");
            return View(response);
        }

        [Route("commissions-dealer-ready")]
        public async Task<IActionResult> CommissionsReady()
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }
            var filter = new CommissionsFilter();

            var fullCommissions = await _commissionsService.DealerCommissionsReadyToPayAsync(filter);

            var response = new CommissionsMaster()
            {
                DealerReadyToPayCommissions = fullCommissions
            };

            await SetViewBags(settings, "index");
            return View(response);
        }

        [Route("commissions-dealer-outstanding")]
        public async Task<IActionResult> OutstandingDealerCommissions()
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }
            var filter = new CommissionsFilter();

            var fullCommissions = await _commissionsService.OutstandingDealerCommissionsAsync(filter);

            var response = new CommissionsMaster()
            {
                OutstandingDealerCommissions = fullCommissions
            };

            await SetViewBags(settings, "index");
            return View(response);
        }

        private async Task SetViewBags(User settings, string backLink, string searchTerm = "")
        {
            ViewBag.BackLink = backLink;
            ViewBag.IsAdmin = settings.RoleId == UserRole.Admin;
            ViewBag.IsApprover = settings.RoleId == UserRole.Approver;
            ViewBag.IsDealer = settings.RoleId == UserRole.Dealer;
            ViewBag.UserName = settings.KnownAs;
            ViewBag.SearchTerm = searchTerm.Trim();
            if (settings.RoleId == UserRole.Dealer)
            {
                var dealer = await _userService.GetDealerByUserId(settings.UserId);
                ViewBag.UserName = dealer.CompanyName;
                settings.DealerId = dealer.DealerId;
                ViewBag.DealerId = dealer.DealerId;
            }
        }
    }
}
