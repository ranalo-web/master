using Microsoft.AspNetCore.Mvc;
using Ranalo.DataStore;
using Ranalo.DataStore.DataModels;
using Ranalo.Models;
using Ranalo.Services;
using Ranalo.SumsungKnox;

namespace Ranalo.Controllers
{
    // Admin: one message to every customer (AnnouncementRules). Write it,
    // send a test to one account, then send to all -- in the background,
    // with progress on the page.
    public class AnnouncementsController : Controller
    {
        private readonly IAnnouncementsRepository _repo;
        private readonly IAnnouncementSender _sender;
        private readonly IPaymentReminderService _messages;
        private readonly IPaymentsRepository _payments;
        private readonly IKnoxGuardClient _knox;
        private readonly ILogger<AnnouncementsController> _logger;

        public AnnouncementsController(IAnnouncementsRepository repo, IAnnouncementSender sender,
            IPaymentReminderService messages, IPaymentsRepository payments, IKnoxGuardClient knox,
            ILogger<AnnouncementsController> logger)
        {
            _repo = repo;
            _sender = sender;
            _messages = messages;
            _payments = payments;
            _knox = knox;
            _logger = logger;
        }

        public class AnnouncementsPage
        {
            public string Title { get; set; } = AnnouncementRules.DefaultTitle;
            public string Body { get; set; } = AnnouncementRules.DefaultBody;
            public AnnouncementRecipientCounts Counts { get; set; } = new();
            public AnnouncementProgress Progress { get; set; } = null!;
            public List<AnnouncementHistory> History { get; set; } = new();
            public string? Preview { get; set; }
        }

        [HttpGet("announcements")]
        public async Task<IActionResult> Index()
        {
            if (!Allowed(out _, out var redirect)) return redirect!;
            var page = new AnnouncementsPage
            {
                Title = TempData["AnnTitle"] as string ?? AnnouncementRules.DefaultTitle,
                Body = TempData["AnnBody"] as string ?? AnnouncementRules.DefaultBody,
                Counts = await _repo.CountRecipientsAsync(),
                Progress = _sender.Progress,
                History = await _repo.HistoryAsync(),
            };
            page.Preview = AnnouncementRules.For(page.Title, page.Body, "Jane", 1234567).Full;
            ViewBag.Message = TempData["AnnMessage"] as string;
            ViewBag.Error = TempData["AnnError"] as string;
            return View(page);
        }

        [HttpGet("announcements/progress")]
        public IActionResult Progress()
        {
            if (!Allowed(out _, out var redirect)) return Unauthorized();
            return Json(_sender.Progress);
        }

        [HttpPost("announcements/test")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Test(string title, string body, long accountNo)
        {
            if (!Allowed(out var user, out var redirect)) return redirect!;
            Keep(title, body);
            var problem = AnnouncementRules.Problem(title, body);
            if (problem != null) { TempData["AnnError"] = problem; return RedirectToAction(nameof(Index)); }

            var account = await _repo.GetAccountAsync(accountNo);
            if (account == null) { TempData["AnnError"] = $"No contract found for account {accountNo}."; return RedirectToAction(nameof(Index)); }

            var message = AnnouncementRules.For(title, body, account.FirstName, account.AccountId);
            await _messages.SendToEachProviderAsync(new List<AccountSendMessage>
            {
                new()
                {
                    AccountId = account.AccountId, FirstName = account.FirstName ?? "", Imei = account.Imei, LockGroup = account.LockGroup,
                    MessageTitle = message.Title, MessageText = message.Content,
                },
            }, AnnouncementRules.TestType, _payments, _knox);

            _logger.LogInformation("Announcement test sent to {Account} by {User}.", accountNo, user!.KnownAs);
            TempData["AnnMessage"] = $"Test sent to account {accountNo}. Check the phone, and the account's messages, before sending to everyone.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost("announcements/send")]
        [ValidateAntiForgeryToken]
        public IActionResult Send(string title, string body, string confirm)
        {
            if (!Allowed(out var user, out var redirect)) return redirect!;
            Keep(title, body);
            var problem = AnnouncementRules.Problem(title, body);
            if (problem != null) { TempData["AnnError"] = problem; return RedirectToAction(nameof(Index)); }
            if (!string.Equals(confirm?.Trim(), "SEND", StringComparison.Ordinal))
            {
                TempData["AnnError"] = "Type SEND in the box to confirm sending to everyone.";
                return RedirectToAction(nameof(Index));
            }

            if (_sender.TryStart(title, body, user!.KnownAs ?? user.UserId.ToString()))
                TempData["AnnMessage"] = "Sending to everyone has started. This page shows the progress.";
            else
                TempData["AnnError"] = "An announcement is already being sent. Wait for it to finish.";
            return RedirectToAction(nameof(Index));
        }

        private void Keep(string title, string body)
        {
            TempData["AnnTitle"] = title;
            TempData["AnnBody"] = body;
        }

        private bool Allowed(out User? user, out IActionResult? redirect)
        {
            user = HttpContext.Items["UserSettings"] as User;
            redirect = user == null ? RedirectToAction("Index", "Login")
                : user.RoleId != UserRole.Admin ? RedirectToAction("Index", "Home") : null;
            if (redirect == null)
            {
                ViewBag.BackLink = "announcements";
                ViewBag.IsAdmin = true;
                ViewBag.IsApprover = false;
                ViewBag.IsDealer = false;
                ViewBag.UserName = user!.KnownAs;
            }
            return redirect == null;
        }
    }
}
