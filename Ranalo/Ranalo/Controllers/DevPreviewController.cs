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
        [Route("dev-preview/dealer-dashboard")]
        public IActionResult DealerDashboard()
        {
            if (!_env.IsDevelopment())
            {
                return NotFound();
            }

            ViewBag.BackLink = "dealer-dashboard";
            ViewBag.IsAdmin = false;
            ViewBag.IsApprover = false;
            ViewBag.IsDealer = true;
            ViewBag.UserName = "Preview Dealer";

            var model = DealerDashboardSampleData.Build();

            return View("~/Views/DealerDashboard/Index.cshtml", model);
        }
    }
}
