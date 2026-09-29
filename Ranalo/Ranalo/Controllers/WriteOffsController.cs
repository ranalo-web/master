using Microsoft.AspNetCore.Mvc;
using Ranalo.Configuration;
using Ranalo.DataStore;
using Ranalo.DataStore.DataModels;
using Ranalo.Models;
using Ranalo.ScheduledServices;

namespace Ranalo.Controllers
{
    // Write-off review (Database/WriteOffs/001 has the policy). The nightly
    // ScheduledWriteOffCheck proposes Pending write-offs; an admin approves
    // or holds them here. Admin-only, same as the rest of Financials.
    [LoadUserSettingsFromCookie]
    public class WriteOffsController : Controller
    {
        private static readonly string[] Statuses = { "pending", "held", "approved", "reinstated" };

        private readonly IWriteOffRepository _repository;
        private readonly ILogger<WriteOffsController> _logger;

        public WriteOffsController(IWriteOffRepository repository, ILogger<WriteOffsController> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        [HttpGet]
        [Route("write-offs")]
        public async Task<IActionResult> Index(string status = "pending", string searchTerm = "", int page = 1, int pageSize = 100)
        {
            if (!IsAdmin(out var redirect))
            {
                return redirect!;
            }

            status = Statuses.Contains(status?.ToLowerInvariant()) ? status!.ToLowerInvariant() : "pending";
            page = Math.Max(1, page);

            SetLayoutViewBags();
            var (rows, total) = await _repository.GetPageAsync(status, searchTerm ?? "", page, pageSize);
            var model = new WriteOffsViewModel
            {
                Status = status,
                SearchTerm = searchTerm ?? "",
                Rows = rows,
                Summary = await _repository.GetSummaryAsync(),
                CurrentPage = page,
                TotalRecords = total,
                TotalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize)),
            };

            return View(model);
        }

        [HttpPost]
        [Route("write-offs/approve")]
        public async Task<IActionResult> Approve(List<int> ids, string status = "pending")
        {
            if (!IsAdmin(out var redirect))
            {
                return redirect!;
            }
            var userId = ((User)HttpContext.Items["UserSettings"]!).UserId;

            var approved = await _repository.ApproveAsync(ids, userId);
            _logger.LogInformation("Write-offs: user {UserId} approved {Count} write-off(s)", userId, approved);
            TempData["WriteOffResult"] = $"Approved {approved:N0} write-off(s).";
            return RedirectToAction("Index", new { status });
        }

        [HttpPost]
        [Route("write-offs/approve-all")]
        public async Task<IActionResult> ApproveAll()
        {
            if (!IsAdmin(out var redirect))
            {
                return redirect!;
            }
            var userId = ((User)HttpContext.Items["UserSettings"]!).UserId;

            var approved = await _repository.ApproveAllPendingAsync(userId);
            _logger.LogInformation("Write-offs: user {UserId} approved all {Count} pending write-off(s)", userId, approved);
            TempData["WriteOffResult"] = $"Approved all {approved:N0} pending write-off(s).";
            return RedirectToAction("Index", new { status = "pending" });
        }

        [HttpPost]
        [Route("write-offs/hold")]
        public async Task<IActionResult> Hold(List<int> ids, string reason, string status = "pending")
        {
            if (!IsAdmin(out var redirect))
            {
                return redirect!;
            }
            var userId = ((User)HttpContext.Items["UserSettings"]!).UserId;

            if (string.IsNullOrWhiteSpace(reason))
            {
                TempData["WriteOffError"] = "Give a reason for holding, e.g. \"promised to pay\" or \"device being recovered\".";
                return RedirectToAction("Index", new { status });
            }

            var reasonText = reason.Trim();
            var held = await _repository.HoldAsync(ids, reasonText.Length > 300 ? reasonText[..300] : reasonText, userId);
            TempData["WriteOffResult"] = $"Held {held:N0} write-off(s). They stay on the Held list until approved, or drop off by themselves if the customer pays.";
            return RedirectToAction("Index", new { status });
        }

        // One-off rebuild of past write-offs from payment history -- preview
        // first, recorded (as Approved) only when the admin confirms.
        [HttpGet]
        [Route("write-offs/history")]
        public async Task<IActionResult> History()
        {
            if (!IsAdmin(out var redirect))
            {
                return redirect!;
            }

            SetLayoutViewBags();
            try
            {
                return View(await _repository.GetHistoryPreviewAsync());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Write-offs: history preview failed");
                TempData["WriteOffError"] = $"Couldn't build the history preview: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        [Route("write-offs/history/record")]
        public async Task<IActionResult> RecordHistory()
        {
            if (!IsAdmin(out var redirect))
            {
                return redirect!;
            }
            var userId = ((User)HttpContext.Items["UserSettings"]!).UserId;

            try
            {
                var (writeOffs, recoveries) = await _repository.RecordHistoryAsync(userId);
                _logger.LogInformation("Write-offs: user {UserId} recorded history: {WriteOffs} write-off(s), {Recoveries} past recovery record(s)", userId, writeOffs, recoveries);
                TempData["WriteOffResult"] = $"Recorded {writeOffs:N0} past write-off(s) and {recoveries:N0} past device recovery record(s).";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Write-offs: recording history failed");
                TempData["WriteOffError"] = $"Recording history failed, nothing was saved: {ex.Message}";
            }

            return RedirectToAction("Index", new { status = "approved" });
        }

        // Runs the same check as the nightly job, so new proposals can be
        // seen straight away (the job itself only runs on the live server).
        [HttpPost]
        [Route("write-offs/run-check")]
        public async Task<IActionResult> RunCheck()
        {
            if (!IsAdmin(out var redirect))
            {
                return redirect!;
            }

            try
            {
                var (reinstated, proposed) = await ScheduledWriteOffCheck.RunAsync(_repository);
                TempData["WriteOffResult"] = $"Check complete: {proposed:N0} new write-off(s) to review, {reinstated:N0} reinstated because the customer paid.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Write-offs: manual check failed");
                TempData["WriteOffError"] = $"The check failed: {ex.Message}";
            }

            return RedirectToAction("Index", new { status = "pending" });
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
            ViewBag.BackLink = "write-offs";
            ViewBag.IsAdmin = true;
            ViewBag.IsApprover = false;
            ViewBag.IsDealer = false;
            ViewBag.UserName = settings.KnownAs;
        }
    }
}
