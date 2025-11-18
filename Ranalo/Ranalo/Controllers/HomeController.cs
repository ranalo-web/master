using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using Microsoft.AspNetCore.Mvc;
using Org.BouncyCastle.Asn1.X509.Qualified;
using Ranalo.Configuration;
using Ranalo.DataStore;
using Ranalo.DataStore.DataModels;
using Ranalo.DataStore.MySql;
using Ranalo.Models;
using Ranalo.Services;
using System.Drawing.Printing;
using System.IO;
using System.Reflection.PortableExecutable;

namespace Ranalo.Controllers
{
    [LoadUserSettingsFromCookie]
    public class HomeController : Controller
    {
        private readonly IApplicationReportService _applicationReportService;
        private readonly IUserService _userService;
        private readonly IStatementService _statementService;
        private readonly IMySqlPaymentsRepository _mysqlRepository;
        public HomeController(IApplicationReportService applicationReportService, 
            IUserService userService, 
            IStatementService statementService,
            IMySqlPaymentsRepository mysqlRepository)
        {
            _mysqlRepository = mysqlRepository;
            _applicationReportService = applicationReportService;
            _userService = userService;
            _statementService = statementService;
        }

        [HttpGet]
        [Route("orders/{page:int?}")]
        public async Task<IActionResult> Index(int page = 1, int pageSize = 10)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            await SetViewBags(settings, "index");

            if (settings.RoleId == UserRole.Admin)
            {
                var allAwaitngApproval = await _applicationReportService.GetAwaitingApprovalOrders(page:page, pageSize:pageSize);
                ViewData["OrdersStatus"] = "Waiting Approval";
                
                return View("~/Views/Home/Index.cshtml", allAwaitngApproval);
            }

            if(settings.RoleId == UserRole.Approver)
            {
                return RedirectToAction("Index", "Approver");
            }

            var dealer = await _userService.GetDealerByUserId(settings.UserId);
            var waitingApprovalByUser = await _applicationReportService.GetAwaitingApprovalOrdersByUser(settings.UserId, page:page, pageSize:pageSize);
            ViewData["OrdersStatus"] = "All Orders";
            return View(waitingApprovalByUser);

        }

        [HttpGet]
        [Route("never-paid/{page:int?}")]
        public async Task<IActionResult> NeverPaid(int page = 1, int pageSize = 10)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }
            await SetViewBags(settings, "index");

            if (settings.RoleId == UserRole.Admin)
            {
                var neverPaid = await _applicationReportService.GetAllNeverPaidOrdersAsync(page: page, pageSize: pageSize);
                ViewData["OrdersStatus"] = "Waiting Approval";

                return View("~/Views/Reports/NeverPaid.cshtml", neverPaid);
            }

            if (settings.RoleId == UserRole.Approver)
            {
                return RedirectToAction("Index", "Approver");
            }

            return RedirectToAction("Index", "Login");
        }

        [HttpPost]
        [Route("never-paid")]
        public async Task<IActionResult> NeverPaid(string searchTerm = "")
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }
            await SetViewBags(settings, "index");

            if (settings.RoleId == UserRole.Admin)
            {
                var neverPaid = await _applicationReportService.GetAllNeverPaidOrdersAsync(searchTerm: searchTerm.Trim());
                ViewData["OrdersStatus"] = "Waiting Approval";

                return View("~/Views/Reports/NeverPaid.cshtml", neverPaid);
            }

            if (settings.RoleId == UserRole.Approver)
            {
                return RedirectToAction("Index", "Approver");
            }

            return RedirectToAction("Index", "Login");
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

            return RedirectToAction("AssignedPayments", "Home");
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
                var allPayments = await _applicationReportService.GetAllPaymentsAsync(searchTerm.Trim(), page: page, pageSize:pageSize);

                return View(allPayments);
            }

            var allPaymentsByUser = await _applicationReportService.GetAllPaymentsAsync(settings.UserId, page: page, pageSize: pageSize);

            return View(allPaymentsByUser);
        }

        [Route("statements/{page:int?}")]
        public async Task<IActionResult> Statements(int page = 1, int pageSize = 10)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            await SetViewBags(settings, "index");

            if (settings.RoleId == UserRole.Admin)
            {
                var statement = await _statementService.GetStatementForDealerWithTransactionsAsync(settings.DealerId, settings.DealerId);

                return View(statement);
            }

            if (settings.RoleId == UserRole.Dealer)
            {
                var dealer = await _userService.GetDealerByUserId(settings.UserId);
                var transactions = await _statementService.GetTransactionsByDealerAsync(dealer.DealerReference);

                var bankStatement = new BankAccountStatement() 
                { 
                   Transactions = transactions.ToList(),
                };

                return View(bankStatement);
            }

            return RedirectToAction("Index", "Login");
        }

        [Route("statements-dealer/{page:int?}")]
        public async Task<IActionResult> Statements(string dealerReference, int page = 1, int pageSize = 10)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            await SetViewBags(settings, "index");

            ViewBag.Selected = dealerReference;
            if (dealerReference == "all" || dealerReference == "select")
            {
                var statement = await _statementService.GetStatementForDealerWithTransactionsAsync(settings.DealerId, settings.DealerId);

                return View(statement);
            } else
            {
                var dealer = await _userService.GetDealerByUserId(settings.UserId);
                var transactions = await _statementService.GetTransactionsByDealerAsync(dealerReference);

                var bankStatement = new BankAccountStatement()
                {
                    Transactions = transactions.ToList(),
                    Dealers = await _statementService.GetStatementsDealersAsync()
                };

                return View(bankStatement);
            }
        }

        [HttpPost("upload")]
        public async Task<IActionResult> Statements(IFormFile file, CancellationToken cancellationToken)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            await SetViewBags(settings, "index");

            var mapper = new BankStatementMapper();

            var isPdf = file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);

            if(isPdf)
            {
                using (var stream = file.OpenReadStream())
                using (var pdfReader = new PdfReader(stream))
                using (var pdfDoc = new PdfDocument(pdfReader))
                {
                    var numberOfPages = pdfDoc.GetNumberOfPages();

                    //Lets get first page fror account details
                    var bankStatement = new BankAccountStatement();
                    var statementDetails = PdfTextExtractor.GetTextFromPage(pdfDoc.GetFirstPage());

                    if (statementDetails != null)
                    {
                        bankStatement = BankStatementMapper.Parse(statementDetails);
                    }

                    bankStatement.AccountType = "EQUITY";
                    bankStatement.GeneratedBy = "Edward Guda Osewe";
                    bankStatement.FileName = file.FileName;

                    for (int i = 1; i <= numberOfPages; i++)
                    {
                        string text = PdfTextExtractor.GetTextFromPage(pdfDoc.GetPage(i));
                        var statementTransactions = BankStatementMapper.ExtractTransactions(text);

                        bankStatement.Transactions.AddRange(statementTransactions);
                    }

                    // Now you can save with your Dapper insert methods
                    await _statementService.CreateNewStatementAsync(bankStatement);
                }
            }
            else
            {
                using (var stream = file.OpenReadStream()) // IFormFile from upload
                {


                    var statement = mapper.MapFromExcel(stream, settings.DealerId, file.FileName);

                    // Now you can save with your Dapper insert methods
                    await _statementService.CreateNewStatementAsync(statement);
                }


                using (var stream = file.OpenReadStream()) // IFormFile from upload
                {
                    var statement = mapper.MapFromExcel(stream, settings.DealerId, file.FileName);

                    // Now you can save with your Dapper insert methods
                    await _statementService.CreateNewStatementAsync(statement);
                }
            }

            //Now get the latest staements
            return RedirectToAction("Statements", "Home");
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

            var allAwaitngApproval = await _applicationReportService.PaymentsSummary();

            return View(allAwaitngApproval.ToList());
        }

        [HttpPost]
        [Route("allpayments")]
        public async Task<IActionResult> Search(string searchTerm = "")
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            await SetViewBags(settings, "index", searchTerm.Trim());

            if (settings.RoleId == UserRole.Admin || settings.RoleId == UserRole.Approver)
            {
                var allPayments = await _applicationReportService.GetAllPaymentsAsync(searchTerm.Trim());

                return View("AllPayments", allPayments);
            }

            var allPaymentsByUser = await _applicationReportService.GetAllPaymentsAsync(settings.UserId, searchTerm.Trim());

            return View("AllPayments", allPaymentsByUser);
        }

        [HttpPost]
        [Route("orders")]
        public async Task<IActionResult> SearchDashBoard(string searchTerm = "")
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }
            await SetViewBags(settings, "index", searchTerm.Trim());

            if (settings.RoleId == UserRole.Admin)
            {
                var allAwaitngApproval = await _applicationReportService.GetAwaitingApprovalOrders(searchTerm);
                allAwaitngApproval.SearchTerm = searchTerm.Trim();
                ViewData["OrdersStatus"] = "Waiting Approval";

                return View("~/Views/Home/Index.cshtml", allAwaitngApproval);
            }

            var waitingApprovalByUser = await _applicationReportService.GetAwaitingApprovalOrdersByUser(settings.UserId, searchTerm.Trim());
            ViewData["OrdersStatus"] = "All Orders";
            waitingApprovalByUser.SearchTerm = searchTerm.Trim();
            return View(waitingApprovalByUser);
        }

        [Route("users")]
        public async Task<IActionResult> Users()
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            await SetViewBags(settings, "index");

            if (settings.RoleId == UserRole.Admin || settings.RoleId == UserRole.Approver)
            {
                var users = new UsersViewModel()
                {
                    Users = await _userService.GetAllUsersAsync()
                };

                return View(users);
            }

            var dealerUsers = new UsersViewModel()
            {
                Users = await _userService.GetUsersByDealerIdAsync(settings.DealerId)
            };

            return View(dealerUsers);
        }


        [Route("adduser")]
        public async Task<IActionResult> AddUser()
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            await SetViewBags(settings, "index");

            return View();
        }

        [HttpPost]
        [Route("adduser")]
        public async Task<IActionResult> AddUserSubmit(User userDetails)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            await SetViewBags(settings, "index");

            try
            {
                userDetails.Status = UserStatus.Active;
                userDetails.DealerId = settings.DealerId;
                userDetails.IsActive = true;
                await _userService.AddUserAsync(userDetails);
            }
            catch (Exception)
            {

                return View("AddUser");
            }

            return RedirectToAction("Users");
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
