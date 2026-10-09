using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Ranalo.Configuration;
using Ranalo.DataStore.DataModels;
using Ranalo.Models;
using Ranalo.Services;

namespace Ranalo.Controllers
{
    // Fraud early-warning checks and reports (Services/FraudRules.cs).
    // Admin only: dealers and agents must never see which check fired.
    [LoadUserSettingsFromCookie]
    public class FraudController : Controller
    {
        private static readonly string[] Statuses = { "active", "open", "investigating", "confirmed", "cleared", "all" };

        private readonly IFraudService _fraudService;
        private readonly ILogger<FraudController> _logger;

        public FraudController(IFraudService fraudService, ILogger<FraudController> logger)
        {
            _fraudService = fraudService;
            _logger = logger;
        }

        [HttpGet]
        [Route("fraud")]
        public async Task<IActionResult> Index(string? check = null, int? dealerId = null, string status = "active")
        {
            if (!IsAdmin(out var redirect))
            {
                return redirect!;
            }

            SetLayoutViewBags();
            return View(await _fraudService.GetAsync(CleanCheck(check), dealerId, CleanStatus(status)));
        }

        [HttpPost]
        [Route("fraud/review")]
        public async Task<IActionResult> Review(string checkCode, string subjectKey, string status, string? notes,
            string? check = null, int? dealerId = null, string listStatus = "active")
        {
            if (!IsAdmin(out var redirect))
            {
                return redirect!;
            }

            var reviewStatus = FraudReviewStatus.Settable.Append(FraudReviewStatus.Open)
                .FirstOrDefault(s => string.Equals(s, status, StringComparison.OrdinalIgnoreCase));
            if (FraudRules.GetCheck(checkCode) == null || string.IsNullOrWhiteSpace(subjectKey) || reviewStatus == null)
            {
                TempData["FraudError"] = "That review could not be saved.";
            }
            else
            {
                var userId = ((User)HttpContext.Items["UserSettings"]!).UserId;
                notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()[..Math.Min(notes.Trim().Length, 1000)];
                try
                {
                    await _fraudService.SaveReviewAsync(checkCode, subjectKey.Trim(), reviewStatus, notes, userId);
                    _logger.LogInformation("Fraud: user {UserId} set {Check} {Subject} to {Status}", userId, checkCode, subjectKey, reviewStatus);
                    TempData["FraudResult"] = $"{FraudRules.GetCheck(checkCode)!.Title} – {subjectKey}: {reviewStatus}.";
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Fraud: saving review {Check} {Subject} failed", checkCode, subjectKey);
                    TempData["FraudError"] = "The review could not be saved. Has Database/Fraud/001_create_fraud_flag_reviews.sql been run?";
                }
            }

            return RedirectToAction("Index", new { check = CleanCheck(check), dealerId, status = CleanStatus(listStatus) });
        }

        [HttpGet]
        [Route("fraud/export")]
        public async Task<IActionResult> Export(string? check = null, int? dealerId = null, string status = "active")
        {
            if (!IsAdmin(out var redirect))
            {
                return redirect!;
            }

            var model = await _fraudService.GetAsync(CleanCheck(check), dealerId, CleanStatus(status));
            var csv = new StringBuilder();
            csv.AppendLine("Check,Flagged on,Account,Orders,Customer,Dealer,Amount at risk,Review,Notes,Evidence");
            foreach (var f in model.Flags)
            {
                csv.AppendLine(string.Join(",", new[]
                {
                    FraudRules.GetCheck(f.CheckCode)?.Title ?? f.CheckCode,
                    f.FlaggedOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    f.AccountNo?.ToString(CultureInfo.InvariantCulture) ?? "",
                    string.Join(" ", f.OrderIds),
                    f.CustomerName ?? "",
                    f.DealerName ?? "",
                    f.AmountAtRisk.ToString("0.00", CultureInfo.InvariantCulture),
                    f.ReviewStatus,
                    f.Review?.Notes ?? "",
                    f.Evidence,
                }.Select(Csv)));
            }

            return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray(),
                "text/csv", $"fraud_flags_{DateTime.Today:yyyy-MM-dd}.csv");
        }

        private static string Csv(string value) =>
            value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;

        private static string? CleanCheck(string? check) => FraudRules.GetCheck(check)?.Code;

        private static string CleanStatus(string? status) =>
            Statuses.Contains(status?.ToLowerInvariant()) ? status!.ToLowerInvariant() : "active";

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
            ViewBag.BackLink = "fraud";
            ViewBag.IsAdmin = true;
            ViewBag.IsApprover = false;
            ViewBag.IsDealer = false;
            ViewBag.UserName = settings.KnownAs;
        }
    }
}
