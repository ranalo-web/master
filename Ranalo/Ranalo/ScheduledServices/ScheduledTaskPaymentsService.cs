using Ranalo.Woocommece.Api.Services;

namespace Ranalo.ScheduledServices
{
    public class ScheduledTaskPaymentsService : BackgroundService
    {
        private readonly ILogger<ScheduledTaskPaymentsService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly TimeSpan _interval = TimeSpan.FromMinutes(10); // Run every 30 min

        public ScheduledTaskPaymentsService(
            ILogger<ScheduledTaskPaymentsService> logger,
            IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Kose Payments Task started at: {time}", DateTime.UtcNow);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {

                    // Example: perform a database operation
                    _logger.LogInformation("Running scheduled Kose payments task at: {time}", DateTime.UtcNow);
                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var syncService = scope.ServiceProvider.GetRequiredService<ISyncService>();
                        var inactiveUsers = await syncService.SyncPayments();
                        if(inactiveUsers != null)
                        {
                            foreach (var order in inactiveUsers)
                            {
                                _logger.LogInformation("Synced payment: {order}", order);
                                // Possibly send email reminders, clean up data, etc.
                            }
                        }
                        else
                        {
                            _logger.LogInformation("Synced payment has no records to sync");
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
