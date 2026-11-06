using Microsoft.AspNetCore.Mvc;
using Ranalo.DataStore;
using Ranalo.DataStore.DataModels;
using Ranalo.Models;
using Ranalo.Services;

namespace Ranalo.Controllers
{
    // Approver follow-ups (ApproverFollowUpRules): pool accounts the
    // Approvers sort out before a collector is involved. Approver and Admin.
    public class ApproverFollowUpsController : Controller
    {
        private readonly IApproverFollowUpRepository _followUps;
        private readonly ICollectionsRepository _collections;
        private readonly ILogger<ApproverFollowUpsController> _logger;

        public ApproverFollowUpsController(IApproverFollowUpRepository followUps, ICollectionsRepository collections,
            ILogger<ApproverFollowUpsController> logger)
        {
            _followUps = followUps;
            _collections = collections;
            _logger = logger;
        }

        [HttpGet("approver-follow-ups")]
        public async Task<IActionResult> Index(string status = ApproverFollowUpRules.Open)
        {
            if (!Allowed(out _, out var redirect)) return redirect!;

            status = status is ApproverFollowUpRules.ToCollections or ApproverFollowUpRules.Closed ? status : ApproverFollowUpRules.Open;
            var rows = await _followUps.ListAsync(status);
            var standings = rows.Count == 0 ? new()
                : (await _collections.GetStandingsAsync(rows.Select(r => r.AccountNo).ToList())).GroupBy(s => s.ContractId).ToDictionary(g => g.Key, g => g.First());

            ViewBag.Status = status;
            // Every note so far; until Collections/004 is run there is no history, only the latest.
            try { ViewBag.Notes = await _followUps.GetNotesAsync(rows.Select(r => r.Id).ToList()); }
            catch (Exception ex) { _logger.LogWarning(ex, "No follow-up note history (has Collections/004 been run?)"); }
            ViewBag.Message = TempData["FollowUpMessage"] as string;
            ViewBag.Error = TempData["FollowUpError"] as string;
            return View(rows.Select(r => (r, standings.GetValueOrDefault(r.ContractId))).ToList());
        }

        [HttpPost("approver-follow-ups/take")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Take(int id)
        {
            if (!Allowed(out var user, out var redirect)) return redirect!;
            if (!await _followUps.TakeAsync(id, user!.UserId)) TempData["FollowUpError"] = "That one isn't open any more.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost("approver-follow-ups/note")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Note(int id, string? note, DateTime? promised)
        {
            if (!Allowed(out var user, out var redirect)) return redirect!;
            var clean = CollectionsRules.CleanNote(note);
            if (clean == null)
            {
                TempData["FollowUpError"] = "Write what happened on the call.";
            }
            else if (!CollectionsRules.IsValidPromiseDate(promised, DateTime.UtcNow.AddHours(3).Date))
            {
                TempData["FollowUpError"] = "A promise to pay can't be in the past.";
            }
            else if (!await _followUps.NoteAsync(id, user!.UserId, clean, promised))
            {
                TempData["FollowUpError"] = "That one isn't open any more.";
            }
            else
            {
                TempData["FollowUpMessage"] = "Note saved.";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost("approver-follow-ups/to-collections")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToCollections(int id, long accountNo, string? note)
        {
            if (!Allowed(out var user, out var redirect)) return redirect!;
            if (await _followUps.SendToCollectionsAsync(id, user!.UserId, CollectionsRules.CleanNote(note)))
            {
                _logger.LogInformation("Approver follow-up {Id} ({Account}) sent to collections by {User}", id, accountNo, user.KnownAs);
                TempData["FollowUpMessage"] = $"{accountNo} sent to collections: it's back in the pool for an admin to give to a collector.";
            }
            else
            {
                TempData["FollowUpError"] = "That one isn't open any more.";
            }
            return RedirectToAction(nameof(Index));
        }

        private bool Allowed(out User? user, out IActionResult? redirect)
        {
            user = HttpContext.Items["UserSettings"] as User;
            redirect = user == null ? RedirectToAction("Index", "Login")
                : user.RoleId is not (UserRole.Admin or UserRole.Approver) ? RedirectToAction("Index", "Home") : null;
            if (redirect == null)
            {
                ViewBag.BackLink = "approver-follow-ups";
                ViewBag.IsAdmin = user!.RoleId == UserRole.Admin;
                ViewBag.IsApprover = user.RoleId == UserRole.Approver;
                ViewBag.IsDealer = false;
                ViewBag.UserName = user.KnownAs;
            }
            return redirect == null;
        }
    }
}
