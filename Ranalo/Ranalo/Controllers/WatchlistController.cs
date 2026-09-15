using Microsoft.AspNetCore.Mvc;
using Ranalo.Configuration;
using Ranalo.DataStore.DataModels;
using Ranalo.Models;
using Ranalo.Services;

namespace Ranalo.Controllers
{
    // Manual account watchlist -- a user-flagged list, not a computed
    // classification. See Database/Watchlist/001_create_account_watchlist.sql
    // and AccountWatchlistService for the add/remove permission rule.
    [LoadUserSettingsFromCookie]
    public class WatchlistController : Controller
    {
        private readonly IAccountWatchlistService _watchlistService;
        private readonly IApplicationReportService _applicationReportService;

        public WatchlistController(IAccountWatchlistService watchlistService, IApplicationReportService applicationReportService)
        {
            _watchlistService = watchlistService;
            _applicationReportService = applicationReportService;
        }

        [HttpGet]
        [Route("watchlist")]
        public async Task<IActionResult> Index(string searchTerm = "", int page = 1, int pageSize = 10)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            if (!CanUseWatchlist(settings.RoleId))
            {
                return RedirectToAction("Index", "Home");
            }

            ViewBag.BackLink = "watchlist";
            ViewBag.IsAdmin = settings.RoleId == UserRole.Admin;
            ViewBag.IsApprover = false;
            ViewBag.IsDealer = settings.RoleId == UserRole.Dealer;
            ViewBag.UserName = settings.KnownAs;

            var model = new WatchlistViewModel
            {
                Watchlist = await _watchlistService.GetWatchlistForViewerAsync(settings),
                SearchTerm = searchTerm,
            };

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                int? dealerId = settings.RoleId == UserRole.Admin ? null : settings.DealerId;
                var searchResults = await _applicationReportService.GetAllAccountsAsync(dealerId, searchTerm, page, pageSize);
                model.SearchResults = searchResults.Accounts ?? new List<AllAccounts>();
            }

            return View(model);
        }

        [HttpPost]
        [Route("watchlist/add")]
        public async Task<IActionResult> Add(long accountId, string searchTerm = "")
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            if (!CanUseWatchlist(settings.RoleId))
            {
                return RedirectToAction("Index", "Home");
            }

            await _watchlistService.AddToWatchlistAsync(accountId, settings);

            return RedirectToAction("Index", new { searchTerm });
        }

        [HttpPost]
        [Route("watchlist/remove")]
        public async Task<IActionResult> Remove(int watchlistId)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            if (!CanUseWatchlist(settings.RoleId))
            {
                return RedirectToAction("Index", "Home");
            }

            await _watchlistService.RemoveFromWatchlistAsync(watchlistId, settings);

            return RedirectToAction("Index");
        }

        private static bool CanUseWatchlist(UserRole role) =>
            role is UserRole.Admin or UserRole.Dealer or UserRole.Agent;
    }
}
