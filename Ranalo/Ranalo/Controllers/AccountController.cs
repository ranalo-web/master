using Microsoft.AspNetCore.Mvc;
using Ranalo.Configuration;
using Ranalo.DataStore.DataModels;
using Ranalo.Models;
using Ranalo.Services;

namespace Ranalo.Controllers
{
    // My Account: everyone's own name, phone and city, and their password.
    // Email, role, dealer and status are changed by whoever manages their
    // login (UserAdminController).
    [LoadUserSettingsFromCookie]
    public class AccountController : Controller
    {
        private readonly IUserService _users;
        private readonly ILoginSession _session;

        public AccountController(IUserService users, ILoginSession session)
        {
            _users = users;
            _session = session;
        }

        [HttpGet]
        [Route("account")]
        public async Task<IActionResult> Index()
        {
            var me = (User)HttpContext.Items["UserSettings"]!;
            var dealer = me.RoleId == UserRole.Dealer ? await _users.GetDealerByUserId(me.UserId)
                : me.DealerId > 0 ? await _users.GetDealerByDealerId(me.DealerId) : null;

            await SetViewBagsAsync(me);
            return View(new AccountViewModel { User = me, DealerName = dealer?.CompanyName });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("account/details")]
        public async Task<IActionResult> Details(AccountForm form)
        {
            var me = (User)HttpContext.Items["UserSettings"]!;
            if (string.IsNullOrWhiteSpace(form.Name))
            {
                TempData["AccountError"] = "Enter your first name.";
                return RedirectToAction("Index");
            }

            var user = await _users.GetUserAnyUserByIdAsync(me.UserId);
            if (user == null)
            {
                return RedirectToAction("Index", "Login");
            }

            user.Name = form.Name.Trim();
            user.LastName = form.LastName?.Trim();
            user.KnownAs = form.KnownAs?.Trim();
            user.Phone = form.Phone?.Trim();
            user.City = form.City?.Trim();
            await _users.SaveUserAsync(user);

            TempData["AccountMessage"] = "Your details are saved.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("account/password")]
        public async Task<IActionResult> Password(string currentPassword, string newPassword, string confirmPassword)
        {
            var me = (User)HttpContext.Items["UserSettings"]!;
            if (await _users.LoginUser(me.Email, currentPassword) == null)
            {
                TempData["PasswordError"] = "Your current password isn't right.";
                return RedirectToAction("Index");
            }

            var cannotUse = PasswordRules.CannotUse(newPassword, confirmPassword);
            if (cannotUse != null)
            {
                TempData["PasswordError"] = cannotUse;
                return RedirectToAction("Index");
            }

            // Other browsers are signed out; this one stays signed in.
            var updated = await _users.UpdateUserPasswordAsync(me.UserId, newPassword);
            _session.SignIn(Response, updated);

            TempData["PasswordMessage"] = "Password changed. You've been signed out on any other devices.";
            return RedirectToAction("Index");
        }

        private async Task SetViewBagsAsync(User settings)
        {
            ViewBag.BackLink = "account";
            ViewBag.IsAdmin = settings.RoleId == UserRole.Admin;
            ViewBag.IsApprover = settings.RoleId == UserRole.Approver;
            ViewBag.IsDealer = settings.RoleId == UserRole.Dealer;
            ViewBag.IsAgent = settings.RoleId == UserRole.Agent;
            ViewBag.UserName = settings.KnownAs;
            if (settings.RoleId == UserRole.Dealer)
            {
                ViewBag.UserName = (await _users.GetDealerByUserId(settings.UserId))?.CompanyName ?? settings.KnownAs;
            }
        }
    }
}
