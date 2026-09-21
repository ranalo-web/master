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
            ViewBag.IsApprover = settings.RoleId == UserRole.Approver;
            ViewBag.IsDealer = settings.RoleId == UserRole.Dealer;
            ViewBag.UserName = settings.KnownAs;

            var model = new WatchlistViewModel
            {
                Watchlist = await _watchlistService.GetWatchlistForViewerAsync(settings),
                SearchTerm = searchTerm,
            };

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                // Admin and Approver both search across every dealer -- an
                // Approver isn't tied to any one dealer, same as Admin.
                int? dealerId = settings.RoleId is UserRole.Admin or UserRole.Approver ? null : settings.DealerId;
                // An Agent can only add their own assigned accounts to the
                // watchlist -- restrict the search results themselves so the
                // "Add" button never offers an account outside their book.
                int? agentUserId = settings.RoleId == UserRole.Agent ? settings.UserId : null;
                var searchResults = await _applicationReportService.GetAllAccountsAsync(dealerId, searchTerm, page, pageSize, agentUserId);
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

            // Defense in depth: accountId travels as a hidden form field, so
            // even though the search results already only offer an Agent
            // their own accounts, re-verify ownership server-side before
            // adding.
            if (settings.RoleId == UserRole.Agent &&
                !await _applicationReportService.IsAccountAssignedToAgentAsync(accountId, settings.UserId))
            {
                return RedirectToAction("Index", new { searchTerm });
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
            role is UserRole.Admin or UserRole.Dealer or UserRole.Agent or UserRole.Approver;
    }
}
