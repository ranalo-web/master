using Microsoft.AspNetCore.Mvc;
using Ranalo.Configuration;
using Ranalo.DataStore.DataModels;
using Ranalo.Services;

namespace Ranalo.Controllers
{
    [LoadUserSettingsFromCookie]
    public class CustomerController : Controller
    {
        private readonly IApplicationReportService _applicationReportService;
        private readonly IUserService _userService;
        public CustomerController(IApplicationReportService applicationReportService, IUserService userService)
        {
            _applicationReportService = applicationReportService;
            _userService = userService;
        }

        [Route("customer-details/{orderId:int}")]
        public async Task<IActionResult> CustomerDetails(long orderId)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            if (settings.RoleId == UserRole.Dealer)
            {
                var dealer = await _userService.GetDealerByUserId(settings.UserId);
                ViewBag.UserName = dealer.CompanyName;
            }
            else
            {
                var user = await _userService.GetUserByCustomerIdAsync(settings.UserId);
                ViewBag.UserName = user.KnownAs;
            }


            ViewBag.IsAdmin = settings.RoleId == UserRole.Admin;
            ViewBag.IsApprover = settings.RoleId == UserRole.Approver;
            var customerDetails = await _applicationReportService.GetCustomerDetailsByOrderIdAsync(orderId);

            return View(customerDetails);
        }

        
    }
}
