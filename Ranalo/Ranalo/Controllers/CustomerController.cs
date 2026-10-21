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
        private readonly ICustomerScreeningService _screening;
        public CustomerController(IApplicationReportService applicationReportService, IUserService userService, ICustomerScreeningService screening)
        {
            _applicationReportService = applicationReportService;
            _userService = userService;
            _screening = screening;
        }

        [Route("order-details/{orderId:int}")]
        public async Task<IActionResult> OrderDetails(long orderId)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            await SetViewBags(settings, "customer");

            var customerDetails = await _applicationReportService.GetCustomerDetailsByOrderIdAsync(orderId);

            if(customerDetails == null)
            {
                return View(customerDetails);
            }

            //Get customer Notes
            var notes = await _applicationReportService.GetNotesByOrderIdAsync(orderId);

            if(notes != null)
            {
                foreach (var note in notes)
                {
                    var userDetails = await _userService.GetUserByCustomerIdAsync(note.UserId);
                    if(userDetails.RoleId == UserRole.Dealer)
                    {
                        var dealer = await _userService.GetDealerByUserId(note.UserId);
                        note.UserName = dealer.CompanyName;
                    }
                    else
                    {
                        note.UserName = userDetails.Name;
                    }
                    
                }
                customerDetails.Notes = notes;
            }

            // Screening calls, for approvers and admins on a real order.
            if (customerDetails.OrderID > 0 && settings.RoleId is UserRole.Admin or UserRole.Approver)
            {
                ViewBag.Screening = await _screening.GetOrderScreeningAsync(orderId, customerDetails.DateCreated);
            }

            return View(customerDetails);
        }

        [Route("account-details/{orderId:int}")]
        public async Task<IActionResult> AccountDetails(long orderId)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            await SetViewBags(settings, "customer");

            var customerDetails = await _applicationReportService.GetCustomerDetailsByOrderIdAsync(orderId);

            if(customerDetails == null)
            {
                return View(customerDetails);
            }
            //Get customer Notes
            // No order: notes are kept against the account number (the route id).
            var customerId = customerDetails.OrderID == 0 ? orderId : customerDetails.OrderID;
            var notes = await _applicationReportService.GetNotesByOrderIdAsync(customerId);

            if (notes != null)
            {
                foreach (var note in notes)
                {
                    var userDetails = await _userService.GetUserAnyUserByIdAsync(note.UserId);
                    if (userDetails.RoleId == UserRole.Dealer)
                    {
                        var dealer = await _userService.GetDealerByUserId(note.UserId);
                        note.UserName = dealer.CompanyName;
                    }
                    else
                    {
                        note.UserName = userDetails.Name;
                    }

                }
                customerDetails.Notes = notes;
            }

            return View(customerDetails);
        }

        [HttpPost]
        [Route("addcustomernote")]
        public async Task<IActionResult> AddNote(string orderId, string customerNote)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            await SetViewBags(settings, "approver"); await SetViewBags(settings, "approver");

            await _applicationReportService.AddCustomerNoteAsync(settings.UserId, long.Parse(orderId), customerNote);

            return Redirect($"/account-details/{orderId}");
        }

        private async Task SetViewBags(User settings, string backLink)
        {
            ViewBag.BackLink = backLink;
            ViewBag.IsAdmin = settings.RoleId == UserRole.Admin;
            ViewBag.IsApprover = settings.RoleId == UserRole.Approver;
            ViewBag.IsDealer = settings.RoleId == UserRole.Dealer;
            ViewBag.IsAgent = settings.RoleId == UserRole.Agent;
            ViewBag.UserName = settings.KnownAs;
            if (settings.RoleId == UserRole.Dealer)
            {
                var dealer = await _userService.GetDealerByUserId(settings.UserId);
                ViewBag.UserName = dealer.CompanyName;
            }
        }

    }
}
