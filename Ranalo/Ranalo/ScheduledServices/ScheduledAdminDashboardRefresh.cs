using Ranalo.Services;

namespace Ranalo.ScheduledServices
{
    // Rebuilds the shared Admin Dashboard ("This month", the page admins land
    // on) every 30 minutes, and once a minute after the app starts, so no
    // admin waits for it to be worked out (AdminDashboardCache).
    public class ScheduledAdminDashboardRefresh : BackgroundService
    {
        private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(1);

        private readonly IAdminDashboardCache _dashboards;
        private readonly ILogger<ScheduledAdminDashboardRefresh> _logger;

        public ScheduledAdminDashboardRefresh(IAdminDashboardCache dashboards, ILogger<ScheduledAdminDashboardRefresh> logger)
        {
            _dashboards = dashboards;
            _logger = logger;
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

                delay = AdminDashboardCache.MaxAge;
            }
        }
    }
}
