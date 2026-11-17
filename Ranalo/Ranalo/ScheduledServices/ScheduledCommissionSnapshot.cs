using Ranalo.DataStore;

namespace Ranalo.ScheduledServices
{
    // Works the commission snapshot out three times a day -- 06:00, 12:00 and
    // 18:00 Kenya time -- and once shortly after the app starts. Dashboards,
    // collections and reports read the snapshot instead of running the heavy
    // commission query on every page load (see DashboardReportRepository,
    // "Commission snapshot"). Pay Commissions stays live.
    public class ScheduledCommissionSnapshot : BackgroundService
    {
        // UTC hours: 06:00, 12:00, 18:00 in Nairobi (UTC+3).
        public static readonly int[] RunHoursUtc = { 3, 9, 15 };
        private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(30);

        private readonly ILogger<ScheduledCommissionSnapshot> _logger;
        private readonly IServiceScopeFactory _scopeFactory;

        public ScheduledCommissionSnapshot(ILogger<ScheduledCommissionSnapshot> logger, IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        // The next scheduled run after nowUtc.
        public static DateTime NextRunUtc(DateTime nowUtc)
        {
            foreach (var hour in RunHoursUtc)
            {
                var candidate = nowUtc.Date.AddHours(hour);
                if (candidate > nowUtc)
                {
                    return candidate;
                }
            }
            return nowUtc.Date.AddDays(1).AddHours(RunHoursUtc[0]);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var delay = StartupDelay;
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(delay, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }

                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var repository = scope.ServiceProvider.GetRequiredService<IDashboardReportRepository>();
                    var started = DateTime.UtcNow;
                    await repository.RefreshCommissionSnapshotAsync();
                    _logger.LogInformation("Commission snapshot refreshed in {Seconds:0.0}s", (DateTime.UtcNow - started).TotalSeconds);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Commission snapshot refresh failed; pages keep the previous one");
                }

                var now = DateTime.UtcNow;
                delay = NextRunUtc(now) - now;
            }
        }
    }
}
