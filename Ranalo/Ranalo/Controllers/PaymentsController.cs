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
        public PaymentsController(IPaymentsService paymentsService,
            IApplicationReportService applicationReportService,
            IUserService userService,
            IEnrolmentService enrolmentService)
        {
            _paymentsService = paymentsService;
            _applicationReportService = applicationReportService;
            _userService = userService;
            _enrolmentService = enrolmentService;
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

            try
            {
                var payments = RanaloXlsmUploadParser.Parse(file);

                if (payments.Any())
                {
                    var mapped = _paymentsService.MapXlsPayments(payments);
                    var results = await _paymentsService.CreatePayments(mapped);
                }
            }
            catch (Exception)
            {

                return RedirectToAction("AllPayments", "Payments");
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
