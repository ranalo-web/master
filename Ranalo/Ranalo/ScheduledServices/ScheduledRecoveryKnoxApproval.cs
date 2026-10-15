using Ranalo.Services;

namespace Ranalo.ScheduledServices
{
    // Daily: Samsung recoveries sent to Knox that Knox now has are approved,
    // and ones whose lock was sent and that Knox reports active are moved to
    // Knox (so the daily lock jobs manage them). See RecoveryService.SweepKnoxAsync.
    public class ScheduledRecoveryKnoxApproval : BackgroundService
    {
        // 5:05 AM UTC (8:05 AM in Kenya), between the Transsion check (5:00)
        // and the locker check (5:15).
        private static readonly TimeSpan RunTimeUtc = new TimeSpan(5, 5, 0);

        private readonly ILogger<ScheduledRecoveryKnoxApproval> _logger;
        private readonly IServiceScopeFactory _scopeFactory;

        public ScheduledRecoveryKnoxApproval(ILogger<ScheduledRecoveryKnoxApproval> logger, IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Recovery Knox approval started at {time} (UTC)", DateTime.UtcNow);

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
                    var (approved, moved) = await recoveries.SweepKnoxAsync();
                    _logger.LogInformation("Recovery Knox approval completed: {approved} approved in Knox, {moved} moved to Knox", approved, moved);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error running the Recovery Knox approval.");
                }
            }

            _logger.LogInformation("Recovery Knox approval stopped at {time} (UTC)", DateTime.UtcNow);
        }
    }
}
