using Microsoft.AspNetCore.Mvc;
using Ranalo.Configuration;
using Ranalo.DataStore.DataModels;
using Ranalo.Services;

namespace Ranalo.Controllers
{
    // The dealer's and agent's side of the fraud checks: their own accounts
    // and orders that are under verification or failed, with no hint of
    // which check fired (FraudRules.VerificationItems). Admins use /fraud.
    [LoadUserSettingsFromCookie]
    public class VerificationController : Controller
    {
        private readonly IFraudService _fraudService;

        public VerificationController(IFraudService fraudService)
        {
            _fraudService = fraudService;
        }

        [HttpGet]
        [Route("verification")]
        public async Task<IActionResult> Index()
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            if (settings.RoleId == UserRole.Admin)
            {
                return Redirect("/fraud");
            }

            if (settings.RoleId is not (UserRole.Dealer or UserRole.Agent))
            {
                return RedirectToAction("Index", "Home");
            }

            ViewBag.BackLink = "verification";
            ViewBag.IsAdmin = false;
            ViewBag.IsApprover = false;
            ViewBag.IsDealer = settings.RoleId == UserRole.Dealer;
            ViewBag.IsAgent = settings.RoleId == UserRole.Agent;
            ViewBag.UserName = settings.KnownAs;

            var model = settings.RoleId == UserRole.Dealer
                ? await _fraudService.GetVerificationAsync(settings.DealerId, null)
                : await _fraudService.GetVerificationAsync(null, settings.UserId);
            return View(model);
        }
    }
}
