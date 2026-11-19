using Ranalo.DataStore;
using Ranalo.Models;

namespace Ranalo.Services
{
    // Works out the whole Financials page for a period: income statement,
    // write-offs, age of debt, loss given default, balance sheet.
    public interface IFinancialsBuilder
    {
        Task<FinancialsViewModel> BuildAsync(string? period, DateTime? from, DateTime? to);
    }

    public class FinancialsBuilder : IFinancialsBuilder
    {
        private static readonly Dictionary<string, string> PeriodNames = new()
        {
            ["week"] = "Week",
            ["month"] = "Month",
            ["ytd"] = "Year to Date",
            ["year"] = "Last 12 Months",
        };

        private readonly IDashboardReportService _dashboardReportService;
        private readonly IWriteOffRepository _writeOffRepository;
        private readonly ILogger<FinancialsBuilder> _logger;

        public FinancialsBuilder(IDashboardReportService dashboardReportService, IWriteOffRepository writeOffRepository, ILogger<FinancialsBuilder> logger)
        {
            _dashboardReportService = dashboardReportService;
            _writeOffRepository = writeOffRepository;
            _logger = logger;
        }

        // The page that opens with no choice made (and the "Month" button):
        // the one kept ready by FinancialsCache.
        public static bool IsDefaultView(string? period, DateTime? from, DateTime? to)
        {
            var p = (period ?? "month").ToLowerInvariant();
            return p == "month" || (p == "custom" && !(from.HasValue && to.HasValue));
        }

        public async Task<FinancialsViewModel> BuildAsync(string? period, DateTime? from, DateTime? to)
        {
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

            // The age of debt and balance sheet stand at the end of a past
            // custom range, else today.
            var kenyaToday = DateTime.UtcNow.AddHours(3).Date;
            model.AsOfDate = period == "custom" && to!.Value.Date < kenyaToday ? to.Value.Date : kenyaToday;

            try
            {
                model.WriteOffs = await _writeOffRepository.GetPeriodSummaryAsync(fromDate, toDateExclusive);
                model.LoanBookAgeing = await _writeOffRepository.GetLoanBookAgeingAsync(model.AsOfDate);
                (model.Lgd, model.LgdLeftOut) = await _dashboardReportService.GetLossGivenDefaultAsync();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)   // stopped: another page was opened
            {
                // e.g. Database/WriteOffs/001 not applied -- the rest of the page still works.
                _logger.LogError(ex, "Financials: write-off figures unavailable");
                model.WriteOffsUnavailable = true;
            }

            model.BuiltAtUtc = DateTime.UtcNow;
            return model;
        }
    }

    // The default Financials page (this month) is worked out once a day at
    // 04:00 Kenya time when the database is quiet (ScheduledFinancialsRefresh),
    // and shortly after the app starts. Opening the page shows that copy at
    // once; "Refresh now" works it out again. Any other period or date range
    // is worked out live when it's chosen.
    public interface IFinancialsCache
    {
        Task<FinancialsViewModel> GetAsync(bool refresh = false);
    }

    public class FinancialsCache : IFinancialsCache
    {
        // A little over a day, so a missed 04:00 run (app restarted at the
        // wrong moment) is made up on the next visit rather than showing an
        // older copy.
        public static readonly TimeSpan MaxAge = TimeSpan.FromHours(25);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly SemaphoreSlim _gate = new(1, 1);
        private FinancialsViewModel? _model;

        public FinancialsCache(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        public static bool IsFresh(FinancialsViewModel model, DateTime nowUtc) =>
            model.BuiltAtUtc.HasValue && nowUtc - model.BuiltAtUtc.Value < MaxAge;

        public async Task<FinancialsViewModel> GetAsync(bool refresh = false)
        {
            var cached = _model;
            if (!refresh && cached != null && IsFresh(cached, DateTime.UtcNow))
            {
                return cached;
            }

            var askedAtUtc = DateTime.UtcNow;
            await _gate.WaitAsync();
            try
            {
                // Built by someone else while this request waited: use theirs.
                cached = _model;
                if (cached != null
                    && (cached.BuiltAtUtc >= askedAtUtc || (!refresh && IsFresh(cached, DateTime.UtcNow))))
                {
                    return cached;
                }

                // Own scope: the build is shared, so it mustn't be stopped
                // when the person who asked leaves the page.
                using var scope = _scopeFactory.CreateScope();
                var model = await scope.ServiceProvider.GetRequiredService<IFinancialsBuilder>().BuildAsync("month", null, null);
                _model = model;
                return model;
            }
            finally
            {
                _gate.Release();
            }
        }
    }
}
