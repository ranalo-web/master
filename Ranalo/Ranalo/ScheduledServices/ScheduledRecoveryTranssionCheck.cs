using Ranalo.Services;

namespace Ranalo.ScheduledServices
{
    // Hourly: Recovery phones sent to Transsion that Transsion now has
    // active are checked and moved to Transsion, so the daily lock jobs
    // manage them -- no one has to press Check in Transsion on each. The
    // PayTrigger webhook does the same the moment a phone activates; this
    // catches any notification that didn't arrive. See
    // RecoveryService.SweepTranssionAsync.
    public class ScheduledRecoveryTranssionCheck : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

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
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var recoveries = scope.ServiceProvider.GetRequiredService<IRecoveryService>();
                    var moved = await recoveries.SweepTranssionAsync();
                    if (moved > 0)
                    {
                        _logger.LogInformation("Recovery Transsion check: {moved} phone(s) active in Transsion and moved", moved);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error running the Recovery Transsion check.");
                }

                try
                {
                    await Task.Delay(Interval, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }

            _logger.LogInformation("Recovery Transsion check stopped at {time} (UTC)", DateTime.UtcNow);
        }
    }
}
