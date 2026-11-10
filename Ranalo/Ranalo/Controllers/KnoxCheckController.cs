using Microsoft.AspNetCore.Mvc;
using Ranalo.Configuration;
using Ranalo.DataStore;
using Ranalo.DataStore.DataModels;
using Ranalo.Services;

namespace Ranalo.Controllers
{
    // Knox check (2026-11-10): what Knox says about each phone next to what
    // we send it through, and the phones where the two disagree, for staff
    // to confirm (KnoxStatusRules). Admin only.
    [LoadUserSettingsFromCookie]
    public class KnoxCheckController : Controller
    {
        private readonly IKnoxStatusRepository _repo;
        private readonly IKnoxSwitchService _switch;
        private readonly ILogger<KnoxCheckController> _logger;

        public KnoxCheckController(IKnoxStatusRepository repo, IKnoxSwitchService switchService, ILogger<KnoxCheckController> logger)
        {
            _repo = repo;
            _switch = switchService;
            _logger = logger;
        }

        [HttpGet("knox-check")]
        public async Task<IActionResult> Index()
        {
            if (!IsAdmin(out var redirect)) return redirect!;
            try
            {
                return View(await _repo.GetCheckListAsync());
            }
            catch (System.Data.Common.DbException ex) when (ex.Message.Contains("Knox"))
            {
                _logger.LogWarning(ex, "Knox check: has Database/DeviceLock/002 been run?");
                ViewBag.NotReady = true;
                return View(new List<KnoxCheckRow>());
            }
        }

        [HttpPost("knox-check/switch")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Switch(long accountNo)
        {
            if (!IsAdmin(out var redirect)) return redirect!;
            var user = (User)HttpContext.Items["UserSettings"]!;
            try
            {
                var (done, message) = await _switch.SwitchToKnoxAsync(accountNo, user.UserId);
                TempData[done ? "KnoxResult" : "KnoxError"] = message;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Knox check: switching {Account} failed", accountNo);
                TempData["KnoxError"] = $"Account {accountNo}: the switch failed ({ex.Message}). Nothing was changed if Knox couldn't be reached.";
            }
            return RedirectToAction(nameof(Index));
        }

        private bool IsAdmin(out IActionResult? redirect)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            redirect = settings == null
                ? RedirectToAction("Index", "Login")
                : settings.RoleId != UserRole.Admin ? RedirectToAction("Index", "Home") : null;
            if (redirect == null)
            {
                ViewBag.BackLink = "knox-check";
                ViewBag.IsAdmin = true;
                ViewBag.IsApprover = false;
                ViewBag.IsDealer = false;
                ViewBag.UserName = settings!.KnownAs;
            }
            return redirect == null;
        }
    }
}
