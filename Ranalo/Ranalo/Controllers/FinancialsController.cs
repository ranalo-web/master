using Microsoft.AspNetCore.Mvc;
using Ranalo.Configuration;
using Ranalo.DataStore;
using Ranalo.DataStore.DataModels;
using Ranalo.Models;
using Ranalo.Services;

namespace Ranalo.Controllers
{
    // P&L, monthly comparison chart, and best-effort balance sheet -- moved
    // off the Admin Dashboard onto its own page. Admin-only, same
    // sensitivity as Operating Expenses.
    [LoadUserSettingsFromCookie]
    public class FinancialsController : Controller
    {
        private readonly IDashboardReportService _dashboardReportService;
        private readonly IWriteOffRepository _writeOffRepository;
        private readonly ILogger<FinancialsController> _logger;

        private static readonly Dictionary<string, string> PeriodNames = new()
        {
            ["week"] = "Week",
            ["month"] = "Month",
            ["ytd"] = "Year to Date",
            ["year"] = "Last 12 Months",
        };

        public FinancialsController(IDashboardReportService dashboardReportService, IWriteOffRepository writeOffRepository, ILogger<FinancialsController> logger)
        {
            _dashboardReportService = dashboardReportService;
            _writeOffRepository = writeOffRepository;
            _logger = logger;
        }

        [HttpGet]
        [Route("financials")]
        public async Task<IActionResult> Index(string period = "month", DateTime? from = null, DateTime? to = null)
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

            // Same Week/Month/YTD/Last 12 Months windows as All Payments,
            // plus "all" and a custom From/To range (e.g. a past year's books).
            period = (period ?? "month").ToLowerInvariant();
            DateTime? fromDate, toDateExclusive;
            string periodLabel;
            if (period == "custom" && from.HasValue && to.HasValue)
            {
                if (to < from)
                {
                    (from, to) = (to, from);
                }
                fromDate = from.Value.Date;
                toDateExclusive = to.Value.Date.AddDays(1);
                periodLabel = $"{from.Value:dd MMM yyyy} – {to.Value:dd MMM yyyy}";
            }
            else
            {
                if (period == "custom")
                {
                    period = "month";
                }
                (fromDate, toDateExclusive) = PeriodWindowHelper.Resolve(period);
                periodLabel = PeriodNames.TryGetValue(period, out var name) ? name : "All Time";
                if (!PeriodNames.ContainsKey(period))
                {
                    period = "all";
                }
            }

            var model = await _dashboardReportService.GetFinancialsAsync(fromDate, toDateExclusive);
            model.Period = period;
            model.PeriodLabel = periodLabel;
            model.FromDate = period == "custom" ? from : fromDate;
            model.ToDate = period == "custom" ? to : toDateExclusive?.AddDays(-1);

            try
            {
                model.WriteOffs = await _writeOffRepository.GetPeriodSummaryAsync(fromDate, toDateExclusive);
                model.LoanBookAgeing = await _writeOffRepository.GetLoanBookAgeingAsync(DateTime.UtcNow.AddHours(3).Date);
            }
            catch (Exception ex)
            {
                // e.g. Database/WriteOffs/001 not applied -- the rest of the page still works.
                _logger.LogError(ex, "Financials: write-off figures unavailable");
                model.WriteOffsUnavailable = true;
            }

            return View(model);
        }
    }
}
