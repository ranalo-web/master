using Ranalo.Services;

namespace Ranalo.ScheduledServices
{
    // Every 10 minutes: starts each self-service plan whose first day has
    // been paid, and expires those not paid within 24 hours
    // (SelfRestructureService). The app also does this for the customer's
    // own account when they open it, so a paid plan starts at once.
    public class ScheduledSelfRestructures : BackgroundService
    {
        private readonly ILogger<ScheduledSelfRestructures> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly TimeSpan _interval = TimeSpan.FromMinutes(10);

        public ScheduledSelfRestructures(ILogger<ScheduledSelfRestructures> logger, IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var started = await scope.ServiceProvider.GetRequiredService<ISelfRestructureService>().ProcessPendingAsync();
                    if (started > 0) _logger.LogInformation("Self-service plans started: {Started}", started);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Self-service plans check failed.");
                }

                try
                {
                    await Task.Delay(_interval, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    // Shutting down.
                }
            }
        }
    }
}
