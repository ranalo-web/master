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

        public CustomerCallsController(ICustomerScreeningService screening)
        {
            _screening = screening;
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
        public async Task<IActionResult> LogScreeningCall(ScreeningCall call)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            if (!CanCall(settings) || call.OrderId <= 0
                || call.CallType is not (ScreeningCall.Customer or ScreeningCall.NextOfKin))
            {
                return RedirectToAction("Index", "Home");
            }

            if (call.CallType == ScreeningCall.NextOfKin && call.NextOfKinNo is not (1 or 2))
            {
                TempData["ScreeningError"] = "Choose which next of kin was called.";
                return Redirect($"/order-details/{call.OrderId}");
            }

            // Only what applies to this call is kept; nothing counts if nobody answered.
            var isCustomer = call.CallType == ScreeningCall.Customer;
            call.NextOfKinNo = isCustomer ? null : call.NextOfKinNo;
            call.IdentityConfirmed = isCustomer && call.Reached && call.IdentityConfirmed;
            call.TermsConfirmed = isCustomer && call.Reached && call.TermsConfirmed;
            call.LockUnderstood = isCustomer && call.Reached && call.LockUnderstood;
            call.KnowsCustomer = !isCustomer && call.Reached && call.KnowsCustomer;
            call.RelationshipConfirmed = !isCustomer && call.Reached && call.RelationshipConfirmed;
            call.Relationship = isCustomer ? null : Trim(call.Relationship, 100);
            call.Notes = Trim(call.Notes, NotesMaxLength);
            call.CalledByUserId = settings.UserId;

            await _screening.LogScreeningCallAsync(call);
            TempData["ScreeningMessage"] = "Call logged.";
            return Redirect($"/order-details/{call.OrderId}");
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
