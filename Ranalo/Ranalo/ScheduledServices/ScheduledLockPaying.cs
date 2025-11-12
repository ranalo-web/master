using Ranalo.DataStore;
using Ranalo.Models;
using Ranalo.Services;

namespace Ranalo.ScheduledServices
{
    public class ScheduledLockPaying : BackgroundService
    {
        private readonly ILogger<ScheduledLockPaying> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly TimeSpan _interval = TimeSpan.FromMinutes(10); // Run every 30 min

        public ScheduledLockPaying(
            ILogger<ScheduledLockPaying> logger,
            IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Live lock Auto: {time}", DateTime.UtcNow);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Example: perform a database operation
                    _logger.LogInformation("Live lock Auto scheduled task at: {time}", DateTime.UtcNow);

                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var syncService = scope.ServiceProvider.GetRequiredService<IDeviceProcessor>();
                        //IPaymentsRepository
                        var reminderService = scope.ServiceProvider.GetRequiredService<IApplicationReportService>();
                        var paymentsRepository = scope.ServiceProvider.GetRequiredService<IPaymentsRepository>();
                        var inactiveUsers = await Process(syncService, reminderService, paymentsRepository);
                        foreach (var order in inactiveUsers)
                        {
                            _logger.LogInformation("lock Auto for: {user}", order.AccountId);
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
                    _logger.LogError(ex, "Error while running Live lock Auto restructured scheduled task.");
                }
            }

            _logger.LogInformation("Live lock Reminder stopped at: {time}", DateTime.UtcNow);
        }

        public async Task<List<LockTransaction>?> Process(IDeviceProcessor deviceProcessor, IApplicationReportService applicationReportService, IPaymentsRepository paymentsRepository)
        {
            var records = await applicationReportService.GetStatusReportByDealer(null, null, 1, 1000, ""); ;

            // Get all restructured records to remove from the auto restructure list

            if (records == null && records?.StatusReports?.Any() == false)
            { return null; }

            //Only take records where the last payment is in the last 24hrs
            var autoLockRecords = records?.StatusReports;

            //Remove all fully paid
            autoLockRecords?.RemoveAll(x => x.Arrears > 0 && x.LoanBalance < 0);

            // Remove positive arrears
            autoLockRecords?.RemoveAll(x => x.Arrears < 0);

            var devicesToLock = new List<LockTransaction>();
            //Not sure why this removes negative arrears
            //records.Records.RemoveAll(a => a.ArrearsR > 0);

            if (autoLockRecords != null)
            {
                foreach (var account in autoLockRecords)
                {
                    var dailyAll = ((account.Daily) + (account.Weekly / 7) + (account.Monthly / 30));
                    var unitsLeft = SafeDivide(account.Arrears, dailyAll);

                    var now = DateTime.Now;
                    var autoLockDatePmt = now.AddSeconds(Convert.ToDouble(unitsLeft * 60 * 60 * 24));

                    var lockDevice = new LockTransaction()
                    {
                        AccountId = account.AccountNo,
                        FirstName = account.FirstName,
                        AutoLockDate = account.LoanBalance < 1 ? DateTime.MaxValue : autoLockDatePmt
                    };

                    devicesToLock.Add(lockDevice);
                }

                var lockedDevices = await deviceProcessor.ProcessBatchesAsync(devicesToLock);

                return lockedDevices;
            }

            return devicesToLock;
        }

        private static decimal SafeDivide(decimal numerator, decimal denominator)
        {
            return denominator == 0 ? 0 : numerator / denominator;
        }
    }
}

