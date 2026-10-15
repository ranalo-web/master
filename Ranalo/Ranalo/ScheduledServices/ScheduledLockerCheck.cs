using Ranalo.Services;

namespace Ranalo.ScheduledServices
{
    // Daily: every phone the app locks through Knox or Transsion, with an
    // open contract, must still be in Knox/Transsion. One that isn't gets a
    // Recovery ("Lock lost") so it's seen the next morning. Added after
    // 41109528 dropped out of Knox in June 2026 and the daily lock jobs kept
    // sending locks into nothing until October. See
    // RecoveryService.CheckLockersAsync.
    public class ScheduledLockerCheck : BackgroundService
    {
        // 5:15 AM UTC (8:15 AM in Kenya), after ScheduledRecoveryTranssionCheck (5:00).
        private static readonly TimeSpan RunTimeUtc = new TimeSpan(5, 15, 0);

        private readonly ILogger<ScheduledLockerCheck> _logger;
        private readonly IServiceScopeFactory _scopeFactory;

        public ScheduledLockerCheck(ILogger<ScheduledLockerCheck> logger, IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Locker check started at {time} (UTC)", DateTime.UtcNow);

            while (!stoppingToken.IsCancellationRequested)
            {
                var utcNow = DateTime.UtcNow;
                var nextRunUtc = utcNow.Date.Add(RunTimeUtc);
                if (nextRunUtc <= utcNow)
                {
                    nextRunUtc = nextRunUtc.AddDays(1);
                }

                try
                {
                    await Task.Delay(nextRunUtc - utcNow, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }

                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var recoveries = scope.ServiceProvider.GetRequiredService<IRecoveryService>();
                    var opened = await recoveries.CheckLockersAsync();
                    foreach (var line in opened)
                    {
                        _logger.LogWarning("Locker check: lock lost, Recovery opened -- {line}", line);
                    }
                    _logger.LogInformation("Locker check completed: {count} phone(s) no longer with their locker", opened.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error running the locker check.");
                }
            }

            _logger.LogInformation("Locker check stopped at {time} (UTC)", DateTime.UtcNow);
        }
    }
}
