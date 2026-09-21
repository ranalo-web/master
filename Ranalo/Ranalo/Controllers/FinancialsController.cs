using Microsoft.AspNetCore.Mvc;
using Ranalo.Configuration;
using Ranalo.DataStore.DataModels;
using Ranalo.Models;
using Ranalo.Services;

namespace Ranalo.Controllers
{
    // P&L, monthly comparison chart, and best-effort balance sheet -- moved
    // off the Admin Dashboard onto its own page. Admin-only, same
    // sensitivity as Operating Expenses.
    [LoadUserSettingsFromCookie]
    public class FinancialsController : Controller
    {
        private readonly IDashboardReportService _dashboardReportService;

        public FinancialsController(IDashboardReportService dashboardReportService)
        {
            _dashboardReportService = dashboardReportService;
        }

        [HttpGet]
        [Route("financials")]
        public async Task<IActionResult> Index()
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            if (settings.RoleId != UserRole.Admin)
            {
                return RedirectToAction("Index", "Home");
            }

            ViewBag.BackLink = "financials";
            ViewBag.IsAdmin = true;
            ViewBag.IsApprover = false;
            ViewBag.IsDealer = false;
            ViewBag.UserName = settings.KnownAs;

            var model = await _dashboardReportService.GetFinancialsAsync();

            return View(model);
        }
    }
}
