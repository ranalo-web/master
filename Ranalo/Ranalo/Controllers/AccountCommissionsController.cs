using Microsoft.AspNetCore.Mvc;
using Ranalo.Configuration;
using Ranalo.DataStore.DataModels;
using Ranalo.Services;

namespace Ranalo.Controllers
{
    // Sidebar > Commissions > Account Commissions: each account's agent and
    // dealer commission with the calculation broken out. Admins see every
    // dealer, dealers see their own accounts, agents see only accounts
    // assigned to them and only the agent side.
    [LoadUserSettingsFromCookie]
    public class AccountCommissionsController : Controller
    {
        private readonly IDashboardReportService _dashboardReportService;

        public AccountCommissionsController(IDashboardReportService dashboardReportService)
        {
            _dashboardReportService = dashboardReportService;
        }

        [HttpGet]
        [Route("account-commissions")]
        public async Task<IActionResult> Index()
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            var role = settings.RoleId;
            if (role is not (UserRole.Admin or UserRole.Dealer or UserRole.Agent))
            {
                return RedirectToAction("Index", "Home");
            }

            ViewBag.BackLink = role == UserRole.Agent ? "agent" : role == UserRole.Admin ? "admin-dashboard" : "dealer-dashboard";
            ViewBag.IsAdmin = role == UserRole.Admin;
            ViewBag.IsApprover = false;
            ViewBag.IsDealer = role == UserRole.Dealer;
            ViewBag.IsAgent = role == UserRole.Agent;
            ViewBag.UserName = settings.KnownAs;

            int? dealerId = role == UserRole.Admin ? null : settings.DealerId;
            int? agentUserId = role == UserRole.Agent ? settings.UserId : null;

            var model = await _dashboardReportService.GetAccountCommissionsPageAsync(dealerId, agentUserId, showDealer: role == UserRole.Admin);
            return View(model);
        }
    }
}
