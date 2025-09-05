using Ranalo.Services;
using Ranalo.Woocommece.Api.Services;

namespace Ranalo.ScheduledServices
{
    public class ScheduledTaskWooOrdersService : BackgroundService
    {
        private readonly ILogger<ScheduledTaskWooOrdersService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly TimeSpan _interval = TimeSpan.FromMinutes(30); // Run every 30 min

        public ScheduledTaskWooOrdersService(
            ILogger<ScheduledTaskWooOrdersService> logger,
            IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Woo Orders Task started at: {time}", DateTime.UtcNow);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Example: perform a database operation
                    _logger.LogInformation("Running scheduled task at: {time}", DateTime.UtcNow);

                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var syncService = scope.ServiceProvider.GetRequiredService<ISyncService>();
                        var inactiveUsers = await syncService.SyncWooOrders();
                        foreach (var order in inactiveUsers)
                        {
                            _logger.LogInformation("Synced order: {user}", order.Id);
                            // Possibly send email reminders, clean up data, etc.
                        }
                    }

                    // Wait until next run
                    await Task.Delay(_interval, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    // Ignore when shutting down
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while running scheduled task.");
                }
            }

            _logger.LogInformation("ScheduledTaskService stopped at: {time}", DateTime.UtcNow);
        }
    }
}
