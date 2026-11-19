using Microsoft.AspNetCore.Mvc;
using Ranalo.Configuration;
using Ranalo.DataStore.DataModels;
using Ranalo.Services;

namespace Ranalo.Controllers
{
    // P&L, monthly comparison chart, and best-effort balance sheet -- moved
    // off the Admin Dashboard onto its own page. Admin-only, same
    // sensitivity as Operating Expenses.
    //
    // Opening the page (this month) shows the copy worked out at 04:00
    // (FinancialsCache); refresh=true works it out again now. Any other
    // period or date range is worked out live (FinancialsBuilder).
    [LoadUserSettingsFromCookie]
    public class FinancialsController : Controller
    {
        private readonly IFinancialsBuilder _builder;
        private readonly IFinancialsCache _cache;

        public FinancialsController(IFinancialsBuilder builder, IFinancialsCache cache)
        {
            _builder = builder;
            _cache = cache;
        }

        [HttpGet]
        [Route("financials")]
        public async Task<IActionResult> Index(string period = "month", DateTime? from = null, DateTime? to = null, bool refresh = false)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            if (settings.RoleId != UserRole.Admin)
            {
                return RedirectToAction("Index", "Home");
            }

            ViewBag.BackLink = "financials";
            ViewBag.IsAdmin = true;
            ViewBag.IsApprover = false;
            ViewBag.IsDealer = false;
            ViewBag.UserName = settings.KnownAs;

            var model = FinancialsBuilder.IsDefaultView(period, from, to)
                ? await _cache.GetAsync(refresh)
                : await _builder.BuildAsync(period, from, to);
            ViewBag.IsDailyCopy = FinancialsBuilder.IsDefaultView(period, from, to);

            return View(model);
        }
    }
}
