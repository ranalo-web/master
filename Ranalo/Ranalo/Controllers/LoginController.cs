using Microsoft.AspNetCore.Mvc;
using Ranalo.DataStore;
using Ranalo.DataStore.DataModels;
using Ranalo.Services;

namespace Ranalo.Controllers
{
    // Sign in, sign out and change password. Who's signed in comes from the
    // signed login cookie (LoginSession), checked against the database on
    // every request (UserSettingsMiddleware).
    public class LoginController : Controller
    {
        private const string LoginFailed = "That email and password don't match an active account.";

        private readonly IUserService _usersService;
        private readonly ILoginSession _session;
        private readonly IPasswordResetRepository _resets;
        private readonly ILogger<LoginController> _logger;

        public LoginController(IUserService usersService, ILoginSession session, IPasswordResetRepository resets, ILogger<LoginController> logger)
        {
            _usersService = usersService;
            _session = session;
            _resets = resets;
            _logger = logger;
        }

        [HttpGet]
        [Route("")]
        public IActionResult Index()
        {
            return HttpContext.Items["UserSettings"] is User user ? Home(user) : View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("")]
        public async Task<IActionResult> Login(string email, string password, string rememberme)
        {
            var user = await _usersService.LoginUser(email, password);
            if (user == null)
            {
                ViewBag.LoginError = LoginFailed;
                return View("Index");
            }

            _session.SignIn(Response, user);
            return Home(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("reset")]
        public async Task<IActionResult> ResetPassword(string email, string oldpassword, string newpassword, string? newpasswordconfirm)
        {
            var user = await _usersService.LoginUser(email, oldpassword);
            if (user == null)
            {
                ViewBag.ResetError = LoginFailed;
                return View();
            }

            // The reset page has no confirm box; the account page does.
            var cannotUse = PasswordRules.CannotUse(newpassword, newpasswordconfirm ?? newpassword);
            if (cannotUse != null)
            {
                ViewBag.ResetError = cannotUse;
                return View();
            }

            var updated = await _usersService.UpdateUserPasswordAsync(user.UserId, newpassword);
            _session.SignIn(Response, updated);
            return View("ResetSuccess");
        }

        [HttpGet]
        [Route("reset")]
        public IActionResult ResetPassword()
        {
            return View();
        }

        [HttpGet]
        [Route("reset-success")]
        public IActionResult ResetSuccess()
        {
            return RedirectToAction("Index");
        }

        [HttpGet]
        [Route("forgot-password")]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        // No email or SMS for staff yet: whoever manages the login sees the
        // request on the Users page and resets it. The reply is the same
        // whether or not the email has an account, so it can't be used to
        // find out which emails do.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("forgot-password")]
        public async Task<IActionResult> ForgotPassword(string email)
        {
            var user = string.IsNullOrWhiteSpace(email) ? null : await _usersService.GetUserByEmail(email.Trim());
            if (user != null && user.Status != UserStatus.Suspended)
            {
                await _resets.RequestAsync(user.UserId);
                _logger.LogInformation("Password reset requested for user {UserId}", user.UserId);
            }

            ViewBag.Requested = true;
            return View();
        }

        [HttpGet]
        [Route("logout")]
        public IActionResult Logout()
        {
            _session.SignOut(Response);
            return RedirectToAction("Index", "Login");
        }

        private IActionResult Home(User user) => user.RoleId switch
        {
            UserRole.Admin => RedirectToAction("Index", "AdminDashboard"),
            UserRole.Dealer => RedirectToAction("Index", "DealerDashboard"),
            UserRole.Approver => RedirectToAction("Index", "Approver"),
            UserRole.Collector => RedirectToAction("Index", "Collections"),
            UserRole.Agent => RedirectToAction("Index", "Agents"),
            _ => RedirectToAction("Index", "DealerDashboard")
        };
    }
}
