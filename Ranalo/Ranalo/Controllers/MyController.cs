using System.Collections.Concurrent;
using Microsoft.AspNetCore.Mvc;
using Ranalo.DataStore;
using Ranalo.Models;
using Ranalo.Services;
using Ranalo.Services.DeviceLock;
using Ranalo.SumsungKnox;

namespace Ranalo.Controllers
{
    // The customer app (my.ranalocredit.com, also /my on the staff site):
    // sign in with the account number and a code sent to the financed
    // phone; then the account, statement, paying and their details.
    // Everything is the signed-in account's own -- there is no way to name
    // another account once signed in.
    public class MyController : Controller
    {
        // Per network address: code requests and code tries an hour, on top
        // of the per-account limits (CustomerLoginRules).
        private const int MaxPerIpPerHour = 30;
        private static readonly ConcurrentDictionary<string, List<DateTime>> IpHits = new();

        private readonly ICustomerAccessRepository _access;
        private readonly ICustomerSession _session;
        private readonly IPaymentReminderService _messages;
        private readonly IPaymentsRepository _paymentsRepository;
        private readonly IKnoxGuardClient _knox;
        private readonly IApplicationReportService _reports;
        private readonly IMpesaStkClient _stk;
        private readonly ICompletedContractRepository _completed;
        private readonly ISelfRestructureService _selfPlans;
        private readonly ISelfRestructureRepository _selfPlanStore;
        private readonly IConfiguration _config;
        private readonly ILogger<MyController> _logger;

        public MyController(ICustomerAccessRepository access, ICustomerSession session, IPaymentReminderService messages,
            IPaymentsRepository paymentsRepository, IKnoxGuardClient knox, IApplicationReportService reports,
            IMpesaStkClient stk, ICompletedContractRepository completed, ISelfRestructureService selfPlans,
            ISelfRestructureRepository selfPlanStore, IConfiguration config, ILogger<MyController> logger)
        {
            _access = access;
            _session = session;
            _messages = messages;
            _paymentsRepository = paymentsRepository;
            _knox = knox;
            _reports = reports;
            _stk = stk;
            _completed = completed;
            _selfPlans = selfPlans;
            _selfPlanStore = selfPlanStore;
            _config = config;
            _logger = logger;
        }

        private string SupportEmail => _config["CustomerApp:SupportEmail"] ?? "support@ranalocredit.com";

        public override void OnActionExecuting(Microsoft.AspNetCore.Mvc.Filters.ActionExecutingContext context)
        {
            ViewBag.SupportEmail = SupportEmail;
            ViewBag.HelpLine = CustomerMessages.HelpLine;
            Response.Headers["Cache-Control"] = "no-store";
            base.OnActionExecuting(context);
        }

        // ---------- Sign in ----------

        [HttpGet("my/login")]
        public IActionResult Login() => View(new MyLoginForm());

        [HttpPost("my/login")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(MyLoginForm form)
        {
            if (TooManyFromHere()) return View(form.With("Too many tries from this network. Wait a while and try again."));

            var accountNo = CustomerLoginRules.AccountNo(form.AccountNo);
            var target = accountNo == null ? null : await _access.GetTargetAsync(accountNo.Value);
            if (target == null)
            {
                return View(form.With("We couldn't find that account number. It's on your M-Pesa receipts and Ranalo messages."));
            }

            var access = await _access.GetOrCreateAsync(target.AccountNo, target);
            if (access.Locked) return RedirectToAction(nameof(Locked));

            var now = DateTime.UtcNow;
            var wait = FailureWait(access, now);
            if (wait > TimeSpan.Zero) return View(form.With($"Too many wrong codes. Try again in {CustomerLoginRules.Wait(wait)}."));

            var sendWait = CustomerLoginRules.SendWait(await _access.CodesSentSinceAsync(target.AccountNo, now.AddHours(-1)), now);
            if (sendWait > TimeSpan.Zero)
            {
                TempData["MyMessage"] = $"A code was sent already. You can ask for another in {CustomerLoginRules.Wait(sendWait)}.";
                return RedirectToAction(nameof(Code), new { account = target.AccountNo });
            }

            if (!await SendCodeAsync(target, now))
            {
                return View(form.With("We couldn't reach your phone. Make sure it's switched on and has data or Wi-Fi, then try again."));
            }
            return RedirectToAction(nameof(Code), new { account = target.AccountNo });
        }

        [HttpGet("my/code")]
        public IActionResult Code(long account) =>
            View(new MyCodeForm { Account = account, Message = TempData["MyMessage"] as string });

        [HttpPost("my/code")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Code(MyCodeForm form)
        {
            if (TooManyFromHere()) return View(form.With("Too many tries from this network. Wait a while and try again."));

            var target = await _access.GetTargetAsync(form.Account);
            if (target == null) return RedirectToAction(nameof(Login));
            var access = await _access.GetOrCreateAsync(form.Account, target);
            if (access.Locked) return RedirectToAction(nameof(Locked));

            var now = DateTime.UtcNow;
            var wait = FailureWait(access, now);
            if (wait > TimeSpan.Zero) return View(form.With($"Too many wrong codes. Try again in {CustomerLoginRules.Wait(wait)}."));

            var live = await _access.LatestLiveCodeAsync(form.Account, now);
            if (live == null) return View(form.With("That code has run out. Ask for a new one."));

            var typed = CustomerLoginRules.CleanCode(form.Code);
            if (typed != null && PasswordRules.Verify(live.CodeHash, typed).Ok)
            {
                await _access.MarkUsedAsync(live.Id);
                await _access.RecordLoginAsync(form.Account);
                _session.EndPreview(Response);
                _session.SignIn(Response, form.Account, access.SessionStamp);
                _logger.LogInformation("Customer app sign-in: {Account}", form.Account);
                return RedirectToAction(nameof(Index));
            }

            var failures = await _access.RecordFailureAsync(form.Account);
            if (CustomerLoginRules.Locks(failures))
            {
                await _access.LockAsync(form.Account, $"{failures} wrong codes in a row");
                _logger.LogWarning("Customer app access locked after {Failures} wrong codes: {Account}", failures, form.Account);
                return RedirectToAction(nameof(Locked));
            }

            var next = CustomerLoginRules.WaitAfter(failures);
            return View(form.With(next > TimeSpan.Zero
                ? $"Wrong code. Wait {CustomerLoginRules.Wait(next)} before trying again. After {CustomerLoginRules.LockAfterFailures} wrong codes the app locks."
                : "Wrong code. Check the message on your phone and try again."));
        }

        [HttpPost("my/code/resend")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Resend(long account) =>
            await Login(new MyLoginForm { AccountNo = account.ToString() });

        [HttpGet("my/locked")]
        public IActionResult Locked() => View();

        // Public (no sign-in): the Play Store links to it, and to its
        // "delete your app access" section, from the app's listing.
        [HttpGet("my/privacy")]
        public IActionResult Privacy() => View();

        [HttpPost("my/logout")]
        [ValidateAntiForgeryToken]
        public IActionResult Logout()
        {
            if (Request.Cookies.ContainsKey(CustomerSession.PreviewCookieName))
            {
                _session.EndPreview(Response);
                return Redirect("/customer-app");
            }
            _session.SignOut(Response);
            return RedirectToAction(nameof(Login));
        }

        // ---------- Signed in ----------

        [HttpGet("my")]
        public async Task<IActionResult> Index()
        {
            var (access, redirect) = await SignedInAsync();
            if (redirect != null) return redirect;

            // A plan they chose and have just paid for starts now, not at the next job run.
            if (!Previewing) await _selfPlans.ProcessPendingAsync(access!.AccountNo);

            var view = await AccountAsync(access!.AccountNo);
            if (view == null) return View("NoContract");
            ViewBag.Missing = CustomerAppRules.Missing(access);
            ViewBag.SelfPlan = await _selfPlanStore.GetLatestAsync(access.AccountNo);
            ViewBag.CanSelfRestructure = SelfRestructureRules.Refusal(await _selfPlans.FactsAsync(view)) == null;
            return View(view);
        }

        // "Get back on track": choose one of our plans; it starts once the
        // first day is paid (SelfRestructureRules).
        [HttpGet("my/plan")]
        public async Task<IActionResult> Plan()
        {
            var (access, redirect) = await SignedInAsync(needsDetails: true);
            if (redirect != null) return redirect;
            if (!Previewing) await _selfPlans.ProcessPendingAsync(access!.AccountNo);

            var view = await AccountAsync(access!.AccountNo);
            if (view == null) return View("NoContract");
            return View(await PlanFormAsync(view, TempData["MyPlanMessage"] as string));
        }

        [HttpPost("my/plan")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Plan(string? option)
        {
            var (access, redirect) = await SignedInAsync(needsDetails: true);
            if (redirect != null) return redirect;

            var view = await AccountAsync(access!.AccountNo);
            if (view == null) return View("NoContract");
            if (Previewing) return View(await PlanFormAsync(view, "Staff preview: no plan is set up from here."));

            var (ok, message) = await _selfPlans.ChooseAsync(view, option);
            if (!ok) return View(await PlanFormAsync(view, message));
            TempData["MyPlanMessage"] = message;
            return RedirectToAction(nameof(Plan));
        }

        private async Task<MyPlanForm> PlanFormAsync(MyAccountView view, string? message)
        {
            var facts = await _selfPlans.FactsAsync(view);
            return new MyPlanForm
            {
                Account = view,
                Latest = await _selfPlanStore.GetLatestAsync(view.AccountNo),
                Refusal = SelfRestructureRules.Refusal(facts),
                Options = SelfRestructureRules.Options(facts),
                Message = message,
            };
        }

        [HttpGet("my/statement")]
        public async Task<IActionResult> Statement()
        {
            var (access, redirect) = await SignedInAsync(needsDetails: true);
            if (redirect != null) return redirect;

            var view = await AccountAsync(access!.AccountNo);
            if (view == null) return View("NoContract");
            ViewBag.Account = view;
            return View(CustomerAppRules.Statement(await _access.GetPaymentsAsync(access.AccountNo), view.Owed));
        }

        // Every message we've sent the phone, so nothing is lost if the
        // phone's notification was missed or cleared.
        [HttpGet("my/inbox")]
        public async Task<IActionResult> Inbox()
        {
            var (access, redirect) = await SignedInAsync();
            if (redirect != null) return redirect;

            // Opening the Inbox sees everything; what was new is still marked as new on this visit.
            ViewBag.SeenBefore = access!.InboxSeenUtc;
            if (!Previewing)
            {
                try { await _access.MarkInboxSeenAsync(access.AccountNo); } catch (Exception ex) { _logger.LogWarning(ex, "Inbox seen not saved (has Customers/005 been run?)"); }
            }
            ViewBag.Unread = 0;

            var target = await _access.GetTargetAsync(access.AccountNo);
            var messages = await _access.GetMessagesAsync(access.AccountNo, target?.Imei, 100);
            return View(messages.Select(CustomerAppRules.InboxItem).ToList());
        }

        [HttpGet("my/pay")]
        public async Task<IActionResult> Pay()
        {
            var (access, redirect) = await SignedInAsync(needsDetails: true);
            if (redirect != null) return redirect;

            var view = await AccountAsync(access!.AccountNo);
            // A chosen plan waiting for its first day: that amount starts it.
            var pendingPlan = await _selfPlanStore.GetLatestAsync(access.AccountNo) is { Status: SelfRestructureStatus.Pending } p ? p : null;
            if (view == null) return View("NoContract");
            return View(new MyPayForm
            {
                Account = view,
                Phone = access.Phone,
                // Locked: what opens it for 24 hours; behind: catch up; else a day.
                Amount = pendingPlan != null ? pendingPlan.Daily
                    : view.Locked && view.UnlockFor24h is > 0 ? view.UnlockFor24h.Value
                    : view.Behind > 0 ? Math.Ceiling(view.Behind) : Math.Ceiling(view.Daily),
                CanPrompt = _stk.Ready,
            });
        }

        [HttpPost("my/pay")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Pay(MyPayForm form)
        {
            var (access, redirect) = await SignedInAsync(needsDetails: true);
            if (redirect != null) return redirect;

            var view = await AccountAsync(access!.AccountNo);
            if (view == null) return View("NoContract");
            form.Account = view;
            form.CanPrompt = _stk.Ready;

            if (Previewing)
            {
                form.Message = "Staff preview: nothing is sent to the customer.";
                return View(form);
            }

            // Always the signed-in account: the form can't name another.
            var result = await _stk.RequestAsync(access.AccountNo, form.Phone ?? access.Phone ?? "", form.Amount);
            form.Sent = result.Sent;
            form.Message = result.Message;
            return View(form);
        }

        [HttpGet("my/details")]
        public async Task<IActionResult> Details()
        {
            var (access, redirect) = await SignedInAsync();
            if (redirect != null) return redirect;
            return View(new MyDetailsForm { Email = access!.Email, Phone = access.Phone, NationalId = access.NationalId, Missing = CustomerAppRules.Missing(access) });
        }

        [HttpPost("my/details")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Details(MyDetailsForm form)
        {
            var (access, redirect) = await SignedInAsync();
            if (redirect != null) return redirect;

            if (Previewing)
            {
                form.Problems = new() { "Staff preview: the customer's details can't be changed from here." };
                return View(form);
            }

            var email = CustomerAppRules.CleanEmail(form.Email);
            var phone = StkRules.Phone(form.Phone);
            var id = CustomerAppRules.CleanNationalId(form.NationalId);
            var problems = new List<string>();
            if (email == null) problems.Add("Enter a valid email address.");
            if (phone == null) problems.Add("Enter your Safaricom M-Pesa number, like 0712 345 678.");
            if (id == null) problems.Add("Enter your national ID number.");
            if (problems.Any())
            {
                form.Problems = problems;
                return View(form);
            }

            await _access.SaveDetailsAsync(access!.AccountNo, email, phone, id);
            TempData["MySaved"] = "Your details are saved.";
            return RedirectToAction(nameof(Index));
        }

        // ---------- Helpers ----------

        // Admin or Approver signed in to the staff site and previewing an
        // account (Customer App page): its pages, read-only. Nothing is
        // created for the account and nothing can be paid or saved.
        private bool Previewing => ViewBag.Preview is true;

        private async Task<CustomerAccess?> PreviewAccessAsync()
        {
            var staff = HttpContext.Items["UserSettings"] as Ranalo.DataStore.DataModels.User;
            if (staff == null || !CustomerAppAccessController.CanManage(staff.RoleId)) return null;

            var account = _session.ReadPreview(Request, staff.UserId);
            if (account == null) return null;

            var target = await _access.GetTargetAsync(account.Value);
            if (target == null) return null;

            ViewBag.Preview = true;
            ViewBag.PreviewBy = staff.KnownAs;
            return await _access.GetAsync(account.Value) ?? new CustomerAccess
            {
                AccountNo = account.Value,
                Email = target.OrderEmail,
                Phone = StkRules.Phone(target.OrderPhone),
                NationalId = target.OrderNationalId,
            };
        }

        private async Task<(CustomerAccess? Access, IActionResult? Redirect)> SignedInAsync(bool needsDetails = false)
        {
            var preview = await PreviewAccessAsync();
            if (preview != null) return (preview, null);

            var signedIn = _session.Read(Request);
            if (signedIn == null) return (null, RedirectToAction(nameof(Login)));

            var access = await _access.GetOrCreateAsync(signedIn.Value.AccountNo, null);
            if (access.Locked || access.SessionStamp != signedIn.Value.Stamp)
            {
                _session.SignOut(Response);
                return (null, access.Locked ? RedirectToAction(nameof(Locked)) : RedirectToAction(nameof(Login)));
            }

            // Keeps an app in use signed in.
            _session.SignIn(Response, access.AccountNo, access.SessionStamp);

            await SetNoticesAsync(access);
            if (needsDetails && CustomerAppRules.Missing(access).Any()) return (access, RedirectToAction(nameof(Details)));
            return (access, null);
        }

        // New messages since the Inbox was last opened: the count on the
        // Inbox tab and the pop-up. A first visit counts the last 7 days.
        private async Task<(int Count, MessageLog? Newest)> NewMessagesAsync(CustomerAccess access)
        {
            try
            {
                var target = await _access.GetTargetAsync(access.AccountNo);
                return await _access.GetNewMessagesAsync(access.AccountNo, target?.Imei, access.InboxSeenUtc ?? DateTime.UtcNow.AddDays(-7));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Couldn't count new messages for {Account}", access.AccountNo);
                return (0, null);
            }
        }

        private async Task SetNoticesAsync(CustomerAccess access)
        {
            var (count, newest) = await NewMessagesAsync(access);
            ViewBag.Unread = count;
            if (newest != null)
            {
                var item = CustomerAppRules.InboxItem(newest);
                ViewBag.NewestNotice = new { item.Title, item.Kind, At = newest.DateSent?.Ticks ?? 0 };
            }
        }

        // Checked by the app every minute while it's open: a pop-up when a
        // new message has been saved since the page loaded.
        [HttpGet("my/notifications")]
        public async Task<IActionResult> Notifications()
        {
            var signedIn = _session.Read(Request);
            if (signedIn == null) return Json(new { unread = 0 });
            var access = await _access.GetAsync(signedIn.Value.AccountNo);
            if (access == null || access.Locked || access.SessionStamp != signedIn.Value.Stamp) return Json(new { unread = 0 });

            var (count, newest) = await NewMessagesAsync(access);
            if (newest == null) return Json(new { unread = count });
            var item = CustomerAppRules.InboxItem(newest);
            return Json(new { unread = count, title = item.Title, kind = item.Kind, at = newest.DateSent?.Ticks ?? 0 });
        }

        private async Task<MyAccountView?> AccountAsync(long accountNo)
        {
            var report = await _reports.GetStatusReportByDealer((int)accountNo, null, 1, ReportPaging.All, "");
            var record = report?.StatusReports?.FirstOrDefault(r => r.AccountNo == accountNo);
            var device = await _access.GetDeviceAsync(accountNo);
            if (record == null)
            {
                // No open contract: paid off shows as fully paid; anything
                // else (written off, repossessed) is for staff to explain.
                var contractId = await _access.GetLatestEndedContractIdAsync(accountNo);
                var ended = contractId == null ? null : await _completed.GetAsync(contractId.Value);
                if (ended == null) return null;
                CompletedContractRules.Evaluate(ended);
                return ended.Outcome == CompletedOutcome.PaidOff ? CustomerAppRules.PaidOff(ended, device) : null;
            }

            // The lock date last sent to the phone when known, else the provider's.
            var sent = LockDateBook.Get(accountNo);
            var lockUtc = sent != null && DateTime.UtcNow - sent.SentAtUtc <= PaymentReminderService.LockDateBookMaxAge
                ? sent.LockAtUtc
                : FullyPaidPlanner.ParseLockDate((await _access.GetTargetAsync(accountNo))?.DeviceNextLock ?? record.NextLockDate);

            var plan = (await _reports.GetAllRestructured("", 1, ReportPaging.All))?.Records?.FirstOrDefault(r => r.AccountNo == accountNo);
            return CustomerAppRules.Account(record, lockUtc, device, DateTime.UtcNow, plan);
        }

        private async Task<bool> SendCodeAsync(CustomerPhoneTarget target, DateTime now)
        {
            var code = CustomerLoginRules.NewCode();
            var sent = await _messages.SendLoginCodeAsync(new AccountSendMessage
            {
                AccountId = target.AccountNo,
                FirstName = target.FirstName,
                Imei = target.Imei,
                LockGroup = target.LockGroup,
            }, code, _paymentsRepository, _knox);

            await _access.AddCodeAsync(new CustomerLoginCode
            {
                AccountNo = target.AccountNo,
                CodeHash = PasswordRules.Hash(code),
                SentAtUtc = now,
                ExpiresAtUtc = now.Add(CustomerLoginRules.CodeLifetime),
                Sent = sent,
            }, HttpContext.Connection.RemoteIpAddress?.ToString());

            if (!sent) _logger.LogWarning("Customer app code not delivered to {Account} (LockGroup {Group})", target.AccountNo, target.LockGroup);
            return sent;
        }

        private static TimeSpan FailureWait(CustomerAccess access, DateTime nowUtc)
        {
            if (access.LastFailedUtc == null) return TimeSpan.Zero;
            var until = access.LastFailedUtc.Value + CustomerLoginRules.WaitAfter(access.FailedCodes);
            return until > nowUtc ? until - nowUtc : TimeSpan.Zero;
        }

        private bool TooManyFromHere()
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var now = DateTime.UtcNow;
            var hits = IpHits.GetOrAdd(ip, _ => new List<DateTime>());
            lock (hits)
            {
                hits.RemoveAll(t => t < now.AddHours(-1));
                if (hits.Count >= MaxPerIpPerHour) return true;
                hits.Add(now);
                return false;
            }
        }
    }

    public class MyLoginForm
    {
        public string? AccountNo { get; set; }
        public string? Message { get; set; }
        public MyLoginForm With(string message) { Message = message; return this; }
    }

    public class MyCodeForm
    {
        public long Account { get; set; }
        public string? Code { get; set; }
        public string? Message { get; set; }
        public MyCodeForm With(string message) { Message = message; Code = null; return this; }
    }

    public class MyPayForm
    {
        public MyAccountView Account { get; set; } = new();
        public string? Phone { get; set; }
        public decimal Amount { get; set; }
        public bool CanPrompt { get; set; }
        public bool Sent { get; set; }
        public string? Message { get; set; }
    }

    public class MyPlanForm
    {
        public MyAccountView Account { get; set; } = new();
        public SelfRestructure? Latest { get; set; }
        public string? Refusal { get; set; }
        public List<SelfRestructureRules.Option> Options { get; set; } = new();
        public string? Message { get; set; }
        public bool Waiting => Latest?.Status == SelfRestructureStatus.Pending;
    }

    public class MyDetailsForm
    {
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? NationalId { get; set; }
        public List<string> Missing { get; set; } = new();
        public List<string> Problems { get; set; } = new();
    }
}
