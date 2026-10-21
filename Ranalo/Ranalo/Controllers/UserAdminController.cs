using Microsoft.AspNetCore.Mvc;
using Ranalo.Configuration;
using Ranalo.DataStore;
using Ranalo.DataStore.DataModels;
using Ranalo.Models;
using Ranalo.Services;

namespace Ranalo.Controllers
{
    // Managing other people's logins: details, role, dealer, password reset,
    // suspend and reactivate (Services/UserAdminRules.cs). Your own details
    // and password are on My Account (AccountController).
    [LoadUserSettingsFromCookie]
    public class UserAdminController : Controller
    {
        private readonly IUserService _users;
        private readonly IPasswordResetRepository _resets;

        public UserAdminController(IUserService users, IPasswordResetRepository resets)
        {
            _users = users;
            _resets = resets;
        }

        [HttpGet]
        [Route("users/{userId:int}/edit")]
        public async Task<IActionResult> Edit(int userId)
        {
            var (actor, target, dealerId) = await LoadAsync(userId);
            if (actor == null || target == null)
            {
                return RedirectToAction("Users", "Home");
            }

            await SetViewBagsAsync(actor);
            ViewBag.ResetRequestedAt = await _resets.GetOpenRequestedAtAsync(target.UserId);
            return View(await ModelAsync(actor, target));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("users/{userId:int}/edit")]
        public async Task<IActionResult> Edit(int userId, EditUserForm form)
        {
            var (actor, target, _) = await LoadAsync(userId);
            if (actor == null || target == null)
            {
                return RedirectToAction("Users", "Home");
            }

            var email = form.Email?.Trim() ?? "";
            string? error = null;
            if (string.IsNullOrWhiteSpace(form.Name) || string.IsNullOrWhiteSpace(email))
            {
                error = "Enter a first name and email.";
            }
            else if (!UserAdminRules.CanAssign(actor, form.RoleId))
            {
                error = "You can't give this user that role.";
            }
            else if (!string.Equals(email, target.Email, StringComparison.OrdinalIgnoreCase)
                     && await _users.GetUserByEmail(email) != null)
            {
                error = $"Another user already uses {email}.";
            }

            if (error != null)
            {
                TempData["UserError"] = error;
                return RedirectToAction("Edit", new { userId });
            }

            target.Name = form.Name!.Trim();
            target.LastName = form.LastName?.Trim();
            target.KnownAs = form.KnownAs?.Trim();
            target.Email = email;
            target.Phone = form.Phone?.Trim();
            target.City = form.City?.Trim();
            target.RoleId = form.RoleId;
            if (actor.RoleId == UserRole.Admin && form.DealerId > 0)
            {
                target.DealerId = form.DealerId;
                target.ParentUserId = form.DealerId;
            }

            await _users.SaveUserAsync(target);
            TempData["UserMessage"] = "Saved.";
            return RedirectToAction("Edit", new { userId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("users/{userId:int}/password")]
        public async Task<IActionResult> ResetPassword(int userId, string newPassword, string confirmPassword)
        {
            var (actor, target, _) = await LoadAsync(userId);
            if (actor == null || target == null)
            {
                return RedirectToAction("Users", "Home");
            }

            var cannotUse = PasswordRules.CannotUse(newPassword, confirmPassword);
            if (cannotUse != null)
            {
                TempData["UserError"] = cannotUse;
                return RedirectToAction("Edit", new { userId });
            }

            // Signs them out everywhere (their password stamp changes).
            await _users.UpdateUserPasswordAsync(target.UserId, newPassword);
            await _resets.ResolveAsync(target.UserId, actor.UserId);
            TempData["UserMessage"] = "Password reset. Give it to them securely; they can change it on My Account.";
            return RedirectToAction("Edit", new { userId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("users/{userId:int}/suspend")]
        public Task<IActionResult> Suspend(int userId) => SetStatusAsync(userId, UserStatus.Suspended, "Suspended: they can't log in.");

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("users/{userId:int}/reactivate")]
        public Task<IActionResult> Reactivate(int userId) => SetStatusAsync(userId, UserStatus.Active, "Reactivated.");

        private async Task<IActionResult> SetStatusAsync(int userId, UserStatus status, string message)
        {
            var (actor, target, _) = await LoadAsync(userId);
            if (actor == null || target == null)
            {
                return RedirectToAction("Users", "Home");
            }

            target.Status = status;
            target.IsActive = status == UserStatus.Active;
            await _users.SaveUserAsync(target);
            TempData["UserMessage"] = message;
            return RedirectToAction("Edit", new { userId });
        }

        // The signed-in user and the user they're managing, or nulls when they
        // aren't allowed to.
        private async Task<(User? Actor, User? Target, int ActorDealerId)> LoadAsync(int userId)
        {
            if (HttpContext.Items["UserSettings"] is not User actor)
            {
                return (null, null, 0);
            }

            var actorDealerId = actor.RoleId == UserRole.Dealer
                ? (await _users.GetDealerByUserId(actor.UserId))?.DealerId ?? -1
                : actor.DealerId;
            var target = await _users.GetUserAnyUserByIdAsync(userId);
            return target != null && UserAdminRules.CanManage(actor, actorDealerId, target)
                ? (actor, target, actorDealerId)
                : (actor, null, actorDealerId);
        }

        private async Task<EditUserViewModel> ModelAsync(User actor, User target) => new()
        {
            User = target,
            AssignableRoles = UserAdminRules.AssignableRoles(actor),
            Dealers = actor.RoleId == UserRole.Admin ? await _users.GetAllDealers() ?? new() : new()
        };

        private async Task SetViewBagsAsync(User settings)
        {
            ViewBag.BackLink = "users";
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
