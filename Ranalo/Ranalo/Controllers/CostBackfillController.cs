using Microsoft.AspNetCore.Mvc;
using Ranalo.Configuration;
using Ranalo.DataStore.DataModels;
using Ranalo.Models;
using Ranalo.Services;

namespace Ranalo.Controllers
{
    // One-off device-cost backfill: fills Contract_Info.BuyingPrice for
    // contracts that never had one, from suggestions the admin reviews
    // first. Admin-only, same as the rest of Financials.
    [LoadUserSettingsFromCookie]
    public class CostBackfillController : Controller
    {
        private readonly ICostBackfillService _backfillService;
        private readonly ILogger<CostBackfillController> _logger;

        public CostBackfillController(ICostBackfillService backfillService, ILogger<CostBackfillController> logger)
        {
            _backfillService = backfillService;
            _logger = logger;
        }

        [HttpGet]
        [Route("cost-backfill")]
        public async Task<IActionResult> Index(string rule = "woo")
        {
            if (!IsAdmin(out var redirect))
            {
                return redirect!;
            }

            SetLayoutViewBags();
            var model = await _backfillService.BuildAsync(rule);
            return View(model);
        }

        // Up to ~600 rows x 3 fields posts well past the default 1,024
        // form-value limit.
        [HttpPost]
        [Route("cost-backfill/apply")]
        [RequestFormLimits(ValueCountLimit = 20000)]
        public async Task<IActionResult> Apply(List<CostBackfillApplyRow> rows, string rule = "woo")
        {
            if (!IsAdmin(out var redirect))
            {
                return redirect!;
            }
            var settings = (User)HttpContext.Items["UserSettings"]!;

            var ticked = rows.Count(r => r.Include);
            var invalid = rows.Count(r => r.Include && !(r.BuyingPrice > 0));
            var updated = await _backfillService.ApplyAsync(rows);

            _logger.LogInformation("Cost backfill: user {UserId} set BuyingPrice on {Updated} of {Ticked} ticked contracts", settings.UserId, updated, ticked);

            TempData["BackfillResult"] = $"Saved a buying price on {updated:N0} contract(s)." +
                (invalid > 0 ? $" {invalid:N0} ticked row(s) had no cost and were skipped." : "") +
                (ticked - invalid - updated > 0 ? $" {ticked - invalid - updated:N0} already had a cost by the time you saved and were left unchanged." : "");

            return RedirectToAction("Index", new { rule });
        }

        private bool IsAdmin(out IActionResult? redirect)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            redirect = settings == null
                ? RedirectToAction("Index", "Login")
                : settings.RoleId != UserRole.Admin ? RedirectToAction("Index", "Home") : null;
            return redirect == null;
        }

        private void SetLayoutViewBags()
        {
            var settings = (User)HttpContext.Items["UserSettings"]!;
            ViewBag.BackLink = "cost-backfill";
            ViewBag.IsAdmin = true;
            ViewBag.IsApprover = false;
            ViewBag.IsDealer = false;
            ViewBag.UserName = settings.KnownAs;
        }
    }
}
