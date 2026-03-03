using Azure;
using Microsoft.AspNetCore.Mvc;
using Ranalo.Configuration;
using Ranalo.DataStore.DataModels;
using Ranalo.Models;
using Ranalo.Services;

namespace Ranalo.Controllers
{
    [LoadUserSettingsFromCookie]
    public class EnrolmentsController : Controller
    {
        private readonly IUserService _userService;
        private readonly IEnrolmentService _enrolmentService;
        private readonly IApplicationReportService _applicationReportService;
        public EnrolmentsController(IUserService userService, IEnrolmentService enrolmentService, IApplicationReportService applicationReportService)
        {
            _userService = userService;
            _enrolmentService = enrolmentService;
            _applicationReportService = applicationReportService;
        }

        [Route("enrolments")]
        public async Task<IActionResult> Index(int page = 1, int pageSize = 10, string searchTerm = "")
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            await SetViewBags(settings, "index");

            if (settings.RoleId == UserRole.Admin || settings.RoleId == UserRole.Approver)
            {
                var enrolments = await _enrolmentService.GetAllEnrolmentsAsync(page, pageSize: pageSize);

                var response = new EnrolmentViewModel()
                {
                    CurrentPage = page,
                    Enrolments = enrolments.Items.ToList(),
                    PageSize = pageSize,
                    TotalCount = enrolments.TotalCount,
                };

                return View(response);
            }

            var dealer = await _userService.GetDealerByUserId(settings.UserId);

            var dealerId = Convert.ToInt32(dealer.DealerReference);

            var dealerEnrolments = await _enrolmentService.GetDealerEnrolmentsAsync(dealerId, page, pageSize: pageSize);

            var dealerResponse = new EnrolmentViewModel()
            {
                CurrentPage = page,
                Enrolments = dealerEnrolments.Items.ToList(),
                PageSize = pageSize,
                TotalCount = dealerEnrolments.TotalCount,
            };

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

            await SetViewBags(settings, "index");

            return View(new EnrolmentViewModel() { Enrolments = new List<Enrolment>() });
        }

        [Route("approve-enrolment/{imei}")]
        public async Task<IActionResult> ApproveEnrolment(string imei)
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


            if (existingEnrolment != null && existingEnrolment.Status == EnrolmentStatus.Pending)
            {
                await _enrolmentService.ApproveEnrolment(existingEnrolment);
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
        public async Task<IActionResult> AddEnrolment(Enrolment enrolment)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            var response = new EnrolmentViewModel();
            if(string.IsNullOrEmpty(enrolment.IMEI))
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
            if(order?.NationalId != enrolment.AccountId.ToString())
            {
                response.Errors.Add("The National Id Number does not match the one on this order");
            }

            if (order?.IMEI != enrolment.IMEI)
            {
                response.Errors.Add("There IMEI Number does not match the one on this order");
            }

            if (IsValidImei(enrolment.IMEI) == false)
            {
                response.Errors.Add("The IMEI Number is not valid, please check the number and correct.");
            }

            var existingEnrolment = await _enrolmentService.GetByImeiNumberAsync(enrolment.IMEI);

            if(existingEnrolment != null)
            {
                response.Errors.Add("The IMEI Number is registered to another order");
            }

            //Validate IMEI
            await SetViewBags(settings, "index");

            if(response.Errors.Any())
            {
                response.Enrolments.Add(enrolment);

                return View(response);
            }

            try
            {
                enrolment.Status = EnrolmentStatus.New;
                enrolment.DealerId = settings.DealerId;
                enrolment.Updated = DateTime.Now;
                enrolment.Created = DateTime.Now;
                enrolment.UpdatedBy = settings.Name;
                enrolment.DealerId = settings.DealerId;
                enrolment.Id = Guid.NewGuid();
                await _enrolmentService.CreateEnrolmentasync(enrolment, order);
            }
            catch (Exception)
            {
                response.Errors.Add("There was an Error processing your request. Please contact the system administrator.");
                return View(response);
            }

            return RedirectToAction("Index");
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

        public static bool IsValidImei(string imei)
        {
            if (string.IsNullOrWhiteSpace(imei))
                return false;

            if (imei.Length != 15 || !imei.All(char.IsDigit))
                return false;

            int sum = 0;

            for (int i = 0; i < 14; i++)
            {
                int digit = imei[i] - '0';

                if (i % 2 == 1) // Double every second digit
                {
                    digit *= 2;
                    if (digit > 9)
                        digit -= 9;
                }

                sum += digit;
            }

            int checkDigit = (10 - (sum % 10)) % 10;

            return checkDigit == (imei[14] - '0');
        }
    }
}
