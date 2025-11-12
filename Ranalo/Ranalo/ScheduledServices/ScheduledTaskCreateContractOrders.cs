using Ranalo.Woocommece.Api.Services;

namespace Ranalo.ScheduledServices
{
    public class ScheduledTaskCreateContractOrders : BackgroundService
    {

        private readonly ILogger<ScheduledTaskCreateContractOrders> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly TimeSpan _interval = TimeSpan.FromMinutes(120); // Run every 30 min

        public ScheduledTaskCreateContractOrders(
            ILogger<ScheduledTaskCreateContractOrders> logger,
            IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Create contracts from orders started at: {time}", DateTime.UtcNow);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Example: perform a database operation
                    _logger.LogInformation("Running scheduled task Create contracts at: {time}", DateTime.UtcNow);

                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var syncService = scope.ServiceProvider.GetRequiredService<ISyncService>();
                        var inactiveUsers = await syncService.CreateContractsForEligibleOrders();
                        foreach (var order in inactiveUsers)
                        {
                            _logger.LogInformation("Created contract for order: {user}", order);
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
                    _logger.LogError(ex, "Error while running scheduled task Create contracts.");
                }
            }

            _logger.LogInformation("ScheduledTaskCreateContractOrders stopped at: {time}", DateTime.UtcNow);
        }
    }
}
