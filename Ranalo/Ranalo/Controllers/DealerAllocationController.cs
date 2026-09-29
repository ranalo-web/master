using Microsoft.AspNetCore.Mvc;
using Ranalo.Configuration;
using Ranalo.DataStore;
using Ranalo.DataStore.DataModels;
using Ranalo.Models;

namespace Ranalo.Controllers
{
    // Dealer Allocation (admin): give accounts with no dealer a dealer, and
    // move accounts from one dealer to another. Every change is logged
    // (DealerAllocationLog, Database/Dealers/001).
    [LoadUserSettingsFromCookie]
    public class DealerAllocationController : Controller
    {
        private readonly IDealerAllocationRepository _repository;
        private readonly ILogger<DealerAllocationController> _logger;

        public DealerAllocationController(IDealerAllocationRepository repository, ILogger<DealerAllocationController> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        [HttpGet]
        [Route("dealer-allocation")]
        public async Task<IActionResult> Index(string? show = null, string? q = null)
        {
            if (!TryGetAdmin(out var settings, out var redirect))
            {
                return redirect!;
            }

            var unassignedOnly = show != "all";
            var (unassigned, noDevice) = await _repository.GetUnassignedCountsAsync();
            var model = new DealerAllocationViewModel
            {
                Show = unassignedOnly ? "unassigned" : "all",
                Search = q,
                UnassignedCount = unassigned,
                NoDeviceCount = noDevice,
                Accounts = await _repository.GetAccountsAsync(unassignedOnly, q),
                Dealers = await _repository.GetDealersAsync(),
            };

            try
            {
                model.RecentChanges = await _repository.GetRecentChangesAsync(20);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dealer allocation log unavailable -- has Database/Dealers/001 been run?");
                model.LogUnavailable = true;
            }

            SetViewBags(settings!);
            return View(model);
        }

        [HttpPost]
        [Route("dealer-allocation/assign")]
        public async Task<IActionResult> Assign(List<long> accountNos, int dealerId, string? note, string? show, string? q)
        {
            if (!TryGetAdmin(out var settings, out var redirect))
            {
                return redirect!;
            }

            if (accountNos == null || accountNos.Count == 0 || dealerId <= 0)
            {
                TempData["AllocationError"] = "Tick at least one account and choose a dealer.";
                return RedirectToAction("Index", new { show, q });
            }

            try
            {
                var trimmed = string.IsNullOrWhiteSpace(note) ? null : note.Trim()[..Math.Min(note.Trim().Length, 300)];
                var moved = await _repository.AssignAsync(accountNos, dealerId, settings!.UserId, trimmed);
                var dealer = (await _repository.GetDealersAsync()).FirstOrDefault(d => d.DealerId == dealerId)?.CompanyName ?? $"dealer {dealerId}";
                var skipped = accountNos.Distinct().Count() - moved;
                _logger.LogInformation("Dealer allocation: {Moved} account(s) moved to dealer {DealerId} by user {UserId}", moved, dealerId, settings.UserId);
                TempData["AllocationSuccess"] = $"Moved {moved} account(s) to {dealer}." +
                    (skipped > 0 ? $" {skipped} skipped (already with {dealer}, or no device record)." : "");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dealer allocation failed for dealer {DealerId}", dealerId);
                TempData["AllocationError"] = "Nothing was changed: the move could not be saved. Has Database/Dealers/001_create_dealer_allocation_log.sql been run?";
            }

            return RedirectToAction("Index", new { show, q });
        }

        private bool TryGetAdmin(out User? settings, out IActionResult? redirect)
        {
            settings = HttpContext.Items["UserSettings"] as User;
            redirect = settings == null
                ? RedirectToAction("Index", "Login")
                : settings.RoleId != UserRole.Admin ? RedirectToAction("Index", "Home") : null;
            return redirect == null;
        }

        private void SetViewBags(User settings)
        {
            ViewBag.BackLink = "dealer-allocation";
            ViewBag.IsAdmin = true;
            ViewBag.IsApprover = false;
            ViewBag.IsDealer = false;
            ViewBag.UserName = settings.KnownAs;
        }
    }
}
