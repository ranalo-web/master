using Microsoft.AspNetCore.Mvc;
using Ranalo.Configuration;
using Ranalo.DataStore;
using Ranalo.DataStore.DataModels;
using Ranalo.Models;
using Ranalo.Services;
using System.Drawing.Printing;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Ranalo.Controllers
{
    [LoadUserSettingsFromCookie]
    public class DevicesController : Controller
    {
        private readonly IDeviceService _devicesService;
        private readonly IUserService _userService;
        private readonly IApplicationReportService _applicationReportService;

        public DevicesController(IDeviceService devicesService, IUserService userService, IApplicationReportService applicationReportService)
        {
            _devicesService = devicesService;
            _userService = userService;
            _applicationReportService = applicationReportService;
        }

        [Route("devices-with-no-orders/{page:int?}/{pageSize:int?}")]
        public async Task<IActionResult> DevicesWithNoOrders(int page = 1, int pageSize = 10)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            return await GetAndRenderView(settings, null, page, pageSize);
        }

        [HttpPost]
        [Route("search-devices")]
        public async Task<IActionResult> DevicesWithNoOrders(string searchTerm)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            return await GetAndRenderView(settings, null, searchTerm: searchTerm);
        }

        private async Task<IActionResult> GetAndRenderView(User settings, List<string>? errors, int page = 1, int pageSize = 10, string searchTerm = "")
        {
            await SetViewBags(settings, "index");
            //await SetViewBags(settings, "index");
            var devices = new DevicesWithDealerViewModel();

            if (settings.RoleId == UserRole.Admin || settings.RoleId == UserRole.Approver)
            {
                devices = await _devicesService.GetDevicesWithNoOrders(page: page, pageSize: pageSize, searchTerm: searchTerm);

                if (errors != null)
                {
                    devices.Errors = errors;
                }

                return View(devices);
            }

            var dealer = await _userService.GetDealerByUserId(settings.UserId);

            var dealerId = Convert.ToInt32(dealer.DealerReference);

            devices = await _devicesService.GetDevicesWithNoOrders(dealerId, page: page, pageSize: pageSize, searchTerm: searchTerm);

            if (errors != null)
            {
                devices.Errors = errors;
            }

            return View(devices);
        }

        [HttpPost]
        [Route("correct-mpesa")]
        public async Task<IActionResult> DevicesWithNoOrders(int page, int pageSize, long accountno, int orderNumber, string newMpesa)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }
            //Validate order number
            var result = await _devicesService.GetCheckOrderIdLinkedAsync(orderNumber);
            var errors = new List<string>();
            if (result.AccountNo != null && result.DeviceId.HasValue)
            {
                errors.Add("Order Id already mapped to an account." );
            }

            //Validate Mpesa Code
            if(!await _devicesService.MpesaCodeIsValidAsync(newMpesa))
            {
                errors.Add("Mpesa Code is invalid.");
            }
            //Check if we have already seen this Mpesa
            if (await _devicesService.MpesaCodeIsLinkedAsync(newMpesa))
            {
                errors.Add("Mpesa already linked to an order.");
            }

            //Validate Order Id
            if (!await _devicesService.OrderNumberIsValidAsync(orderNumber))
            {
                errors.Add("Order number is invalid.");
            }

            if(errors.Any())
            {
                return await GetAndRenderView(settings, errors, page, pageSize);
            }

            //Only if no errors do we need to assign new MPESA
            try
            {
                await _devicesService.AssignMpesaToOrderAsync(orderNumber, newMpesa);
            }
            catch (Exception)
            {

                errors.Add("System error occured, please try again later.");
            }


            return await GetAndRenderView(settings, errors, page, pageSize);
            
        }

        [HttpGet]
        [Route("alldevices/{page:int?}")]
        public async Task<IActionResult> AllDevices(int page = 1, int pageSize = 10, string searchTerm = "")
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }
            await SetViewBags(settings, "index");
            //await SetViewBags(settings, "index");
            var accounts = new AllAccountsViewModel();

            if (settings.RoleId == UserRole.Admin || settings.RoleId == UserRole.Approver)
            {
                accounts = await _applicationReportService.GetAllAccountsAsync(null, searchTerm: searchTerm, page: page, pageSize: pageSize);

                return View(accounts);
            }

            var dealer = await _userService.GetDealerByUserId(settings.UserId);

            var dealerId = Convert.ToInt32(dealer.DealerReference);

            accounts = await _applicationReportService.GetAllAccountsAsync(dealerId, searchTerm: searchTerm, page: page, pageSize: pageSize);

            return View(accounts);
        }
        private async Task SetViewBags(User settings, string backLink)
        {
            ViewBag.BackLink = backLink;
            ViewBag.IsAdmin = settings.RoleId == UserRole.Admin;
            ViewBag.IsApprover = settings.RoleId == UserRole.Approver;
            ViewBag.IsDealer = settings.RoleId == UserRole.Dealer;

            ViewBag.UserName = settings.KnownAs;
            if (settings.RoleId == UserRole.Dealer)
            {
                var dealer = await _userService.GetDealerByUserId(settings.UserId);
                ViewBag.UserName = dealer.CompanyName;
                settings.DealerId = dealer.DealerId;
            }
        }
    }
}
