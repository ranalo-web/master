using Ranalo.Services;

namespace Ranalo.ScheduledServices
{
    // Rebuilds the shared Admin Dashboard ("This month", the page admins land
    // on) three times a day -- 06:05, 12:05 and 18:05 Kenya time, five minutes
    // after the commission snapshot so it uses fresh commission figures --
    // and once a minute after the app starts, so no admin waits for it
    // (AdminDashboardCache). "Refresh now" on the page rebuilds it any time.
    public class ScheduledAdminDashboardRefresh : BackgroundService
    {
        private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(1);
        public static readonly TimeSpan AfterCommissionSnapshot = TimeSpan.FromMinutes(5);

        private readonly IAdminDashboardCache _dashboards;
        private readonly ILogger<ScheduledAdminDashboardRefresh> _logger;

        public ScheduledAdminDashboardRefresh(IAdminDashboardCache dashboards, ILogger<ScheduledAdminDashboardRefresh> logger)
        {
            _dashboards = dashboards;
            _logger = logger;
        }

        // The next rebuild after nowUtc: five minutes after the next
        // commission snapshot run (or the current one, if that's still ahead).
        public static DateTime NextRunUtc(DateTime nowUtc)
        {
            var next = ScheduledCommissionSnapshot.NextRunUtc(nowUtc - AfterCommissionSnapshot) + AfterCommissionSnapshot;
            return next > nowUtc ? next : ScheduledCommissionSnapshot.NextRunUtc(nowUtc) + AfterCommissionSnapshot;
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
                    var started = DateTime.UtcNow;
                    await _dashboards.GetAsync("month", refresh: true);
                    _logger.LogInformation("Admin dashboard rebuilt in {Seconds:0.0}s", (DateTime.UtcNow - started).TotalSeconds);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Admin dashboard rebuild failed; admins keep the previous copy");
                }

                var now = DateTime.UtcNow;
                delay = NextRunUtc(now) - now;
            }
        }
    }
}
