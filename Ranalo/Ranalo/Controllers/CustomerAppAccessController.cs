using Microsoft.AspNetCore.Mvc;
using Ranalo.DataStore;
using Ranalo.DataStore.DataModels;
using Ranalo.Models;
using Ranalo.Services;

namespace Ranalo.Controllers
{
    // Staff page for the customer app (Admin and Approver): who uses it,
    // whose access is locked after too many wrong codes, unlocking it once
    // the customer has emailed in and been checked, and previewing any
    // account's app read-only.
    public class CustomerAppAccessController : Controller
    {
        private readonly ICustomerAccessRepository _access;
        private readonly ICustomerSession _session;
        private readonly ILogger<CustomerAppAccessController> _logger;

        public CustomerAppAccessController(ICustomerAccessRepository access, ICustomerSession session, ILogger<CustomerAppAccessController> logger)
        {
            _access = access;
            _session = session;
            _logger = logger;
        }

        public static bool CanManage(UserRole role) => role is UserRole.Admin or UserRole.Approver;

        [HttpGet("customer-app")]
        public async Task<IActionResult> Index(string show = "locked", string? q = null)
        {
            if (!Allowed(out var user, out var redirect)) return redirect!;

            // An issued code may be on this page: never cached.
            Response.Headers["Cache-Control"] = "no-store";
            var lockedOnly = show != "all";
            var model = new CustomerAppAccessViewModel
            {
                Show = lockedOnly ? "locked" : "all",
                Search = q,
                Rows = await _access.ListAsync(lockedOnly, q),
                LockedCount = (await _access.ListAsync(true, null)).Count,
                Message = TempData["CustomerAppMessage"] as string,
                Error = TempData["CustomerAppError"] as string,
                IssuedCode = TempData["CustomerAppCode"] is string issued && issued.Split('|') is { Length: 3 } p
                    ? new IssuedCode(p[0], p[1], p[2]) : null,
            };
            return View(model);
        }

        [HttpPost("customer-app/unlock")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Unlock(long accountNo, string? note)
        {
            if (!Allowed(out var user, out var redirect)) return redirect!;

            if (string.IsNullOrWhiteSpace(note))
            {
                TempData["CustomerAppError"] = $"Say why {accountNo} is being unlocked (e.g. \"emailed 6 Nov, ID checked\").";
                return RedirectToAction(nameof(Index));
            }

            if (await _access.UnlockAsync(accountNo, user!.UserId, note.Trim()))
            {
                _logger.LogInformation("Customer app access unlocked: {Account} by {User} ({Note})", accountNo, user.KnownAs, note.Trim());
                TempData["CustomerAppMessage"] = $"Account {accountNo} unlocked. The customer can sign in again with a new code.";
            }
            else
            {
                TempData["CustomerAppError"] = $"Account {accountNo} isn't locked.";
            }
            return RedirectToAction(nameof(Index));
        }

        // For a customer whose phone messages don't arrive (or for staff to
        // test): a one-time code shown once on screen, to read to the
        // customer after checking it's them. Signs in the normal way.
        [HttpPost("customer-app/issue-code")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> IssueCode(string? accountNo)
        {
            if (!Allowed(out var user, out var redirect)) return redirect!;

            var account = CustomerLoginRules.AccountNo(accountNo);
            var target = account == null ? null : await _access.GetTargetAsync(account.Value);
            if (target == null)
            {
                TempData["CustomerAppError"] = $"No phone found for account '{accountNo}'.";
                return RedirectToAction(nameof(Index));
            }

            var access = await _access.GetOrCreateAsync(target.AccountNo, target);
            if (access.Locked)
            {
                TempData["CustomerAppError"] = $"Account {target.AccountNo}'s app access is locked. Unlock it first.";
                return RedirectToAction(nameof(Index), new { show = "locked" });
            }

            var code = CustomerLoginRules.NewCode();
            var now = DateTime.UtcNow;
            await _access.AddCodeAsync(new CustomerLoginCode
            {
                AccountNo = target.AccountNo,
                CodeHash = PasswordRules.Hash(code),
                SentAtUtc = now,
                ExpiresAtUtc = now.Add(CustomerLoginRules.StaffCodeLifetime),
                Sent = true,
            }, CustomerLoginRules.StaffIssued(user!.UserId));

            _logger.LogInformation("Customer app sign-in code issued by staff for {Account} by {User}", target.AccountNo, user.KnownAs);
            TempData["CustomerAppCode"] = $"{target.AccountNo}|{CustomerMessages.Name(target.FirstName)}|{code}";
            return RedirectToAction(nameof(Index), new { show = "all", q = target.AccountNo.ToString() });
        }

        [HttpPost("customer-app/preview")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Preview(string? accountNo)
        {
            if (!Allowed(out var user, out var redirect)) return redirect!;

            var account = CustomerLoginRules.AccountNo(accountNo);
            if (account == null || await _access.GetTargetAsync(account.Value) == null)
            {
                TempData["CustomerAppError"] = $"No phone found for account '{accountNo}'.";
                return RedirectToAction(nameof(Index));
            }

            _logger.LogInformation("Customer app preview of {Account} by {User}", account, user!.KnownAs);
            _session.StartPreview(Response, account.Value, user.UserId);
            return Redirect("/my");
        }

        private bool Allowed(out User? user, out IActionResult? redirect)
        {
            user = HttpContext.Items["UserSettings"] as User;
            redirect = user == null ? RedirectToAction("Index", "Login")
                : !CanManage(user.RoleId) ? RedirectToAction("Index", "Home") : null;
            if (redirect == null)
            {
                ViewBag.BackLink = "customer-app";
                ViewBag.IsAdmin = user!.RoleId == UserRole.Admin;
                ViewBag.IsApprover = user.RoleId == UserRole.Approver;
                ViewBag.IsDealer = false;
                ViewBag.UserName = user.KnownAs;
            }
            return redirect == null;
        }
    }

    public class CustomerAppAccessViewModel
    {
        public string Show { get; set; } = "locked";
        public string? Search { get; set; }
        public List<CustomerAccessRow> Rows { get; set; } = new();
        public int LockedCount { get; set; }
        public string? Message { get; set; }
        public string? Error { get; set; }
        public IssuedCode? IssuedCode { get; set; }
    }

    // Shown once, straight after issuing; never stored as typed.
    public record IssuedCode(string AccountNo, string Name, string Code);
}
