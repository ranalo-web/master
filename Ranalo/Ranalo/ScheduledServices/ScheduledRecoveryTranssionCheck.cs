using Ranalo.Services;

namespace Ranalo.ScheduledServices
{
    // Daily: Recovery phones sent to Transsion that Transsion now has active
    // are checked and moved to Transsion, so the daily lock jobs manage them
    // -- no one has to press Check in Transsion on each. The PayTrigger
    // webhook does this the moment a phone activates; this catches any
    // notification that didn't arrive. See RecoveryService.SweepTranssionAsync.
    public class ScheduledRecoveryTranssionCheck : BackgroundService
    {
        // 5 AM UTC (8 AM in Kenya), after ScheduledWriteOffCheck (4:30).
        private static readonly TimeSpan RunTimeUtc = new TimeSpan(5, 0, 0);

        private readonly ILogger<ScheduledRecoveryTranssionCheck> _logger;
        private readonly IServiceScopeFactory _scopeFactory;

        public ScheduledRecoveryTranssionCheck(ILogger<ScheduledRecoveryTranssionCheck> logger, IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Recovery Transsion check started at {time} (UTC)", DateTime.UtcNow);

            while (!stoppingToken.IsCancellationRequested)
            {
                var utcNow = DateTime.UtcNow;
                var nextRunUtc = utcNow.Date.Add(RunTimeUtc);
                if (nextRunUtc <= utcNow)
                {
                    nextRunUtc = nextRunUtc.AddDays(1);
                }

                _logger.LogInformation("Next scheduled Recovery Transsion check: {next}", nextRunUtc);

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
                    var moved = await recoveries.SweepTranssionAsync();
                    _logger.LogInformation("Recovery Transsion check completed: {moved} phone(s) active in Transsion and moved", moved);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error running the Recovery Transsion check.");
                }
            }

            _logger.LogInformation("Recovery Transsion check stopped at {time} (UTC)", DateTime.UtcNow);
        }
    }
}
