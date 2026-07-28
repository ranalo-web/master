using Ranalo.DataStore;
using Ranalo.Models;
using Ranalo.Services;
using System.Globalization;

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
                        var enrolmentService = scope.ServiceProvider.GetRequiredService<IEnrolmentService>();
                        var inactiveUsers = await Process(syncService, reminderService, paymentsRepository, enrolmentService);
                        
                        if(inactiveUsers != null && inactiveUsers.Any())
                        {
                            foreach (var order in inactiveUsers)
                            {
                                _logger.LogInformation("lock Auto for: {user}", order.AccountId);
                                // Possibly send email reminders, clean up data, etc.
                            }
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

            //Remove all fully paid
            autoLockRecords?.RemoveAll(x => x.Arrears > 0 && x.LoanBalance < 0);

            // Remove positive arrears
            autoLockRecords?.RemoveAll(x => x.Arrears < 0);

            var qualifying = autoLockRecords?
            //.Where(r => NeedsUpdate(r.NextLockDate))
            .ToList();

            var devicesToLock = new List<LockTransaction>();
            var devicesToLockKnox = new List<LockTransaction>();
            //Not sure why this removes negative arrears
            //records.Records.RemoveAll(a => a.ArrearsR > 0);
            var currentYear = DateTime.UtcNow.Year;

            if (qualifying != null && qualifying.Any())
            {
                foreach (var account in qualifying)
                {
                    var dailyAll = ((account.Daily) + (account.Weekly / 7) + (account.Monthly / 30));
                    var unitsLeft = SafeDivide(account.Arrears, dailyAll);

                    var now = DateTime.Now;
                    var autoLockDatePmt = now.AddSeconds(Convert.ToDouble(unitsLeft * 60 * 60 * 24));

                    var lockDevice = new LockTransaction()
                    {
                        AccountId = account.AccountNo,
                        FirstName = account.FirstName,
                        AutoLockDate = (account.Arrears > 0 
                                        && account.LoanBalance < 0 &&
                        DateTimeFormat(account.NextLockDate).HasValue &&
                        DateTimeFormat(account.NextLockDate).Value.Year == currentYear) ? DateTime.MaxValue : autoLockDatePmt
                    };

                    if(account.LockGroup == 2)
                    {
                        devicesToLockKnox.Add(lockDevice);
                    }
                    else
                    {
                        devicesToLock.Add(lockDevice);
                    }
                }

                var lockedDevices = await deviceProcessor.ProcessBatchesAsync(devicesToLock, _logger);

                if(devicesToLockKnox.Any())
                {
                    await enrolmentService.LockDevicesKnox(devicesToLockKnox);
                }

                return lockedDevices;
            }

            return devicesToLock;
        }

        private static decimal SafeDivide(decimal numerator, decimal denominator)
        {
            return denominator == 0 ? 0 : numerator / denominator;
        }

        private static DateTime? DateTimeFormat(string? firstPaidDate)
        {
            if (string.IsNullOrEmpty(firstPaidDate)) return null;
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

        bool NeedsUpdate(string? kenyanTimestamp)
        {
            if (string.IsNullOrEmpty(kenyanTimestamp)) return true;

            var tz = TimeZoneInfo.FindSystemTimeZoneById("E. Africa Standard Time");

            if (!DateTime.TryParseExact(
                    kenyanTimestamp,
                    "dd/MM/yyyy'T'HH:mm:ss",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out DateTime kenyaTime))
            {
                return false; // invalid date → skip
            }

            DateTime eventUtc = TimeZoneInfo.ConvertTimeToUtc(kenyaTime, tz);
            DateTime nowUtc = DateTime.UtcNow;

            // Condition: older OR within next 2 hours
            return eventUtc < nowUtc || eventUtc <= nowUtc.AddHours(2);
        }
    }
}

