using Ranalo.DataStore;
using Ranalo.Models;
using Ranalo.Services;
using Ranalo.Services.DeviceLock;

namespace Ranalo.ScheduledServices
{
    public class ScheduledLockFullyPaid : BackgroundService
    {
        private readonly ILogger<ScheduledLockFullyPaid> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly TimeSpan _interval = TimeSpan.FromDays(1); // Run once a day

        public ScheduledLockFullyPaid(
            ILogger<ScheduledLockFullyPaid> logger,
            IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Unlock fully paid started: {time}", DateTime.UtcNow);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Example: perform a database operation
                    _logger.LogInformation("Unlock fully paid scheduled task at: {time}", DateTime.UtcNow);

                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var syncService = scope.ServiceProvider.GetRequiredService<IDeviceProcessor>();
                        //IPaymentsRepository
                        var reminderService = scope.ServiceProvider.GetRequiredService<IApplicationReportService>();
                        var paymentsRepository = scope.ServiceProvider.GetRequiredService<IPaymentsRepository>();
                        var enrolmentService = scope.ServiceProvider.GetRequiredService<IEnrolmentService>();
                        var removalService = scope.ServiceProvider.GetRequiredService<IDeviceRemovalService>();
                        var inactiveUsers = await Process(syncService, reminderService, paymentsRepository, enrolmentService, removalService);
                        foreach (var order in inactiveUsers)
                        {
                            _logger.LogInformation("Unlock fully paid lock Auto for: {user}", order.AccountId);
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
                    _logger.LogError(ex, "Error while running Unlock fully paid.");
                }
            }

            _logger.LogInformation("Unlock fully paid stopped at: {time}", DateTime.UtcNow);
        }

        // Fully paid: set the lock date to 31/12/9999 with the device's own
        // provider so the customer isn't held up, and queue the device for an
        // admin to approve removing it from that provider (Pending Tasks).
        // Nothing is removed here -- see DeviceRemovalService.
        public async Task<List<LockTransaction>?> Process(IDeviceProcessor deviceProcessor,
            IApplicationReportService applicationReportService,
            IPaymentsRepository paymentsRepository,
            IEnrolmentService enrolmentService,
            IDeviceRemovalService removalService)
        {
            var records = await applicationReportService.GetStatusReportByDealer(null, null, 1, 1000, "");

            var fullyPaid = FullyPaidPlanner.Select(records?.StatusReports, DateTime.UtcNow.Year);
            if (!fullyPaid.Any())
            {
                return new List<LockTransaction>();
            }

            var nuovo = new List<LockTransaction>();
            var knox = new List<LockTransaction>();
            var transsion = new List<LockTransaction>();

            foreach (var account in fullyPaid)
            {
                var lockDevice = new LockTransaction()
                {
                    AccountId = account.AccountNo,
                    FirstName = account.FirstName,
                    AutoLockDate = DeviceLockRules.FullyPaidLockDateUtc
                };

                switch (DeviceLockRules.ProviderForLockGroup(account.LockGroup))
                {
                    case LockProvider.Knox: knox.Add(lockDevice); break;
                    case LockProvider.Transsion: transsion.Add(lockDevice); break;
                    default: nuovo.Add(lockDevice); break;
                }
            }

            var processed = new List<LockTransaction>();

            if (nuovo.Any())
            {
                processed.AddRange(await deviceProcessor.ProcessBatchesAsync(nuovo, _logger));
            }

            if (knox.Any())
            {
                processed.AddRange(await enrolmentService.SetFullyPaidKnox(knox));
            }

            if (transsion.Any())
            {
                processed.AddRange(await enrolmentService.SetFullyPaidPayTrigger(transsion));
            }

            var queued = await removalService.QueueAsync(fullyPaid.Select(FullyPaidPlanner.ToTask));
            _logger.LogInformation("Fully paid: {Count} accounts set to 31/12/9999, {Queued} newly queued for removal approval",
                fullyPaid.Count, queued);

            return processed;
        }
    }
}
