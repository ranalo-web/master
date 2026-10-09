using Microsoft.AspNetCore.Mvc;
using Ranalo.Configuration;
using Ranalo.DataStore.DataModels;
using Ranalo.Models;
using Ranalo.Services;

namespace Ranalo.Controllers
{
    // Recovery: phones we're trying to get back under a lock (Services/
    // RecoveryRules.cs). Admin only -- every step calls Veritech or Knox on
    // a real phone.
    [LoadUserSettingsFromCookie]
    public class RecoveryController : Controller
    {
        private static readonly string[] Statuses = { "open", "moved", "ended", "all" };

        private readonly IRecoveryService _recoveryService;

        public RecoveryController(IRecoveryService recoveryService)
        {
            _recoveryService = recoveryService;
        }

        [HttpGet]
        [Route("recovery")]
        public async Task<IActionResult> Index(string status = "open")
        {
            if (!IsAdmin(out var redirect))
            {
                return redirect!;
            }

            SetLayoutViewBags();
            status = Statuses.Contains(status?.ToLowerInvariant()) ? status!.ToLowerInvariant() : "open";
            return View(await _recoveryService.GetListAsync(status));
        }

        // Fills the add form from the account's device.
        [HttpGet]
        [Route("recovery/lookup")]
        public async Task<IActionResult> Lookup(long accountNo)
        {
            if (!IsAdmin(out _))
            {
                return Unauthorized();
            }

            var device = await _recoveryService.LookupAsync(accountNo);
            return device == null ? NotFound() : Json(device);
        }

        [HttpPost]
        [Route("recovery/add")]
        public async Task<IActionResult> Add(long accountNo, string reason, string? notes)
        {
            if (!IsAdmin(out var redirect))
            {
                return redirect!;
            }

            var (id, error) = await _recoveryService.AddAsync(accountNo, reason, string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(), UserId);
            if (error != null)
            {
                TempData["RecoveryError"] = error;
                return id.HasValue ? RedirectToAction("Details", new { id }) : RedirectToAction("Index");
            }

            TempData["RecoveryResult"] = $"Account {accountNo} added to Recovery.";
            return RedirectToAction("Details", new { id });
        }

        [HttpGet]
        [Route("recovery/{id:int}")]
        public async Task<IActionResult> Details(int id)
        {
            if (!IsAdmin(out var redirect))
            {
                return redirect!;
            }

            var model = await _recoveryService.GetDetailAsync(id);
            if (model == null)
            {
                return RedirectToAction("Index");
            }

            SetLayoutViewBags();
            return View(model);
        }

        [HttpPost]
        [Route("recovery/{id:int}/action")]
        public async Task<IActionResult> RunStep(int id, string step, string? note)
        {
            if (!IsAdmin(out var redirect))
            {
                return redirect!;
            }

            if (!Enum.TryParse<RecoveryAction>(step, out var action))
            {
                TempData["RecoveryError"] = "Unknown step.";
                return RedirectToAction("Details", new { id });
            }

            var result = await _recoveryService.RunAsync(id, action, note, UserId);
            TempData[result.Success ? "RecoveryResult" : "RecoveryError"] = result.Message;
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
            ViewBag.BackLink = "recovery";
            ViewBag.IsAdmin = true;
            ViewBag.IsApprover = false;
            ViewBag.IsDealer = false;
            ViewBag.UserName = settings.KnownAs;
        }
    }
}
