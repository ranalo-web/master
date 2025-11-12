using Ranalo.Woocommece.Api.Services;

namespace Ranalo.ScheduledServices
{
    public class ScheduledTaskDeviceUnlockService : BackgroundService
    {
        private readonly ILogger<ScheduledTaskDeviceUnlockService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly TimeSpan _interval = TimeSpan.FromMinutes(10); // Run every 30 min

        public ScheduledTaskDeviceUnlockService(
            ILogger<ScheduledTaskDeviceUnlockService> logger,
            IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Device unlock Task started at: {time}", DateTime.UtcNow);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Example: perform a database operation
                    _logger.LogInformation("Running scheduled Devices pull task at: {time}", DateTime.UtcNow);

                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var syncService = scope.ServiceProvider.GetRequiredService<ISyncService>();
                        var inactiveUsers = await syncService.DeviceUnlockPull();
                        foreach (var device in inactiveUsers)
                        {
                            _logger.LogInformation("New Device Added: {order}", device.Id);
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
                    _logger.LogError(ex, "Error while running scheduled ScheduledTaskDeviceUnlockService.");
                }
            }

            _logger.LogInformation("ScheduledTaskDeviceUnlockService stopped at: {time}", DateTime.UtcNow);
        }
    }
}
