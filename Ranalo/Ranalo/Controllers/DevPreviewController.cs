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
        [Route("dev-preview/agent-dashboard")]
        public IActionResult AgentDashboard()
        {
            if (!_env.IsDevelopment())
            {
                return NotFound();
            }

            ViewBag.BackLink = "agent-dashboard";
            ViewBag.IsAdmin = false;
            ViewBag.IsApprover = false;
            ViewBag.IsDealer = false;
            ViewBag.IsAgent = true;
            ViewBag.UserName = "Preview Agent";

            var model = AgentDashboardSampleData.Build();

            return View("~/Views/AgentDashboard/Index.cshtml", model);
        }
    }
}
