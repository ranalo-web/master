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
        public PaymentsController(IPaymentsService paymentsService,
            IApplicationReportService applicationReportService,
            IUserService userService)
        {
            _paymentsService = paymentsService;
            _applicationReportService = applicationReportService;
            _userService = userService;
        }

        [HttpPost("upload-payments")]
        public async Task<IActionResult> UploadStatement(IFormFile file)
        {
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
        public async Task<IActionResult> AllPayments(string searchTerm = "", int page = 1, int pageSize = 10)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            await SetViewBags(settings, "index", searchTerm.Trim());

            if (settings.RoleId == UserRole.Admin || settings.RoleId == UserRole.Approver)
            {
                var allPayments = await _applicationReportService.GetAllPaymentsAsync(null, searchTerm.Trim(), page: page, pageSize: pageSize);

                return View(allPayments);
            }

            var allPaymentsByUser = await _applicationReportService.GetAllPaymentsAsync(settings.UserId, page: page, pageSize: pageSize);

            return View(allPaymentsByUser);
        }

        [Route("paymentsummary")]
        public async Task<IActionResult> PaymentSummary()
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }
            await SetViewBags(settings, "index");

            if (settings.RoleId == UserRole.Admin)
            {
                var allPayments = await _applicationReportService.PaymentsSummary();

                return View(allPayments);
            }

            return RedirectToAction("Index", "Login");
        }

        [HttpPost]
        [Route("allpayments")]
        public async Task<IActionResult> Search(string searchTerm = "", int page = 1, int pageSize = 10)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            await SetViewBags(settings, "index", searchTerm.Trim());

            if (settings.RoleId == UserRole.Admin || settings.RoleId == UserRole.Approver)
            {
                var allPayments = await _applicationReportService.GetAllPaymentsAsync(null, searchTerm.Trim(), page, pageSize);

                return View("AllPayments", allPayments);
            }

            var allPaymentsByUser = await _applicationReportService.GetAllPaymentsAsync(settings.UserId, searchTerm.Trim());

            return View("AllPayments", allPaymentsByUser);
        }

        [Route("orphanedpayments/{page:int?}")]
        public async Task<IActionResult> OrphanedPayments(int page = 1, int pageSize = 10)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }
            await SetViewBags(settings, "index");

            var orphanedPayments = await _applicationReportService.GetOrphanedPaymentsAsync(page, pageSize);

            return View(orphanedPayments);
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
