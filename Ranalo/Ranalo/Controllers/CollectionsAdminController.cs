using Microsoft.AspNetCore.Mvc;
using Ranalo.Configuration;
using Ranalo.DataStore.DataModels;
using Ranalo.Services;

namespace Ranalo.Controllers
{
    // Collections (admin): hand pool accounts to collectors, reassign or
    // return them, and see each collector's recoveries, earnings and flags.
    // Rules: Services.CollectionsRules.
    [LoadUserSettingsFromCookie]
    public class CollectionsAdminController : Controller
    {
        private readonly ICollectionsService _collections;
        private readonly ILogger<CollectionsAdminController> _logger;

        public CollectionsAdminController(ICollectionsService collections, ILogger<CollectionsAdminController> logger)
        {
            _collections = collections;
            _logger = logger;
        }

        [HttpGet]
        [Route("collections-admin")]
        public async Task<IActionResult> Index(string? tab = null, string? q = null, int? dealerId = null, int? collectorId = null)
        {
            if (!TryGetAdmin(out var settings, out var redirect))
            {
                return redirect!;
            }

            SetViewBags(settings!);
            return View(await _collections.GetAdminAsync(tab, q, dealerId, collectorId));
        }

        [HttpPost]
        [Route("collections-admin/assign")]
        public async Task<IActionResult> Assign(List<long> accountNos, int collectorUserId, string? q, int? dealerId)
        {
            if (!TryGetAdmin(out var settings, out var redirect))
            {
                return redirect!;
            }

            return await Run(() => _collections.AssignAsync(accountNos ?? new List<long>(), collectorUserId, settings!.UserId),
                new { tab = "pool", q, dealerId });
        }

        [HttpPost]
        [Route("collections-admin/reassign")]
        public async Task<IActionResult> Reassign(List<int> caseIds, int collectorUserId, string? q, int? dealerId, int? collectorId)
        {
            if (!TryGetAdmin(out var settings, out var redirect))
            {
                return redirect!;
            }

            return await Run(() => _collections.ReassignAsync(caseIds ?? new List<int>(), collectorUserId, settings!.UserId),
                new { tab = "cases", q, dealerId, collectorId });
        }

        [HttpPost]
        [Route("collections-admin/return")]
        public async Task<IActionResult> Return(List<int> caseIds, string? q, int? dealerId, int? collectorId)
        {
            if (!TryGetAdmin(out var settings, out var redirect))
            {
                return redirect!;
            }

            return await Run(() => _collections.ReturnAsync(caseIds ?? new List<int>(), settings!.UserId),
                new { tab = "cases", q, dealerId, collectorId });
        }

        [HttpPost]
        [Route("collections-admin/flags/{id:int}/resolve")]
        public async Task<IActionResult> ResolveFlag(int id)
        {
            if (!TryGetAdmin(out var settings, out var redirect))
            {
                return redirect!;
            }

            var ok = await _collections.ResolveFlagAsync(id, settings!.UserId);
            TempData[ok ? "CollectionsSuccess" : "CollectionsError"] = ok ? "Flag cleared." : "That flag was already cleared.";
            return RedirectToAction("Index", new { tab = "flags" });
        }

        private async Task<IActionResult> Run(Func<Task<(bool Ok, string Message)>> action, object routeValues)
        {
            try
            {
                var (ok, message) = await action();
                TempData[ok ? "CollectionsSuccess" : "CollectionsError"] = message;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Collections change failed");
                TempData["CollectionsError"] = "Nothing was changed: it could not be saved. Has Database/Collections/001_create_collections_tables.sql been run?";
            }
            return RedirectToAction("Index", routeValues);
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
            ViewBag.BackLink = "collections-admin";
            ViewBag.IsAdmin = true;
            ViewBag.IsApprover = false;
            ViewBag.IsDealer = false;
            ViewBag.IsAgent = false;
            ViewBag.UserName = settings.KnownAs;
        }
    }
}
