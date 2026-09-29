using Ranalo.DataStore;

namespace Ranalo.ScheduledServices
{
    // Nightly write-off check (Database/WriteOffs/001 has the policy):
    //  1. Reinstates written-off accounts that have since paid anything
    //     (dated on the payment day; the 360-day clock restarts from it).
    //  2. Proposes a Pending write-off for every open contract that still
    //     owes money and has gone 360 days without a payment, dated on the
    //     day it crossed that line.
    // It never approves anything -- an admin does that on the review screen.
    public class ScheduledWriteOffCheck : BackgroundService
    {
        private readonly ILogger<ScheduledWriteOffCheck> _logger;
        private readonly IServiceScopeFactory _scopeFactory;

        // 4:30 AM UTC -- after ScheduledDashboardRollup (4 AM), on a settled
        // day's payments.
        private static readonly TimeSpan RunTimeUtc = new TimeSpan(4, 30, 0);

        public ScheduledWriteOffCheck(ILogger<ScheduledWriteOffCheck> logger, IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Write-off check service started at {time} (UTC)", DateTime.UtcNow);

            while (!stoppingToken.IsCancellationRequested)
            {
                var utcNow = DateTime.UtcNow;
                var nextRunUtc = utcNow.Date.Add(RunTimeUtc);
                if (nextRunUtc <= utcNow)
                {
                    nextRunUtc = nextRunUtc.AddDays(1);
                }

                _logger.LogInformation("Next scheduled write-off check: {next}", nextRunUtc);

                try
                {
                    await Task.Delay(nextRunUtc - utcNow, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    _logger.LogInformation("Service cancellation detected, stopping.");
                    break;
                }

                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var repository = scope.ServiceProvider.GetRequiredService<IWriteOffRepository>();
                    var (reinstated, proposed) = await RunAsync(repository);

                    _logger.LogInformation("Write-off check completed: {reinstated} reinstated, {proposed} new write-off(s) pending review",
                        reinstated, proposed);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error running write-off check.");
                }
            }

            _logger.LogInformation("Write-off check service stopped at {time} (UTC)", DateTime.UtcNow);
        }

        // Also run on demand from the review screen ("Run check now").
        // "Today" is Kenya's date (UTC+3), matching how payments are dated.
        public static async Task<(int Reinstated, int Proposed)> RunAsync(IWriteOffRepository repository)
        {
            var reinstated = await repository.ReinstatePaidAsync();
            var proposed = await repository.ProposeNoPaymentWriteOffsAsync(DateTime.UtcNow.AddHours(3).Date);
            return (reinstated, proposed);
        }
    }
}
