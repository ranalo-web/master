using Microsoft.AspNetCore.Mvc;
using Ranalo.Configuration;
using Ranalo.DataStore.DataModels;
using Ranalo.Models;
using Ranalo.Services;

namespace Ranalo.Controllers
{
    // Lock check: Nuovo phones that need attention (Services/LockCheckRules.cs).
    // Admin only -- actions lock phones and close contracts.
    [LoadUserSettingsFromCookie]
    public class LockCheckController : Controller
    {
        private static readonly string[] Brands = { "all", "samsung", "transsion", "other" };

        private readonly ILockCheckService _service;

        public LockCheckController(ILockCheckService service)
        {
            _service = service;
        }

        [HttpGet]
        [Route("lock-check")]
        public async Task<IActionResult> Index(LockCheckStage stage = LockCheckStage.NeedsNewLocker, string brand = "all", int days = LockCheckRules.DefaultMinDaysWithoutPayment)
        {
            if (!IsAdmin(out var redirect))
            {
                return redirect!;
            }

            SetLayoutViewBags();
            brand = Brands.Contains(brand?.ToLowerInvariant()) ? brand!.ToLowerInvariant() : "all";
            if (stage == LockCheckStage.None) stage = LockCheckStage.NeedsNewLocker;
            return View(await _service.GetAsync(stage, brand, days));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("lock-check/act")]
        public async Task<IActionResult> Act(string act, List<long> accounts, LockCheckStage stage, string brand = "all", int days = LockCheckRules.DefaultMinDaysWithoutPayment)
        {
            if (!IsAdmin(out var redirect))
            {
                return redirect!;
            }

            if (accounts == null || accounts.Count == 0)
            {
                TempData["LockCheckError"] = "Tick at least one phone first.";
                return RedirectToAction("Index", new { stage, brand, days });
            }

            var (label, result) = act switch
            {
                LockCheckAction.ToRecovery => ("sent to Recovery", await _service.SendToRecoveryAsync(accounts, days, UserId)),
                LockCheckAction.NuovoLock => ("sent a Nuovo lock", await _service.SendNuovoLockAsync(accounts, days, UserId)),
                LockCheckAction.CloseRelease => ("closed and released", await _service.CloseAndReleaseAsync(accounts, UserId)),
                _ => ("", new LockCheckResult(0, 0, new List<string> { "Unknown action." })),
            };

            var summary = $"{result.Done} phone{(result.Done == 1 ? "" : "s")} {label}."
                + (result.Failed > 0 ? $" {result.Failed} not: {string.Join(" ", result.Messages.Take(10))}" : "");
            TempData[result.Failed > 0 && result.Done == 0 ? "LockCheckError" : "LockCheckResult"] = summary;
            return RedirectToAction("Index", new { stage, brand, days });
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
            ViewBag.BackLink = "lock-check";
            ViewBag.IsAdmin = true;
            ViewBag.IsApprover = false;
            ViewBag.IsDealer = false;
            ViewBag.UserName = settings.KnownAs;
        }
    }
}
