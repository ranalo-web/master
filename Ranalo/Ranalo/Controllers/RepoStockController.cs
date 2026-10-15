using Microsoft.AspNetCore.Mvc;
using Ranalo.Configuration;
using Ranalo.DataStore.DataModels;
using Ranalo.Models;
using Ranalo.Services;

namespace Ranalo.Controllers
{
    // Repo stock: phones taken back and waiting to be sold again
    // (Services/RepoStockRules.cs). Admin only.
    [LoadUserSettingsFromCookie]
    public class RepoStockController : Controller
    {
        private static readonly string[] Statuses = { "instock", "sold", "all" };

        private readonly IRepoStockService _service;

        public RepoStockController(IRepoStockService service)
        {
            _service = service;
        }

        [HttpGet]
        [Route("repo-stock")]
        public async Task<IActionResult> Index(string status = "instock")
        {
            if (!IsAdmin(out var redirect))
            {
                return redirect!;
            }

            SetLayoutViewBags();
            status = Statuses.Contains(status?.ToLowerInvariant()) ? status!.ToLowerInvariant() : "instock";
            return View(await _service.GetListAsync(status));
        }

        // Fills the add form from the account.
        [HttpGet]
        [Route("repo-stock/lookup")]
        public async Task<IActionResult> Lookup(long accountNo)
        {
            if (!IsAdmin(out _))
            {
                return Unauthorized();
            }

            var account = await _service.LookupAsync(accountNo);
            return account == null ? NotFound() : Json(account);
        }

        [HttpGet]
        [Route("repo-stock/agents")]
        public async Task<IActionResult> Agents(int dealerReference)
        {
            if (!IsAdmin(out _))
            {
                return Unauthorized();
            }

            var agents = await _service.GetAgentsAsync(dealerReference);
            return Json(agents.Select(a => new { a.UserId, Name = $"{a.Name} {a.LastName}".Trim() }));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("repo-stock/add")]
        public async Task<IActionResult> Add(long accountNo, string reason, string? condition)
        {
            if (!IsAdmin(out var redirect))
            {
                return redirect!;
            }

            var result = await _service.AddAsync(accountNo, reason, condition, UserId);
            TempData[result.Success ? "RepoResult" : "RepoError"] = result.Message;
            return result.Id.HasValue ? RedirectToAction("Details", new { id = result.Id }) : RedirectToAction("Index");
        }

        [HttpGet]
        [Route("repo-stock/{id:int}")]
        public async Task<IActionResult> Details(int id, int page = 1)
        {
            if (!IsAdmin(out var redirect))
            {
                return redirect!;
            }

            var model = await _service.GetDetailAsync(id, page);
            if (model == null)
            {
                return RedirectToAction("Index");
            }

            SetLayoutViewBags();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("repo-stock/{id:int}/sell")]
        public async Task<IActionResult> Sell(int id, RepoSale sale)
        {
            if (!IsAdmin(out var redirect))
            {
                return redirect!;
            }

            var result = await _service.SellAsync(id, sale, UserId);
            TempData[result.Success ? "RepoResult" : "RepoError"] = result.Message;
            return RedirectToAction("Details", new { id });
        }

        private int UserId => ((User)HttpContext.Items["UserSettings"]!).UserId;

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
            ViewBag.BackLink = "repo-stock";
            ViewBag.IsAdmin = true;
            ViewBag.IsApprover = false;
            ViewBag.IsDealer = false;
            ViewBag.UserName = settings.KnownAs;
        }
    }
}
