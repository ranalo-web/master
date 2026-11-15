using Microsoft.AspNetCore.Mvc;
using Ranalo.Configuration;
using Ranalo.DataStore.DataModels;
using Ranalo.Models;
using Ranalo.Services;

namespace Ranalo.Controllers
{
    // Approvers' and admins' calls: screening before an order is approved,
    // welfare calls after enrolment, and customers not paying in their first
    // month (Services/ScreeningRules.cs). Admins use the same screens.
    [LoadUserSettingsFromCookie]
    public class CustomerCallsController : Controller
    {
        private const int NotesMaxLength = 1000;

        private readonly ICustomerScreeningService _screening;
        private readonly IApplicationReportService _orders;
        private readonly ILogger<CustomerCallsController> _logger;

        public CustomerCallsController(ICustomerScreeningService screening, IApplicationReportService orders, ILogger<CustomerCallsController> logger)
        {
            _screening = screening;
            _orders = orders;
            _logger = logger;
        }

        private static bool CanCall(User settings) =>
            settings.RoleId is UserRole.Admin or UserRole.Approver;

        [HttpGet]
        [Route("customer-calls")]
        public async Task<IActionResult> Index()
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            if (!CanCall(settings))
            {
                return RedirectToAction("Index", "Home");
            }

            SetViewBags(settings);
            return View(await _screening.GetCustomerCallsAsync());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("customer-calls/screening")]
        // The screening form: the customer call, the next of kin call or both,
        // saved together (ScreeningRules.CallsFromForm).
        public async Task<IActionResult> LogScreeningCall(ScreeningCallsForm form)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            if (!CanCall(settings) || form.OrderId <= 0)
            {
                return RedirectToAction("Index", "Home");
            }

            var (calls, error) = ScreeningRules.CallsFromForm(form, settings.UserId);
            if (error != null)
            {
                TempData["ScreeningError"] = error;
                return Redirect($"/order-details/{form.OrderId}#screening");
            }

            foreach (var call in calls)
            {
                await _screening.LogScreeningCallAsync(call);
            }
            TempData["ScreeningMessage"] = calls.Count == 2 ? "Both calls logged." : "Call logged.";

            // The call that completes screening approves the order.
            var order = await _orders.GetOrderByOrderIdAsync(form.OrderId);
            if (order != null)
            {
                var screening = await _screening.GetOrderScreeningAsync(form.OrderId, order.DateCreated);
                if (ScreeningRules.ShouldAutoApprove(order.Status, screening.Status))
                {
                    try
                    {
                        await _orders.ApproveOrderAsync(form.OrderId);
                        TempData["ScreeningMessage"] = "Screening passed, so the order is now approved and can be enrolled.";
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Auto-approving order {OrderId} after screening failed", form.OrderId);
                        TempData["ScreeningError"] = "Screening passed, but the order couldn't be approved automatically. Press Approve.";
                    }
                }
            }

            return Redirect($"/order-details/{form.OrderId}#screening");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("customer-calls/welfare")]
        public async Task<IActionResult> LogWelfareCall(WelfareCall call, string? returnUrl)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            if (!CanCall(settings) || call.AccountNo <= 0
                || call.Outcome is not (WelfareCall.AllGood or WelfareCall.Issue or WelfareCall.NoAnswer))
            {
                return RedirectToAction("Index", "Home");
            }

            if (call.Outcome == WelfareCall.Issue && string.IsNullOrWhiteSpace(call.Notes))
            {
                TempData["CallsError"] = $"Add a note describing the issue for account {call.AccountNo}.";
                return LocalRedirectOr(returnUrl);
            }

            call.Notes = Trim(call.Notes, NotesMaxLength);
            call.CalledByUserId = settings.UserId;

            await _screening.LogWelfareCallAsync(call);
            TempData["CallsMessage"] = $"Welfare call logged for account {call.AccountNo}.";
            return LocalRedirectOr(returnUrl);
        }

        private IActionResult LocalRedirectOr(string? returnUrl) =>
            !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
                ? LocalRedirect(returnUrl)
                : RedirectToAction("Index");

        private static string? Trim(string? value, int max)
        {
            var trimmed = value?.Trim();
            return string.IsNullOrEmpty(trimmed) ? null : trimmed.Length > max ? trimmed[..max] : trimmed;
        }

        private void SetViewBags(User settings)
        {
            ViewBag.BackLink = "customer-calls";
            ViewBag.IsAdmin = settings.RoleId == UserRole.Admin;
            ViewBag.IsApprover = settings.RoleId == UserRole.Approver;
            ViewBag.IsDealer = false;
            ViewBag.IsAgent = false;
            ViewBag.UserName = settings.KnownAs;
        }
    }
}
