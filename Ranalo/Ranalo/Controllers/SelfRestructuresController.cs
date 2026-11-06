using Microsoft.AspNetCore.Mvc;
using Ranalo.DataStore;
using Ranalo.DataStore.DataModels;

namespace Ranalo.Controllers
{
    // Admin review of plans customers set up themselves in the app
    // (SelfRestructureRules): each is looked at within 24 hours -- marked
    // reviewed, or undone (the previous plan put back; it still counts as
    // the customer's one go). On "Waiting for you" until reviewed.
    public class SelfRestructuresController : Controller
    {
        private readonly ISelfRestructureRepository _plans;
        private readonly Ranalo.Services.IAccountRefreshQueue _refresh;
        private readonly ILogger<SelfRestructuresController> _logger;

        public SelfRestructuresController(ISelfRestructureRepository plans, Ranalo.Services.IAccountRefreshQueue refresh,
            ILogger<SelfRestructuresController> logger)
        {
            _plans = plans;
            _refresh = refresh;
            _logger = logger;
        }

        [HttpGet("self-restructures")]
        public async Task<IActionResult> Index(string show = "review")
        {
            if (!Allowed(out _, out var redirect)) return redirect!;
            ViewBag.Show = show == "all" ? "all" : "review";
            ViewBag.Message = TempData["SelfPlanMessage"] as string;
            ViewBag.Error = TempData["SelfPlanError"] as string;
            return View(await _plans.ListAsync(toReviewOnly: show != "all"));
        }

        [HttpPost("self-restructures/reviewed")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reviewed(int id)
        {
            if (!Allowed(out var user, out var redirect)) return redirect!;
            if (await _plans.MarkReviewedAsync(id, user!.UserId))
                TempData["SelfPlanMessage"] = "Marked as reviewed.";
            else
                TempData["SelfPlanError"] = "That plan was already reviewed or undone.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost("self-restructures/undo")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Undo(int id, long accountNo, string? note)
        {
            if (!Allowed(out var user, out var redirect)) return redirect!;
            if (string.IsNullOrWhiteSpace(note))
            {
                TempData["SelfPlanError"] = "Say why the plan is being undone.";
                return RedirectToAction(nameof(Index));
            }

            var error = await _plans.RevertAsync(id, user!.UserId, note.Trim());
            if (error != null)
            {
                TempData["SelfPlanError"] = error;
            }
            else
            {
                _logger.LogInformation("Self-service plan {Id} on {Account} undone by {User}: {Note}", id, accountNo, user.KnownAs, note.Trim());
                _refresh.Enqueue(accountNo);
                TempData["SelfPlanMessage"] = $"Plan undone on {accountNo}: the previous plan is back. It still counts as the customer's one plan.";
            }
            return RedirectToAction(nameof(Index));
        }

        private bool Allowed(out User? user, out IActionResult? redirect)
        {
            user = HttpContext.Items["UserSettings"] as User;
            redirect = user == null ? RedirectToAction("Index", "Login")
                : user.RoleId != UserRole.Admin ? RedirectToAction("Index", "Home") : null;
            if (redirect == null)
            {
                ViewBag.BackLink = "self-restructures";
                ViewBag.IsAdmin = true;
                ViewBag.IsApprover = false;
                ViewBag.IsDealer = false;
                ViewBag.UserName = user!.KnownAs;
            }
            return redirect == null;
        }
    }
}
