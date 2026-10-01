using Microsoft.AspNetCore.Mvc;
using Ranalo.Configuration;
using Ranalo.DataStore;
using Ranalo.DataStore.DataModels;
using Ranalo.Models;

namespace Ranalo.Controllers
{
    // Account Assignment (admin): bulk-assign agents and collectors to
    // accounts across every dealer. Agents and collectors themselves are
    // added on Users > Add User. Every change is logged
    // (AccountAssignmentLog, Database/Assignments/001).
    [LoadUserSettingsFromCookie]
    public class AccountAssignmentController : Controller
    {
        private readonly IAccountAssignmentRepository _repository;
        private readonly IDealerAllocationRepository _dealers;
        private readonly ILogger<AccountAssignmentController> _logger;

        public AccountAssignmentController(IAccountAssignmentRepository repository, IDealerAllocationRepository dealers, ILogger<AccountAssignmentController> logger)
        {
            _repository = repository;
            _dealers = dealers;
            _logger = logger;
        }

        [HttpGet]
        [Route("account-assignment")]
        public async Task<IActionResult> Index(int? dealerId = null, string? show = null, string? q = null)
        {
            if (!TryGetAdmin(out var settings, out var redirect))
            {
                return redirect!;
            }

            var mode = show is "all" or "nocollector" ? show : "noagent";
            var model = new AccountAssignmentViewModel
            {
                DealerId = dealerId,
                Show = mode,
                Search = q,
                Accounts = await _repository.GetAccountsAsync(dealerId, mode, q),
                Dealers = await _dealers.GetDealersAsync(),
                Agents = await _repository.GetAgentsAsync(),
                Collectors = await _repository.GetCollectorsAsync(),
            };

            ViewBag.BackLink = "account-assignment";
            ViewBag.IsAdmin = true;
            ViewBag.IsApprover = false;
            ViewBag.IsDealer = false;
            ViewBag.UserName = settings!.KnownAs;
            return View(model);
        }

        // mode: "assign" or "remove"; role: Agent or Collector.
        [HttpPost]
        [Route("account-assignment/assign")]
        public async Task<IActionResult> Assign(List<long> accountNos, string role, string mode, int? userId,
            int? dealerId, string? show, string? q)
        {
            if (!TryGetAdmin(out var settings, out var redirect))
            {
                return redirect!;
            }

            var back = RedirectToAction("Index", new { dealerId, show, q });
            if (role == AssignmentRole.Collector)
            {
                // Collectors are handed accounts on the Collections page, so the
                // collections rules (eligibility, frozen arrears) always apply.
                TempData["AssignmentError"] = "Collectors are now assigned on Collections > Collections Dashboard.";
                return back;
            }
            role = AssignmentRole.Agent;
            var remove = mode == "remove";
            if (accountNos == null || accountNos.Count == 0)
            {
                TempData["AssignmentError"] = "Tick at least one account.";
                return back;
            }
            if (!remove && userId is null or <= 0)
            {
                TempData["AssignmentError"] = $"Choose the {role.ToLowerInvariant()} to assign.";
                return back;
            }

            try
            {
                var ticked = accountNos.Distinct().Count();
                var changed = await _repository.AssignAsync(role, accountNos, remove ? null : userId, settings!.UserId);
                _logger.LogInformation("Account assignment: {Role} {Action} on {Changed} account(s) by user {UserId}", role, remove ? "removed" : $"set to {userId}", changed, settings.UserId);

                var skipped = ticked - changed;
                TempData["AssignmentSuccess"] = remove
                    ? $"Removed the {role.ToLowerInvariant()} from {changed} account(s)."
                    : $"Assigned the {role.ToLowerInvariant()} to {changed} account(s)."
                      + (skipped > 0
                          ? role == AssignmentRole.Agent
                              ? $" {skipped} skipped: already theirs, or belonging to a different dealer (an agent can only take their own dealer's accounts)."
                              : $" {skipped} skipped: already theirs."
                          : "");
            }
            catch (InvalidOperationException ex)
            {
                TempData["AssignmentError"] = ex.Message;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Account assignment failed");
                TempData["AssignmentError"] = "Nothing was changed: the assignment could not be saved. Has Database/Assignments/001_create_account_assignment_log.sql been run?";
            }

            return back;
        }

        private bool TryGetAdmin(out User? settings, out IActionResult? redirect)
        {
            settings = HttpContext.Items["UserSettings"] as User;
            redirect = settings == null
                ? RedirectToAction("Index", "Login")
                : settings.RoleId != UserRole.Admin ? RedirectToAction("Index", "Home") : null;
            return redirect == null;
        }
    }
}
