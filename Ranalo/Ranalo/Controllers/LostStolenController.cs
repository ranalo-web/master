using Microsoft.AspNetCore.Mvc;
using Ranalo.Configuration;
using Ranalo.DataStore.DataModels;
using Ranalo.Models;
using Ranalo.Services;

namespace Ranalo.Controllers
{
    // Lost & Stolen phones: report with evidence, admin confirms (locks the
    // phone) or rejects, then closes. See LostStolenRules.
    [LoadUserSettingsFromCookie]
    public class LostStolenController : Controller
    {
        private readonly ILostStolenService _lostStolen;
        private readonly IApplicationReportService _applicationReportService;

        public LostStolenController(ILostStolenService lostStolen, IApplicationReportService applicationReportService)
        {
            _lostStolen = lostStolen;
            _applicationReportService = applicationReportService;
        }

        [HttpGet]
        [Route("lost-stolen")]
        public async Task<IActionResult> Index(string tab = "open", string searchTerm = "")
        {
            searchTerm = searchTerm?.Trim() ?? "";

            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }
            if (!LostStolenRules.CanUsePage(settings))
            {
                return RedirectToAction("Index", "Home");
            }

            SetViewBags(settings);

            var model = await _lostStolen.GetPageAsync(settings, tab);
            model.SearchTerm = searchTerm;

            if (!model.SetupMissing && !string.IsNullOrWhiteSpace(searchTerm))
            {
                // Same account search as the Watchlist, then cut down to the
                // accounts this user may report (collectors: the ones they hold).
                int? dealerId = settings.RoleId == UserRole.Dealer ? settings.DealerId : null;
                int? agentUserId = settings.RoleId == UserRole.Agent ? settings.UserId : null;
                var results = await _applicationReportService.GetAllAccountsAsync(dealerId, searchTerm, 1, 10, agentUserId);
                model.SearchResults = await _lostStolen.FilterToScopeAsync(settings, results.Accounts ?? new List<AllAccounts>());
            }

            return View(model);
        }

        [HttpPost]
        [Route("lost-stolen/report")]
        [RequestSizeLimit(30_000_000)]   // generous, so an oversized photo gets our "too big" message, not an error page
        public async Task<IActionResult> Report(NewLostStolenReport report, List<IFormFile>? files)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            var (uploads, error) = await ReadUploadsAsync(files);
            var (ok, message) = error != null ? (false, error) : await _lostStolen.ReportAsync(settings, report, uploads);
            return Done(ok, message);
        }

        [HttpPost]
        [Route("lost-stolen/{id:int}/evidence")]
        [RequestSizeLimit(30_000_000)]   // generous, so an oversized photo gets our "too big" message, not an error page
        public async Task<IActionResult> AddEvidence(int id, List<IFormFile>? files)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            var (uploads, error) = await ReadUploadsAsync(files);
            var (ok, message) = error != null ? (false, error) : await _lostStolen.AddEvidenceAsync(settings, id, uploads);
            return Done(ok, message);
        }

        [HttpGet]
        [Route("lost-stolen/evidence/{id:int}")]
        public async Task<IActionResult> Evidence(int id)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            var file = await _lostStolen.GetEvidenceAsync(settings, id);
            if (file == null)
            {
                return NotFound();
            }

            // Content type comes from our own allow-list (LostStolenRules), and
            // nosniff stops the browser second-guessing it.
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            return File(file.Content, file.ContentType, file.FileName);
        }

        [HttpPost]
        [Route("lost-stolen/{id:int}/confirm")]
        public async Task<IActionResult> Confirm(int id, string? note) =>
            await AdminAction(admin => _lostStolen.ConfirmAsync(admin, id, note));

        [HttpPost]
        [Route("lost-stolen/{id:int}/reject")]
        public async Task<IActionResult> Reject(int id, string? note) =>
            await AdminAction(admin => _lostStolen.RejectAsync(admin, id, note));

        [HttpPost]
        [Route("lost-stolen/{id:int}/lock")]
        public async Task<IActionResult> LockAgain(int id) =>
            await AdminAction(admin => _lostStolen.ResendLockAsync(admin, id));

        [HttpPost]
        [Route("lost-stolen/{id:int}/close")]
        public async Task<IActionResult> Close(int id, string? outcome, string? note) =>
            await AdminAction(admin => _lostStolen.CloseAsync(admin, id, outcome, note));

        private async Task<IActionResult> AdminAction(Func<User, Task<(bool Ok, string Message)>> action)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            var (ok, message) = await action(settings);
            return Done(ok, message);
        }

        private IActionResult Done(bool ok, string message)
        {
            TempData[ok ? "LostStolenSuccess" : "LostStolenError"] = message;
            return RedirectToAction("Index");
        }

        // Files over the limit are refused here, before reading them in.
        private static async Task<(List<LostStolenUpload> Uploads, string? Error)> ReadUploadsAsync(List<IFormFile>? files)
        {
            var uploads = new List<LostStolenUpload>();
            foreach (var f in files ?? new List<IFormFile>())
            {
                if (f.Length == 0) continue;
                if (f.Length > LostStolenRules.MaxFileBytes)
                {
                    return (uploads, $"{LostStolenRules.SafeFileName(f.FileName)} is over {LostStolenRules.MaxFileSizeLabel}. Take the photo at a lower size, or scan it smaller.");
                }

                using var ms = new MemoryStream();
                await f.CopyToAsync(ms);
                uploads.Add(new LostStolenUpload(f.FileName, f.ContentType, ms.ToArray()));
            }
            return (uploads, null);
        }

        private void SetViewBags(User settings)
        {
            ViewBag.IsAdmin = settings.RoleId == UserRole.Admin;
            ViewBag.IsApprover = settings.RoleId == UserRole.Approver;
            ViewBag.IsDealer = settings.RoleId == UserRole.Dealer;
            ViewBag.IsAgent = settings.RoleId == UserRole.Agent;
            ViewBag.UserName = settings.KnownAs;
        }
    }
}
