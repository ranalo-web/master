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
using Ranalo.SumsungKnox;
using Ranalo.SumsungKnox.Models;
using Ranalo.VeriTechClient;
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
        private readonly IKnoxGuardClient _knoxSerciceClient;
        private readonly IVeritechApiClient _veritechApiClient;
        public HomeController(IApplicationReportService applicationReportService, 
            IUserService userService, 
            IStatementService statementService,
            IMySqlPaymentsRepository mysqlRepository, IKnoxGuardClient knoxClient, IVeritechApiClient veritechApiClient)
        {
            _mysqlRepository = mysqlRepository;
            _applicationReportService = applicationReportService;
            _userService = userService;
            _statementService = statementService;
            _knoxSerciceClient = knoxClient;
            _veritechApiClient = veritechApiClient;
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

            //Test Veritech
            //var foo = await _veritechClient.GetDevicesAsync();
            //var devices = new List<string>() { "351065613616471" };
            //var foo = await _veritechApiClient.UploadDevicesAsync(devices);

            //Knox
            //var deviceId = "351065613616471";
            //var foo = await _knoxSerciceClient.GetDeviceInfoAsync(deviceId);

            //var response = await _knoxSerciceClient.ListDevicesAsync(new ListDevicesRequest
            //{
            //    PageNum = 0,
            //    PageSize = 20,
            //    SortBy = "updateTime",
            //    SortOrder = "descending",
            //    Search = "351065613616471"
            //    //Filter = new DeviceListFilter
            //    //{
            //    //    Status = new List<string> { "Enrolled" },
            //    //    //SimControlEnabled = true
            //    //}
            //});

            //var request = new UnlockDeviceRequest
            //{
            //    DeviceUid = "351065613492352",
            //    Message = "Device unlocked after payment received"
            //};

            //var fruitBalls = await _knoxSerciceClient.UnlockDeviceAsync(request);

            //var deviceToApprove = new ApproveDeviceRequest() { 
            //     DeviceUid = "351065613616471",
            //     ApproveId = "vkdp302411utid",
            //     ApproveComment = "Test Approval comment"
            //};

            //DateTime utcDate = DateTime.UtcNow.AddDays(1);

            //long unixTimestamp = new DateTimeOffset(utcDate)
            //    .ToUnixTimeMilliseconds();

            //var request = new DeviceActionsRequest
            //{
            //    DeviceUid = "351065613492352",
            //    ApproveId = "TestApprovalViaKnoxUI",
            //    Actions = new List<DeviceActionItem>
            //    {
            //        new DeviceActionItem
            //        {
            //            Action = "unLock",
            //            Timestamp = 0
            //        },
            //        new DeviceActionItem
            //        {
            //            Action = "lock",
            //            Timestamp = unixTimestamp,
            //            Message = "Device lock message"
            //        }
            //    }
            //};

            //var bar = await _knoxSerciceClient.ExecuteDeviceActionsAsync(request);

            //var fooBar = await _knoxSerciceClient.ApproveDeviceAsync(deviceToApprove);


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

        [Route("dealers")]
        public async Task<IActionResult> Dealers()
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            await SetViewBags(settings, "index");

            if (settings.RoleId == UserRole.Admin || settings.RoleId == UserRole.Approver)
            {
                var dealers = new DealersViewModel()
                {
                    Dealers = await _userService.GetAllDealers()
                };

                return View(dealers);
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

        [Route("edituser")]
        public async Task<IActionResult> EditUser(int userId)
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
                userDetails.ParentUserId = settings.DealerId;
                userDetails.IsActive = true;
                await _userService.AddUserAsync(userDetails);
            }
            catch (Exception)
            {

                return View("AddUser");
            }

            return RedirectToAction("Users");
        }

        [HttpPost]
        [Route("updateuser")]
        public async Task<IActionResult> UpdateUserSubmit(User userDetails)
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
                userDetails.ParentUserId = settings.DealerId;
                userDetails.IsActive = true;
                await _userService.UpdateUserAsync(userDetails);
            }
            catch (Exception)
            {

                return View("EditUser", userDetails);
            }

            return RedirectToAction("Users");
        }


        [Route("suspenduser/{userId:int}")]
        public async Task<IActionResult> SuspendUserSubmit(int userId)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            await SetViewBags(settings, "index");

            try
            {
                await _userService.SuspendUserAsync(userId);
            }
            catch (Exception)
            {

                return RedirectToAction("Users");
            }

            return RedirectToAction("Users");
        }

        [Route("adddealer")]
        public async Task<IActionResult> AddDealer()
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
        [Route("adddealer")]
        public async Task<IActionResult> AddDealerSubmit(DataStore.Dealer dealerDetails)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            await SetViewBags(settings, "index");

            try
            {
                await _userService.AddDealerAsync(dealerDetails);
            }
            catch (Exception)
            {

                return View("AddDealer");
            }

            return RedirectToAction("Dealers");
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
                ViewBag.DealerId = dealer.DealerId;
            }
        }

    }
}
