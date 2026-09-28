using Microsoft.AspNetCore.Mvc;
using Ranalo.DataStore.DataModels;
using Ranalo.Services;

namespace Ranalo.Controllers
{
    public class PaymentsController : Controller
    {
        private readonly IPaymentsService _paymentsService;
        private readonly IApplicationReportService _applicationReportService;
        private readonly IUserService _userService;
        private readonly IEnrolmentService _enrolmentService;
        private readonly ILogger<PaymentsController> _logger;
        public PaymentsController(IPaymentsService paymentsService,
            IApplicationReportService applicationReportService,
            IUserService userService,
            IEnrolmentService enrolmentService,
            ILogger<PaymentsController> logger)
        {
            _paymentsService = paymentsService;
            _applicationReportService = applicationReportService;
            _userService = userService;
            _enrolmentService = enrolmentService;
            _logger = logger;
        }

        [HttpPost("upload-payments")]
        public async Task<IActionResult> UploadStatement(IFormFile file)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            // Importing a payments statement is Admin-only -- every other
            // role (including Approver/Dealer/Agent) only gets read access
            // to Payments.
            if (settings.RoleId != UserRole.Admin)
            {
                return RedirectToAction("AllPayments", "Payments");
            }

            // The result is shown as a banner on All Payments after the
            // redirect -- previously every outcome (success, nothing new,
            // or a swallowed exception) looked identical to the user.
            if (file == null || file.Length == 0)
            {
                TempData["ImportError"] = "No file was selected. Choose a statement and click Save.";
                return RedirectToAction("AllPayments", "Payments");
            }

            // ClosedXML only reads Open XML workbooks; a legacy .xls M-Pesa
            // export fails with "File contains corrupted data".
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (extension != ".xlsx" && extension != ".xlsm")
            {
                TempData["ImportError"] = $"'{file.FileName}' is not supported. Open it in Excel, Save As \"Excel Workbook (.xlsx)\" and upload that.";
                return RedirectToAction("AllPayments", "Payments");
            }

            try
            {
                var payments = RanaloXlsmUploadParser.Parse(file);
                var mapped = payments.Any() ? _paymentsService.MapXlsPayments(payments) : new();
                var inserted = mapped.Any() ? (await _paymentsService.CreatePayments(mapped))?.Count ?? 0 : 0;

                // MapXlsPayments drops rows whose account number isn't
                // numeric; list their receipts so they can be followed up.
                var skipped = payments
                    .Where(p => !long.TryParse(p.AccountNumber, out _))
                    .Select(p => $"{p.ReceiptNo} ('{p.AccountNumber}')")
                    .ToList();

                _logger.LogInformation("Payments import {FileName}: {Read} read, {Valid} valid, {Inserted} new, {Skipped} skipped",
                    file.FileName, payments.Count, mapped.Count, inserted, skipped.Count);

                TempData["ImportSuccess"] = $"Imported '{file.FileName}': {payments.Count:N0} rows read, {inserted:N0} new payments added, " +
                    $"{mapped.Count - inserted:N0} already recorded, {skipped.Count:N0} skipped (invalid account number).";
                if (skipped.Any())
                {
                    TempData["ImportSkipped"] = string.Join(", ", skipped.Take(50)) + (skipped.Count > 50 ? $" and {skipped.Count - 50} more" : "");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Payments import failed for {FileName}", file.FileName);
                TempData["ImportError"] = $"Import of '{file.FileName}' failed: {ex.Message}";
            }

            return RedirectToAction("AllPayments", "Payments");
        }

        [Route("allpayments/{page:int?}")]
        public async Task<IActionResult> AllPayments(string searchTerm = "", int page = 1, int pageSize = 10, string period = "")
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            await SetViewBags(settings, "index", searchTerm.Trim());
            ViewBag.Period = period;
            var (fromDate, toDateExclusive) = ResolvePeriod(period);

            //await _enrolmentService.SendReminderMessage("359063757998542");

            if (settings.RoleId == UserRole.Admin || settings.RoleId == UserRole.Approver)
            {
                var allPayments = await _applicationReportService.GetAllPaymentsAsync(null, searchTerm.Trim(), page, pageSize, fromDate, toDateExclusive);

                return View(allPayments);
            }

            // GetAllPaymentsAsync(int userId, ...) resolves the dealer for
            // this user and joins through Devices/Dealers (the correct
            // dealer-scoped query, same as the POST Search action below) --
            // GetAllPaymentAccountsByUserIdAsync filtered on
            // Contract_Info.AssignedAgentId, which is the collections agent
            // assigned to an account, not this dealer, so it returned no
            // rows for a real dealer user. That userId overload internally
            // looks up Dealers.UserId, which never resolves for an Agent (not
            // a Dealer themselves) -- their own Users.DealerId already names
            // the dealer they belong to, so use the dealerId overload above
            // directly instead, same as the Admin/Approver branch.
            if (settings.RoleId == UserRole.Agent)
            {
                var agentPayments = await _applicationReportService.GetAllPaymentsAsync((int?)settings.DealerId, searchTerm.Trim(), page, pageSize, fromDate, toDateExclusive, agentUserId: settings.UserId);
                return View(agentPayments);
            }

            var allPaymentsByUser = await _applicationReportService.GetAllPaymentsAsync(settings.UserId, searchTerm.Trim(), page, pageSize, fromDate, toDateExclusive);

            return View(allPaymentsByUser);
        }

        [Route("paymentsummary")]
        public async Task<IActionResult> PaymentSummary(string searchTerm = "", int page = 1, int pageSize = 10, string period = "")
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }
            await SetViewBags(settings, "index", searchTerm.Trim());
            ViewBag.Period = period;

            if (settings.RoleId == UserRole.Admin)
            {
                var (fromDate, toDateExclusive) = ResolvePeriod(period);
                var allPayments = await _applicationReportService.PaymentsSummary(searchTerm.Trim(), page, pageSize, fromDate, toDateExclusive);

                return View(allPayments);
            }

            return RedirectToAction("Index", "Login");
        }

        [HttpPost]
        [Route("allpayments")]
        public async Task<IActionResult> Search(string searchTerm = "", int page = 1, int pageSize = 10, string period = "")
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            await SetViewBags(settings, "index", searchTerm.Trim());
            ViewBag.Period = period;
            var (fromDate, toDateExclusive) = ResolvePeriod(period);

            if (settings.RoleId == UserRole.Admin || settings.RoleId == UserRole.Approver)
            {
                var allPayments = await _applicationReportService.GetAllPaymentsAsync(null, searchTerm.Trim(), page, pageSize, fromDate, toDateExclusive);

                return View("AllPayments", allPayments);
            }

            if (settings.RoleId == UserRole.Agent)
            {
                var agentPayments = await _applicationReportService.GetAllPaymentsAsync((int?)settings.DealerId, searchTerm.Trim(), page, pageSize, fromDate, toDateExclusive, agentUserId: settings.UserId);
                return View("AllPayments", agentPayments);
            }

            var allPaymentsByUser = await _applicationReportService.GetAllPaymentsAsync(settings.UserId, searchTerm.Trim(), page, pageSize, fromDate, toDateExclusive);

            return View("AllPayments", allPaymentsByUser);
        }

        private static (DateTime? FromDate, DateTime? ToDateExclusive) ResolvePeriod(string period) =>
            PeriodWindowHelper.Resolve(period);

        [Route("orphanedpayments/{page:int?}")]
        public async Task<IActionResult> OrphanedPayments(string searchTerm = "",  int page = 1, int pageSize = 10)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }
            await SetViewBags(settings, "index");

            var orphanedPayments = await _applicationReportService.GetOrphanedPaymentsAsync(page, pageSize, searchTerm);

            return View(orphanedPayments);
        }

        [HttpPost]
        [Route("orphanedpayments")]
        public async Task<IActionResult> OrphanedPaymentsSearch(string searchTerm = "", int page = 1, int pageSize = 10)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }
            await SetViewBags(settings, "index");

            var orphanedPayments = await _applicationReportService.GetOrphanedPaymentsAsync(page, pageSize, searchTerm);

            return View("OrphanedPayments", orphanedPayments);
        }

        [Route("assignedpayments/{page:int?}")]
        public async Task<IActionResult> AssignedPayments(string searchTerm = "", int page = 1, int pageSize = 10)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }
            await SetViewBags(settings, "index");

            var assignedPayments = await _applicationReportService.GetAssignedPaymentsAsync(searchTerm.Trim(), page, pageSize);

            return View(assignedPayments);
        }

        [HttpPost]
        [Route("assign-payments")]
        public async Task<IActionResult> CreateAssignedPayments(string orphanedNo, string mpesaCode, string accountNo)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }
            await SetViewBags(settings, "index");

            await _applicationReportService.CreateAssignedPaymentsAsync(orphanedNo, mpesaCode, accountNo);

            return RedirectToAction("AssignedPayments", "Payments");
        }

        private async Task SetViewBags(User settings, string backLink, string searchTerm = "")
        {
            ViewBag.BackLink = backLink;
            ViewBag.IsAdmin = settings.RoleId == UserRole.Admin;
            ViewBag.IsApprover = settings.RoleId == UserRole.Approver;
            ViewBag.IsDealer = settings.RoleId == UserRole.Dealer;
            ViewBag.IsAgent = settings.RoleId == UserRole.Agent;
            ViewBag.UserName = settings.KnownAs;
            ViewBag.SearchTerm = searchTerm.Trim();
            if (settings.RoleId == UserRole.Dealer)
            {
                var dealer = await _userService.GetDealerByUserId(settings.UserId);
                ViewBag.UserName = dealer.CompanyName;
                settings.DealerId = dealer.DealerId;
            }
        }

    }
}
