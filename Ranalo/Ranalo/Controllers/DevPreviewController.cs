using Microsoft.AspNetCore.Mvc;
using Ranalo.Models;

namespace Ranalo.Controllers
{
    // Local-only helper for previewing dashboard layouts with mock data, bypassing
    // login and the database. Not wired to any real service/repository. Only
    // responds when running in Development, and should be removed before this
    // branch merges to main.
    public class DevPreviewController : Controller
    {
        private readonly IWebHostEnvironment _env;

        public DevPreviewController(IWebHostEnvironment env)
        {
            _env = env;
        }

        [HttpGet]
        [Route("dev-preview/admin-dashboard")]
        public IActionResult AdminDashboard()
        {
            if (!_env.IsDevelopment())
            {
                return NotFound();
            }

            ViewBag.BackLink = "index";
            ViewBag.IsAdmin = true;
            ViewBag.IsApprover = false;
            ViewBag.IsDealer = false;
            ViewBag.UserName = "Preview Admin";
            ViewData["IsAdmin"] = true;

            var model = new AwaitingApprovalViewModel
            {
                CurrentPage = 1,
                TotalPages = 3,
                SearchTerm = "",
                AwaitingApprovals = new List<AwaitingApprovalDto>
                {
                    new AwaitingApprovalDto { Id = 1, OrderId = 10231, Status = "approval-waiting", FirstName = "Jane", LastName = "Wanjiru", Email = "jane.wanjiru@example.com", Phone = "0712 345 678", DealerRef = "Nairobi Mobile Hub", MpesaDepositRef = "QGH7XJ2K", DateCreated = DateTime.Now.AddHours(-3), DaysUnpaid = 0 },
                    new AwaitingApprovalDto { Id = 2, OrderId = 10232, Status = "approved", FirstName = "Brian", LastName = "Otieno", Email = "brian.otieno@example.com", Phone = "0722 456 789", DealerRef = "Kisumu Electronics", MpesaDepositRef = "QGH8YK3L", DateCreated = DateTime.Now.AddHours(-6), DaysUnpaid = 0 },
                    new AwaitingApprovalDto { Id = 3, OrderId = 10233, Status = "cancelled", FirstName = "Amina", LastName = "Hassan", Email = "amina.hassan@example.com", Phone = "0733 567 890", DealerRef = "Mombasa Devices Ltd", MpesaDepositRef = "QGH9ZM4N", DateCreated = DateTime.Now.AddDays(-1), DaysUnpaid = 2 },
                    new AwaitingApprovalDto { Id = 4, OrderId = 10234, Status = "rejected", FirstName = "Peter", LastName = "Kamau", Email = "peter.kamau@example.com", Phone = "0744 678 901", DealerRef = "Nakuru Phone Shop", MpesaDepositRef = "QGH1AB5P", DateCreated = DateTime.Now.AddDays(-2), DaysUnpaid = 5 },
                    new AwaitingApprovalDto { Id = 5, OrderId = 10235, Status = "approval-waiting", FirstName = "Grace", LastName = "Njeri", Email = "grace.njeri@example.com", Phone = "0755 789 012", DealerRef = "Eldoret Tech", MpesaDepositRef = "QGH2CD6Q", DateCreated = DateTime.Now.AddHours(-1), DaysUnpaid = 0 },
                }
            };

            return View("~/Views/Home/Index.cshtml", model);
        }
    }
}
