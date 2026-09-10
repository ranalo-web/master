using Ranalo.DataStore;
using Ranalo.Models;
using Ranalo.Services;
using System.Globalization;

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
                        var inactiveUsers = await Process(syncService, reminderService, paymentsRepository, enrolmentService);
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

        public async Task<List<LockTransaction>?> Process(IDeviceProcessor deviceProcessor,
            IApplicationReportService applicationReportService,
            IPaymentsRepository paymentsRepository,
            IEnrolmentService enrolmentService)
        {
            var records = await applicationReportService.GetStatusReportByDealer(null, null, 1, 1000, ""); ;

            // Get all restructured records to remove from the auto restructure list
            if (records == null && records?.StatusReports?.Any() == false)
            { return null; }

            //Only take records where the last payment is in the last 24hrs
            var autoLockRecords = records?.StatusReports;

            var currentYear = DateTime.UtcNow.Year;

            //Get all fully paid
            var fullyPaidRecords = autoLockRecords?
            .Where(x => 
                x.Arrears >= 0 && 
                x.LoanBalance <= 0 &&
                DateTimeFormat(x.NextLockDate).HasValue &&
                DateTimeFormat(x.NextLockDate).Value.Year == currentYear)
            .ToList();

            // NOTE: this loop has never branched on LockGroup -- every fully-paid
            // account, Knox (LockGroup==2) included, falls into devicesToLock
            // and is only ever sent to deviceProcessor.ProcessBatchesAsync
            // (Nuovo-only), so Knox devices are never actually unlocked here.
            // Not fixing that pre-existing gap as part of this task -- but
            // LockGroup==3 (Transsion) devices ARE now correctly routed to
            // PayTrigger's dedicated removeLock endpoint below (via
            // RemoveDevicesPayTrigger), which releases the device from
            // PayTrigger's management entirely rather than reusing the
            // payment-cycle lock-extension call with a far-future date.
            var devicesToLock = new List<LockTransaction>();
            var devicesToRemovePayTrigger = new List<LockTransaction>();

            if (fullyPaidRecords != null && fullyPaidRecords.Any())
            {
                foreach (var account in fullyPaidRecords)
                {

                    var lockDevice = new LockTransaction()
                    {
                        AccountId = account.AccountNo,
                        FirstName = account.FirstName,
                        AutoLockDate = DateTime.MaxValue
                    };

                    if (account.LockGroup == 3)
                    {
                        devicesToRemovePayTrigger.Add(lockDevice);
                    }
                    else
                    {
                        devicesToLock.Add(lockDevice);
                    }
                }

                var lockedDevices = await deviceProcessor.ProcessBatchesAsync(devicesToLock, _logger);

                if (devicesToRemovePayTrigger.Any())
                {
                    await enrolmentService.RemoveDevicesPayTrigger(devicesToRemovePayTrigger);
                }

                return lockedDevices;
            }

            return devicesToLock;
        }

        private static DateTime? DateTimeFormat(string? firstPaidDate)
        {
            if(string.IsNullOrEmpty(firstPaidDate)) return null;
            // Try both formats (with and without fractional seconds: %OS in R)
            string[] formats =
            {
                 "dd/MM/yyyy HH:mm:ss",
                "dd/MM/yyyy HH:mm:ss.FFFFFFF",
                "yyyy-MM-dd HH:mm:ss",
                "yyyy-MM-dd HH:mm:ss.FFFFFFF",
                "dd/MM/yyyy'T'HH:mm:ss",          // <-- NEW FORMAT
                "dd/MM/yyyy'T'HH:mm:ss.FFFFFFF",   // if fractional seconds appear
                "d/M/yyyy h:mm:ss tt",
                "dd/MM/yyyy h:mm:ss tt",   // also allow 2-digit day
                "d/M/yyyy hh:mm:ss tt",    // padded hour
                "dd/MM/yyyy hh:mm:ss tt"
            };

            DateTime parsedDate = DateTime.ParseExact(
                firstPaidDate,
                formats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None
            );

            // R adds term_in_months * 30 days (fixed)
            return parsedDate;
        }
    }
}
