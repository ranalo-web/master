using System.Collections.Concurrent;
using Ranalo.DataStore;
using Ranalo.Models;

namespace Ranalo.Services
{
    // The Admin Dashboard runs about 15 queries (account details over every
    // payment, revenue for 12 months, write-offs, loan book...). Admins land
    // on it after login, so every login ran all of them, and on 2026-11-17
    // that kept the S1 database at 100%. Now one copy per period is built
    // and shared by every admin: rebuilt in the background three times a
    // day, just after the commission snapshot (ScheduledAdminDashboardRefresh),
    // built once on demand when there's none, and only one build per period
    // runs at a time. "Refresh now" on the page forces a rebuild.
    public interface IAdminDashboardCache
    {
        Task<AdminDashboardViewModel> GetAsync(string? period, bool refresh = false);
    }

    public class AdminDashboardCache : IAdminDashboardCache
    {
        // Longer than the longest gap between scheduled rebuilds (18:05 to
        // 06:05, 12 hours), so a copy is only built on a visit when there's
        // none for that period yet (after a restart, or a period nobody's
        // opened today).
        public static readonly TimeSpan MaxAge = TimeSpan.FromHours(13);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<AdminDashboardCache> _logger;
        private readonly ConcurrentDictionary<string, AdminDashboardViewModel> _models = new();
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _gates = new();

        public AdminDashboardCache(IServiceScopeFactory scopeFactory, ILogger<AdminDashboardCache> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public static bool IsFresh(AdminDashboardViewModel model, DateTime nowUtc) =>
            model.BuiltAtUtc.HasValue && nowUtc - model.BuiltAtUtc.Value < MaxAge;

        public async Task<AdminDashboardViewModel> GetAsync(string? period, bool refresh = false)
        {
            // The resolved period ("month" for none or anything unknown), so a
            // plain visit and ?period=month share one copy.
            var key = AdminDashboardRules.ResolvePeriod(period, DateTime.Now).Period;
            if (!refresh && _models.TryGetValue(key, out var cached) && IsFresh(cached, DateTime.UtcNow))
            {
                return cached;
            }

            var gate = _gates.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
            var askedAtUtc = DateTime.UtcNow;
            await gate.WaitAsync();
            try
            {
                // Built by someone else while this request waited: use theirs.
                if (_models.TryGetValue(key, out cached)
                    && (cached.BuiltAtUtc >= askedAtUtc || (!refresh && IsFresh(cached, DateTime.UtcNow))))
                {
                    return cached;
                }

                var model = await BuildAsync(period);
                _models[key] = model;
                return model;
            }
            finally
            {
                gate.Release();
            }
        }

        private async Task<AdminDashboardViewModel> BuildAsync(string? period)
        {
            using var scope = _scopeFactory.CreateScope();
            var dashboard = scope.ServiceProvider.GetRequiredService<IDashboardReportService>();
            var writeOffs = scope.ServiceProvider.GetRequiredService<IWriteOffRepository>();

            var model = await dashboard.GetAdminDashboardAsync(period);

            try
            {
                var window = AdminDashboardRules.ResolvePeriod(model.Period, DateTime.Now);
                model.WriteOffsPeriod = await writeOffs.GetPeriodSummaryAsync(window.From, window.ToExclusive);
                model.WriteOffsThisMonth = model.WriteOffsPeriod;
                model.WriteOffsLastMonth = await writeOffs.GetPeriodSummaryAsync(window.PriorFrom, window.PriorToExclusive);

                var summary = await writeOffs.GetSummaryAsync();
                var pending = summary.Where(s => s.Status is "pending" or "held").ToList();
                model.WriteOffsPendingCount = pending.Sum(s => s.Count);
                model.WriteOffsPendingBalance = pending.Sum(s => s.Outstanding);

                // Still to collect: every open contract that owes money,
                // approved write-offs left out (same as the Financials book).
                var ageing = await writeOffs.GetLoanBookAgeingAsync(DateTime.UtcNow.AddHours(3).Date);
                model.LoanBookOutstanding = ageing.Where(b => !b.IsWrittenOff).Sum(b => b.Outstanding);
                model.LoanBookAccounts = ageing.Where(b => !b.IsWrittenOff).Sum(b => b.Accounts);
            }
            catch (Exception ex)
            {
                // e.g. Database/WriteOffs/001 not applied -- those cards show zeros.
                _logger.LogError(ex, "Admin dashboard: write-off figures unavailable");
            }

            model.Actions = AdminDashboardRules.BuildActions(model);
            model.BuiltAtUtc = DateTime.UtcNow;
            return model;
        }
    }
}
