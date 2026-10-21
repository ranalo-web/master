using Azure;
using Microsoft.AspNetCore.Mvc;
using Ranalo.Configuration;
using Ranalo.DataStore;
using Ranalo.DataStore.DataModels;
using Ranalo.Models;
using Ranalo.Services;
using Ranalo.Services.DeviceLock;
using Ranalo.Woocommece.Api.Services;

namespace Ranalo.Controllers
{
    [LoadUserSettingsFromCookie]
    public class EnrolmentsController : Controller
    {
        private readonly IUserService _userService;
        private readonly IEnrolmentService _enrolmentService;
        private readonly IApplicationReportService _applicationReportService;
        private readonly ISyncService _syncService;
        private readonly IEnrolmentCheckService _checks;
        private readonly ITranssionEnrolmentWorkflow _transsion;
        private readonly IDevicesRepository _devices;
        private readonly IStockPhoneService _stock;
        private readonly IDeviceService _deviceService;
        private readonly IOrderContractService _contracts;
        private readonly ICustomerScreeningService _screening;
        public EnrolmentsController(IUserService userService, 
            IEnrolmentService enrolmentService, 
            IApplicationReportService applicationReportService,
            ISyncService syncService,
            IEnrolmentCheckService checks,
            ITranssionEnrolmentWorkflow transsion,
            IDevicesRepository devices,
            IStockPhoneService stock,
            IDeviceService deviceService,
            IOrderContractService contracts,
            ICustomerScreeningService screening)
        {
            _screening = screening;
            _devices = devices;
            _contracts = contracts;
            _deviceService = deviceService;
            _stock = stock;
            _userService = userService;
            _enrolmentService = enrolmentService;
            _applicationReportService = applicationReportService;
            _syncService = syncService;
            _checks = checks;
            _transsion = transsion;
        }

        [Route("enrolments")]
        public async Task<IActionResult> Index(int page = 1, int pageSize = 10, string searchTerm = "")
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            await SetViewBags(settings, "index", searchTerm);
            ViewBag.PageSize = pageSize;

            if (settings.RoleId == UserRole.Admin || settings.RoleId == UserRole.Approver)
            {
                var enrolments = await _enrolmentService.GetAllEnrolmentsAsync(page, pageSize, searchTerm.Trim());

                var response = new EnrolmentViewModel()
                {
                    CurrentPage = page,
                    Enrolments = enrolments.Items.ToList(),
                    PageSize = pageSize,
                    TotalCount = enrolments.TotalCount,
                    SearchTerm = searchTerm.Trim(),
                    IsAdmin = settings.RoleId == UserRole.Admin,
                    CanApprove = e => DeviceLockRules.CanApproveEnrolment(settings.RoleId, settings.UserId, e),
                };
                response.LockGroups = await _devices.GetLockGroupsAsync(response.Enrolments.Select(e => e.AccountId));
                response.KnoxActive = await _devices.GetKnoxActiveAsync(response.Enrolments.Select(e => e.AccountId));
                AddFlash(response);

                return View(response);
            }

            var dealer = await _userService.GetDealerByUserId(settings.UserId);

            // Enrolment.DealerId is set from settings.DealerId (see the POST
            // AddEnrolment action below), which is the real Dealers.DealerId
            // PK -- not DealerReference (the separate value matched against
            // Devices.DeviceGroupId elsewhere in the app).
            var dealerEnrolments = await _enrolmentService.GetDealerEnrolmentsAsync(dealer.DealerId, page, pageSize, searchTerm.Trim());

            var dealerResponse = new EnrolmentViewModel()
            {
                CurrentPage = page,
                Enrolments = dealerEnrolments.Items.ToList(),
                PageSize = pageSize,
                TotalCount = dealerEnrolments.TotalCount,
                SearchTerm = searchTerm.Trim(),
            };
            dealerResponse.LockGroups = await _devices.GetLockGroupsAsync(dealerResponse.Enrolments.Select(e => e.AccountId));
            dealerResponse.KnoxActive = await _devices.GetKnoxActiveAsync(dealerResponse.Enrolments.Select(e => e.AccountId));
            AddFlash(dealerResponse);

            return View(dealerResponse);
        }

        [Route("addenrolment")]
        public async Task<IActionResult> AddEnrolment()
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            if (!DeviceLockRules.CanEnrol(settings.RoleId))
            {
                return RedirectToAction("Index");
            }

            //await _enrolmentService.SendReminderMessage("359063757998542");

            //var iemi = "350154840923628";
            //var enrolment = await _enrolmentService.GetByImeiNumberAsync(iemi);
            //await _enrolmentService.CreateDeviceFromKnox(enrolment);

            await SetViewBags(settings, "index");

            return View(new EnrolmentViewModel()
            {
                Enrolments = new List<Enrolment>(),
                IsAdmin = settings.RoleId == UserRole.Admin
            });
        }

        [Route("approve-enrolment/{imei}")]
        public async Task<IActionResult> ApproveEnrolment(string imei)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            var existingEnrolment = await _enrolmentService.GetByImeiNumberAsync(imei);
            if (existingEnrolment == null)
            {
                return RedirectToAction("Index", "Enrolments");
            }

            // Transsion phones are approved after the customer call, by POST
            // (approve-call) -- never through this Knox link.
            if (DeviceLockRules.IsTranssion(existingEnrolment.DeviceBrand))
            {
                TempData["EnrolmentError"] = "Transsion phones are approved with the Approve call button.";
                return RedirectToAction("Index", "Enrolments");
            }

            var decision = DeviceLockRules.CanApproveEnrolment(settings.RoleId, settings.UserId, existingEnrolment);
            if (!decision.Allowed)
            {
                TempData["EnrolmentError"] = decision.Reason;
                return RedirectToAction("Index", "Enrolments");
            }

            if (existingEnrolment.Status == EnrolmentStatus.Pending && existingEnrolment.StockKnoxApprovedAt.HasValue)
            {
                var sold = await _stock.UnlockSoldKnoxAsync(existingEnrolment, settings);
                TempData[sold.Success ? "EnrolmentMessage" : "EnrolmentError"] = sold.Message;
            }
            else if (existingEnrolment.Status == EnrolmentStatus.Pending)
            {
                existingEnrolment.CallApprovedByUserId = settings.UserId;
                existingEnrolment.CallApprovedAt = DateTime.UtcNow;
                if (await _enrolmentService.ApproveEnrolment(existingEnrolment))
                {
                    TempData["EnrolmentMessage"] = $"Phone {imei} approved in Knox.";
                }
                else
                {
                    TempData["EnrolmentError"] = $"Knox hasn't accepted phone {imei} yet: {existingEnrolment.KnoxResponse}. "
                        + "It usually reaches Knox a few minutes after enrolling -- try Approve again shortly.";
                }
            }

            var updatedEnrolment = await _enrolmentService.GetByImeiNumberAsync(imei);

            if (updatedEnrolment != null && updatedEnrolment.Status == EnrolmentStatus.Approved)
            {
                await _enrolmentService.CreateDeviceFromKnox(updatedEnrolment);
                await _contracts.TryCreateAsync(updatedEnrolment.OrderId);
            }


            return RedirectToAction("Index", "Enrolments");
        }

        // Transsion: the approver/admin has called the customer. Unlocks the
        // phone now if it's already switched on, otherwise as soon as it is.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("enrolments/approve-call/{enrolmentId:guid}")]
        public async Task<IActionResult> ApproveCall(Guid enrolmentId)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            var result = await _transsion.ApproveCallAsync(enrolmentId, settings);
            TempData[result.Success ? "EnrolmentMessage" : "EnrolmentError"] = result.Message;
            return RedirectToAction("Index", "Enrolments");
        }

        // Transsion, admin: unlock for a few days once Transsion has the phone
        // registered, when its activation hasn't reached us.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("enrolments/unlock-for/{enrolmentId:guid}")]
        public async Task<IActionResult> UnlockFor(Guid enrolmentId, int days = 3)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            var result = await _transsion.UnlockForAsync(enrolmentId, TimeSpan.FromDays(Math.Clamp(days, 1, 7)), settings);
            TempData[result.Success ? "EnrolmentMessage" : "EnrolmentError"] = result.Message;
            return RedirectToAction("Index", "Enrolments");
        }

        // Transsion: pre-enrol a phone PayTrigger never took or refused.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("enrolments/send-to-transsion/{enrolmentId:guid}")]
        public async Task<IActionResult> SendToTranssion(Guid enrolmentId)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            var result = await _enrolmentService.SendToTranssionAsync(enrolmentId, settings);
            TempData[result.Success ? "EnrolmentMessage" : "EnrolmentError"] = result.Message;
            return RedirectToAction("Index", "Enrolments");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("enrolments/retry-unlock/{enrolmentId:guid}")]
        public async Task<IActionResult> RetryUnlock(Guid enrolmentId)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            var result = await _transsion.RetryUnlockAsync(enrolmentId, settings);
            TempData[result.Success ? "EnrolmentMessage" : "EnrolmentError"] = result.Message;
            return RedirectToAction("Index", "Enrolments");
        }

        [Route("create-device/{imei}")]
        public async Task<IActionResult> CreateKnoxDevice(string imei)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            // Call the approval for IMEI
            var existingEnrolment = await _enrolmentService.GetByImeiNumberAsync(imei);

            //Test
            //await _enrolmentService.CreateDeviceFromKnox(existingEnrolment);


            if (existingEnrolment != null &&
                existingEnrolment.Status == EnrolmentStatus.Approved &&
                !DeviceLockRules.IsTranssion(existingEnrolment.DeviceBrand) &&
                settings.RoleId is UserRole.Admin or UserRole.Approver)
            {
                await _enrolmentService.CreateDeviceFromKnox(existingEnrolment);
                await _contracts.TryCreateAsync(existingEnrolment.OrderId);
            }


            return RedirectToAction("Index", "Enrolments");
        }

        [Route("delete-enrolment/{enrolmentId}")]
        public async Task<IActionResult> DeleteEnrolment(Guid enrolmentId)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            // Call the approval for IMEI
            var existingEnrolment = await _enrolmentService.GetByEnrolmentIdNumberAsync(enrolmentId);

            if (existingEnrolment != null && existingEnrolment.Status == EnrolmentStatus.New)
            {
                await _enrolmentService.DeleteNewEnrolmentEnrolment(existingEnrolment);
            }

            return RedirectToAction("Index", "Enrolments");
        }

        [HttpPost]
        [Route("addenrolment")]
        public async Task<IActionResult> AddEnrolment(Enrolment enrolment, string? overrideBrand, int? selectedDealerId, string? mpesaCode)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            if (!DeviceLockRules.CanEnrol(settings.RoleId))
            {
                return RedirectToAction("Index");
            }

            var isAdmin = settings.RoleId == UserRole.Admin;
            var response = new EnrolmentViewModel { IsAdmin = isAdmin };
            if (string.IsNullOrEmpty(enrolment.IMEI))
            {
                response.Errors.Add("The IMEI number is missing!");
            }

            if (enrolment.OrderId == 0)
            {
                response.Errors.Add("Please provide an order number!");
            }

            var order = await _applicationReportService.GetOrderByOrderIdAsync(enrolment.OrderId);

            if (order == null)
            {
                response.Errors.Add("There are no orders for the order Id!");
            }
            else
            {
                // Screened and approved first (Services/ScreeningRules.cs).
                var cannotEnrol = await _screening.CannotEnrolAsync(order.Status, order.DateCreated);
                if (cannotEnrol != null)
                {
                    response.Errors.Add(cannotEnrol);
                }
            }
            if (order?.NationalId != enrolment.AccountId.ToString())
            {
                response.Errors.Add("The National Id Number does not match the one on this order");
            }

            if (order?.IMEI != enrolment.IMEI)
            {
                response.Errors.Add("There IMEI Number does not match the one on this order");
            }

            // The order came without the deposit's M-Pesa code: the dealer
            // gives it here, the deposit check below confirms it, and it's
            // saved to the order once the enrolment goes through.
            var enteredCode = DeviceLockRules.NormaliseMpesaCode(mpesaCode);
            ViewBag.MpesaCode = enteredCode;
            string? newDepositCode = null;
            if (order != null && enteredCode != null)
            {
                var orderCode = DeviceLockRules.DepositCodeOrNull(order.MpesaDepositRef);
                var orderHasCode = orderCode != null;
                var onAnotherOrder = !orderHasCode && DeviceLockRules.IsMpesaCodeFormat(enteredCode)
                    && await _devices.MpesaCodeIsAlreadyLinked(enteredCode);
                var codeError = DeviceLockRules.CheckEnteredDepositCode(orderCode, enteredCode, onAnotherOrder);
                if (codeError != null)
                {
                    response.Errors.Add(codeError);
                }
                else if (!orderHasCode)
                {
                    order.MpesaDepositRef = enteredCode;
                    newDepositCode = enteredCode;
                }
            }

            if (IsValidImei(enrolment.IMEI) == false)
            {
                response.Errors.Add("The IMEI Number is not valid, please check the number and correct.");
            }

            var existingEnrolment = await _enrolmentService.GetByImeiNumberAsync(enrolment.IMEI);

            // A stock phone (locked by IMEI, no order) is sold by enrolling it
            // with the order as usual: the sale is attached to it.
            var stock = existingEnrolment != null && StockPhoneRules.IsStock(existingEnrolment) ? existingEnrolment : null;

            if (existingEnrolment != null && stock == null)
            {
                response.Errors.Add("The IMEI Number is registered to another order");
            }

            //Validate IMEI
            await SetViewBags(settings, "index");

            if (response.Errors.Any())
            {
                return await EnrolmentFormAsync(response, enrolment);
            }

            // Phones already on Nuovo are approved as Nuovo (any brand).
            object? nuovoDevice = null;
            try
            {
                // A stock phone is with Knox or Transsion already.
                if (stock == null)
                {
                    nuovoDevice = await _syncService.DevicePullSearch(enrolment.IMEI);
                }
            }
            catch (Exception)
            {
                response.Errors.Add("Couldn't check Nuovo for this IMEI. Please try again or contact the system administrator.");
                return await EnrolmentFormAsync(response, enrolment);
            }

            // Brand (from the order + TAC), M-Pesa deposit, order's dealer.
            var checks = await _checks.EvaluateAsync(enrolment, order!, settings.RoleId, overrideBrand, onNuovo: nuovoDevice != null);
            response.Errors.AddRange(checks.Errors);

            // The stock phone's brand was confirmed by an admin when it was
            // locked; the order can only disagree with it.
            if (stock != null)
            {
                var brandError = StockPhoneRules.CheckSaleBrand(stock, checks.Brand);
                if (brandError != null)
                {
                    response.Errors.Add(brandError);
                }
                checks.Brand = stock.DeviceBrand;
                checks.BrandBlocked = false;
                checks.BrandOverridden = false;
            }

            if (checks.BrandBlocked && !response.Errors.Any())
            {
                response.Errors.Add(checks.BrandError + (checks.Brand == null
                    ? " If it's another brand, enrol it on Nuovo first."
                    : ""));
            }

            // Dealers enrol for themselves; approvers/admins for the dealer
            // on the order, or one they pick when the order has none.
            int dealerId;
            if (settings.RoleId == UserRole.Dealer)
            {
                dealerId = settings.DealerId;
            }
            else if (checks.OrderDealerId.HasValue)
            {
                dealerId = checks.OrderDealerId.Value;
            }
            else if (selectedDealerId.HasValue)
            {
                dealerId = selectedDealerId.Value;
            }
            else
            {
                response.Errors.Add($"The order's dealer referral code ('{order!.DealerRef}') doesn't match a dealer. Choose the dealer below.");
                ViewBag.AskDealer = true;
                dealerId = 0;
            }

            if (response.Errors.Any())
            {
                return await EnrolmentFormAsync(response, enrolment);
            }

            try
            {
                enrolment.Status = EnrolmentStatus.New;
                enrolment.Updated = DateTime.Now;
                enrolment.Created = DateTime.Now;
                enrolment.UpdatedBy = settings.Name;
                enrolment.DealerId = dealerId;
                enrolment.Id = Guid.NewGuid();
                enrolment.EnrolledByUserId = settings.UserId;
                enrolment.EnrolledByRole = (int)settings.RoleId;
                enrolment.DeviceBrand = checks.Brand;
                enrolment.ProductName = checks.ProductName;
                enrolment.DealerRef = order!.DealerRef;
                enrolment.TacBrand = checks.TacBrand;
                enrolment.BrandOverriddenByUserId = checks.BrandOverridden ? settings.UserId : null;
                enrolment.RequiredDeposit = checks.Deposit.Required;
                enrolment.DepositPaid = checks.Deposit.Paid;
                enrolment.DepositShort = checks.Deposit.IsShort;

                if (stock != null)
                {
                    var attachError = await _enrolmentService.AttachStockSaleAsync(stock, enrolment);
                    if (attachError != null)
                    {
                        response.Errors.Add(attachError);
                        return await EnrolmentFormAsync(response, enrolment);
                    }

                    await SaveDepositCodeAsync(enrolment.OrderId, newDepositCode);

                    TempData["EnrolmentMessage"] = $"Stock phone {stock.IMEI} sold on order {stock.OrderId}. "
                        + (DeviceLockRules.IsTranssion(stock.DeviceBrand)
                            ? "It unlocks once an approver has called the customer and approved it."
                            : "An approver approves it on this page to unlock it.")
                        + (stock.DepositShort ? " The deposit is short, so only an admin can approve it." : "");
                    return RedirectToAction("Index");
                }

                enrolment = await _enrolmentService.CreateEnrolmentasync(enrolment, order);
                await SaveDepositCodeAsync(enrolment.OrderId, newDepositCode);
            }
            catch (Exception)
            {
                response.Errors.Add("There was an Error processing your request. Please contact the system administrator.");
                return await EnrolmentFormAsync(response, enrolment);
            }

            try
            {
                if (nuovoDevice == null)
                {
                    enrolment = await _enrolmentService.StartEnrolmentasync(enrolment, order);
                }
                else
                {
                    enrolment.Status = EnrolmentStatus.Approved;
                    enrolment.Updated = DateTime.UtcNow;
                    enrolment.UpdatedBy = "NOUVAPAY";
                    await _enrolmentService.UpdateEnrolmentasync(enrolment);
                    await _contracts.TryCreateAsync(enrolment.OrderId);
                }
            }
            catch (Exception)
            {

                response.Errors.Add("There was an sending device to Nouvapay or Knox. Please contact the system administrator.");
                return await EnrolmentFormAsync(response, enrolment);
            }

            if (enrolment.Status == EnrolmentStatus.Error)
            {
                TempData["EnrolmentError"] = $"Enrolment for IMEI {enrolment.IMEI} failed: {enrolment.PayTriggerResponse ?? enrolment.KnoxResponse}";
            }
            else if (DeviceLockRules.IsTranssion(enrolment.DeviceBrand))
            {
                TempData["EnrolmentMessage"] =
                    $"{enrolment.DeviceBrand} phone {enrolment.IMEI} pre-enrolled. Ask the customer to switch it on and connect to the internet. " +
                    "It stays locked until an approver has called the customer and approved it." +
                    (enrolment.DepositShort ? " The deposit is short, so only an admin can approve it." : "");
            }
            else if (enrolment.Status == EnrolmentStatus.Pending)
            {
                TempData["EnrolmentMessage"] = $"Phone {enrolment.IMEI} enrolled and sent to Knox. An approver approves it on this page once it reaches Knox (usually a few minutes)."
                    + (enrolment.DepositShort ? " The deposit is short, so only an admin can approve it." : "");
            }
            else if (enrolment.DepositShort)
            {
                TempData["EnrolmentMessage"] = "Enrolled. The deposit is short, so only an admin can approve it.";
            }

            return RedirectToAction("Index");
        }

        // As the admin fix on Missing M-Pesa: WooCommerce first, then the
        // order here (which is what contract creation reads). If WooCommerce
        // can't be reached the order here still gets it, and an admin is told.
        private async Task SaveDepositCodeAsync(long orderId, string? code)
        {
            if (code == null)
            {
                return;
            }

            try
            {
                await _deviceService.AssignMpesaToOrderAsync((int)orderId, code);
            }
            catch (Exception)
            {
                await _devices.UpdateMpesaForOrder(orderId, code);
                TempData["EnrolmentError"] = $"The deposit code {code} was saved on order {orderId} here, but WooCommerce couldn't be updated. An admin should add it to the order in WooCommerce.";
            }
        }

        private async Task<IActionResult> EnrolmentFormAsync(EnrolmentViewModel response, Enrolment enrolment)
        {
            response.Enrolments ??= new List<Enrolment>();
            response.Enrolments.Add(enrolment);
            if (ViewBag.AskDealer is true)
            {
                response.Dealers = await _userService.GetAllDealers();
            }
            return View("AddEnrolment", response);
        }

        private void AddFlash(EnrolmentViewModel model)
        {
            if (TempData["EnrolmentMessage"] is string message) model.Messages.Add(message);
            if (TempData["EnrolmentError"] is string error) model.Errors.Add(error);
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

        public static bool IsValidImei(string imei) => DeviceLockRules.IsValidImei(imei);
    }
}
