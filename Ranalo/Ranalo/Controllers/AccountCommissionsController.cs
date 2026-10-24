using Microsoft.AspNetCore.Mvc;
using Ranalo.Configuration;
using Ranalo.DataStore;
using Ranalo.DataStore.DataModels;
using Ranalo.Models;
using Ranalo.Services;

namespace Ranalo.Controllers
{
    // Sidebar > Commissions > Account Commissions: each account's agent and
    // dealer commission with the calculation broken out. Admins see every
    // dealer, dealers see their own accounts, agents see only accounts
    // assigned to them and only the agent side. Admins can also mark an
    // account as earning no commission (legacy etc.) or recovering arrears.
    [LoadUserSettingsFromCookie]
    public class AccountCommissionsController : Controller
    {
        private readonly IDashboardReportService _dashboardReportService;
        private readonly ICommissionExclusionRepository _exclusions;
        private readonly ILogger<AccountCommissionsController> _logger;

        public AccountCommissionsController(IDashboardReportService dashboardReportService, ICommissionExclusionRepository exclusions,
            ILogger<AccountCommissionsController> logger)
        {
            _dashboardReportService = dashboardReportService;
            _exclusions = exclusions;
            _logger = logger;
        }

        [HttpGet]
        [Route("account-commissions")]
        public async Task<IActionResult> Index(string? status = null, string? q = null)
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
            model.CanEditStatus = CommissionExclusionRules.CanChange(role);
            model.TotalAccounts = model.Rows.Count;
            if (model.CanEditStatus)
            {
                // Filter and search only narrow the table; the cards stay whole.
                model.StatusFilter = CommissionExclusionRules.Normalize(status);
                model.Search = string.IsNullOrWhiteSpace(q) ? null : q.Trim();
                model.Rows = model.Rows
                    .Where(r => CommissionExclusionRules.Matches(r.Commission, model.StatusFilter))
                    .Where(r => model.Search == null
                        || r.AccountId.ToString() == model.Search || r.ContractId == model.Search
                        || Contains(r.CustomerName, model.Search) || Contains(r.DealerName, model.Search) || Contains(r.AgentName, model.Search))
                    .ToList();
            }
            if (TempData["CommissionMessages"] is string[] messages) model.Messages.AddRange(messages);
            if (TempData["CommissionErrors"] is string[] errors) model.Errors.AddRange(errors);
            return View(model);
        }

        // Admin: set what an account earns -- Qualifies, NoCommission or
        // Recover (no commission, arrears deducted from the dealer). Logged.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("account-commissions/status")]
        public async Task<IActionResult> SetStatus(long contractId, long accountId, string? status, string? reason, string? notes,
            string? filter = null, string? q = null)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            var error = CommissionExclusionRules.Check(settings.RoleId, contractId, status, reason, notes);
            if (error != null)
            {
                TempData["CommissionErrors"] = new[] { $"Account {accountId}: {error}" };
                return RedirectToAction("Index", new { status = filter, q });
            }

            var newStatus = CommissionExclusionRules.Normalize(status)!;
            try
            {
                var changed = await _exclusions.SetStatusAsync(contractId, newStatus,
                    CommissionExclusionReason.Normalize(reason), notes!.Trim(), settings.UserId);
                TempData["CommissionMessages"] = new[]
                {
                    changed ? $"Account {accountId}: now {CommissionStatus.Label(newStatus).ToLowerInvariant()}."
                            : $"Account {accountId} was already {CommissionStatus.Label(newStatus).ToLowerInvariant()}.",
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Setting commission status {Status} on contract {ContractId} failed", newStatus, contractId);
                TempData["CommissionErrors"] = new[] { $"Account {accountId}: couldn't save. Has Database/Commissions/005 been run?" };
            }
            return RedirectToAction("Index", new { status = filter, q });
        }

        private static bool Contains(string? value, string term) =>
            value != null && value.Contains(term, StringComparison.OrdinalIgnoreCase);
    }
}
