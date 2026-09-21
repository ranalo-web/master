using Microsoft.AspNetCore.Mvc;
using Ranalo.Configuration;
using Ranalo.DataStore.DataModels;
using Ranalo.Models;
using Ranalo.Services;

namespace Ranalo.Controllers
{
    // Dedicated report pages linked from the Dealer Dashboard's summary
    // cards (Views/DealerDashboard/Index.cshtml) -- one route per section,
    // each rendering the full list behind that card's headline number.
    [LoadUserSettingsFromCookie]
    [Route("dealer-dashboard/reports")]
    public class DealerDashboardReportsController : Controller
    {
        private readonly IDashboardReportService _dashboardReportService;

        public DealerDashboardReportsController(IDashboardReportService dashboardReportService)
        {
            _dashboardReportService = dashboardReportService;
        }

        private User? RequireDealerOrAdmin(out IActionResult? redirect)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                redirect = RedirectToAction("Index", "Login");
                return null;
            }

            if (settings.RoleId != UserRole.Dealer && settings.RoleId != UserRole.Admin && settings.RoleId != UserRole.Agent)
            {
                redirect = RedirectToAction("Index", "Home");
                return null;
            }

            // _Layout.cshtml reads these on every page (sidebar role gates,
            // topbar name) -- DealerDashboardController.Index sets the same
            // four for the dashboard itself.
            ViewBag.BackLink = settings.RoleId == UserRole.Agent ? "agent" : "dealer-dashboard";
            ViewBag.IsAdmin = settings.RoleId == UserRole.Admin;
            ViewBag.IsApprover = false;
            ViewBag.IsDealer = settings.RoleId == UserRole.Dealer;
            ViewBag.IsAgent = settings.RoleId == UserRole.Agent;
            ViewBag.UserName = settings.KnownAs;

            redirect = null;
            return settings;
        }

        // An Agent only ever sees their own book (AssignedAgentId), not the
        // whole dealer's -- see GetDealerDashboardAsync's agentUserId doc note.
        private static int? AgentUserId(User settings) => settings.RoleId == UserRole.Agent ? settings.UserId : (int?)null;

        [HttpGet]
        [Route("non-payers")]
        public async Task<IActionResult> NonPayers()
        {
            var settings = RequireDealerOrAdmin(out var redirect);
            if (settings == null) return redirect!;

            var rows = await _dashboardReportService.GetDealerNonPayersAsync(settings.DealerId, AgentUserId(settings));
            return View(rows);
        }

        [HttpGet]
        [Route("slow-payers")]
        public async Task<IActionResult> SlowPayers()
        {
            var settings = RequireDealerOrAdmin(out var redirect);
            if (settings == null) return redirect!;

            var rows = await _dashboardReportService.GetDealerSlowPayersAsync(settings.DealerId, AgentUserId(settings));
            return View(rows);
        }

        [HttpGet]
        [Route("good-payers")]
        public async Task<IActionResult> GoodPayers()
        {
            var settings = RequireDealerOrAdmin(out var redirect);
            if (settings == null) return redirect!;

            var rows = await _dashboardReportService.GetDealerGoodPayersAsync(settings.DealerId, AgentUserId(settings));
            return View(rows);
        }

        [HttpGet]
        [Route("agent-performance")]
        public async Task<IActionResult> AgentPerformance()
        {
            var settings = RequireDealerOrAdmin(out var redirect);
            if (settings == null) return redirect!;

            var rows = await _dashboardReportService.GetDealerAgentPerformanceAsync(settings.DealerId, AgentUserId(settings));
            return View(rows);
        }

        [HttpGet]
        [Route("contracts")]
        public async Task<IActionResult> Contracts()
        {
            var settings = RequireDealerOrAdmin(out var redirect);
            if (settings == null) return redirect!;

            var rows = await _dashboardReportService.GetDealerContractsAsync(settings.DealerId, AgentUserId(settings));
            return View(rows);
        }

        [HttpGet]
        [Route("contracts-ending-soon")]
        public async Task<IActionResult> ContractsEndingSoon()
        {
            var settings = RequireDealerOrAdmin(out var redirect);
            if (settings == null) return redirect!;

            var rows = await _dashboardReportService.GetDealerContractsEndingSoonAsync(settings.DealerId, AgentUserId(settings));
            return View(rows);
        }

        [HttpGet]
        [Route("device-performance")]
        public async Task<IActionResult> DevicePerformance()
        {
            var settings = RequireDealerOrAdmin(out var redirect);
            if (settings == null) return redirect!;

            var rows = await _dashboardReportService.GetDealerDeviceStockReportAsync(settings.DealerId);
            return View(rows);
        }

        [HttpGet]
        [Route("completed-contracts")]
        public async Task<IActionResult> CompletedContracts()
        {
            var settings = RequireDealerOrAdmin(out var redirect);
            if (settings == null) return redirect!;

            var rows = await _dashboardReportService.GetDealerCompletedContractsReportAsync(settings.DealerId);
            return View(rows);
        }
    }
}
