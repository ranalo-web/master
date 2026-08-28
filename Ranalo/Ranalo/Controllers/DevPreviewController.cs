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
        [Route("dev-preview/customer-care-dashboard")]
        public IActionResult CustomerCareDashboard()
        {
            if (!_env.IsDevelopment())
            {
                return NotFound();
            }

            ViewBag.BackLink = "customer-care-dashboard";
            ViewBag.IsAdmin = false;
            ViewBag.IsApprover = false;
            ViewBag.IsDealer = false;
            ViewBag.IsCustomerCare = true;
            ViewBag.UserName = "Preview Customer Care";

            var model = CustomerCareDashboardSampleData.Build();

            return View("~/Views/CustomerCareDashboard/Index.cshtml", model);
        }
    }
}
