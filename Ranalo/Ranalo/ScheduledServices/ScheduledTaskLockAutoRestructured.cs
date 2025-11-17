using DocumentFormat.OpenXml.Office2013.Drawing.ChartStyle;
using Ranalo.DataStore;
using Ranalo.Models;
using Ranalo.Services;
using System.Globalization;

namespace Ranalo.ScheduledServices
{
    public class ScheduledTaskLockAutoRestructured : BackgroundService
    {
        private readonly ILogger<ScheduledTaskLockAutoRestructured> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly TimeSpan _interval = TimeSpan.FromMinutes(10); // Run every 30 min

        public ScheduledTaskLockAutoRestructured(
            ILogger<ScheduledTaskLockAutoRestructured> logger,
            IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Live lock Auto restructured: {time}", DateTime.UtcNow);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Example: perform a database operation
                    _logger.LogInformation("Live lock Auto restructured scheduled task at: {time}", DateTime.UtcNow);

                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var syncService = scope.ServiceProvider.GetRequiredService<IDeviceProcessor>();
                        //IPaymentsRepository
                        var reminderService = scope.ServiceProvider.GetRequiredService<IApplicationReportService>();
                        var paymentsRepository = scope.ServiceProvider.GetRequiredService<IPaymentsRepository>();
                        var inactiveUsers = await Process(syncService, reminderService, paymentsRepository);
                        foreach (var order in inactiveUsers)
                        {
                            _logger.LogInformation("lock Auto restructured for: {user}", order.AccountId);
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

            var manualRestructured = await applicationReportService.GetAllRestructuredNoCalculation();
            if (records == null && records?.StatusReports?.Any() == false)
            { return null; }

            //Only take records where the last payment is in the last 24hrs
            var autoRestructured = records?.StatusReports?
            .Where(r => r.LastPaymentDate >= DateTime.UtcNow.AddHours(-24))
            .ToList();

            //Remove all fully paid
            autoRestructured?.RemoveAll(x => x.Arrears > 0 && x.LoanBalance < 0);

            // Remove positive arrears
            autoRestructured?.RemoveAll(x => x.Arrears > 0);

            //Remove all on agreed restructure
            autoRestructured?.RemoveAll(a => manualRestructured?.Any(m => m.AccountNo == a.AccountNo) == true);

            //Now check if they have paid more than the restructured new amount
            var qualifying = autoRestructured?
            .Where(r => r.TotalLast24 >= r.NewDaily )//&& NeedsUpdate(r.NextLockDate)) 
            .ToList();

            var devicesToLock = new List<LockTransaction>();
            //Not sure why this removes negative arrears
            //records.Records.RemoveAll(a => a.ArrearsR > 0);

            if (qualifying != null && qualifying.Any())
            {
                foreach (var account in qualifying)
                {
                    var lockDevice = new LockTransaction()
                    {
                        AccountId = account.AccountNo,
                        FirstName = account.FirstName,
                        AutoLockDate = account.LastPaymentDate != null ? account.LastPaymentDate.Value.AddHours(30) : DateTime.UtcNow
                    };

                    devicesToLock.Add(lockDevice);
                }

                var lockedDevices = await deviceProcessor.ProcessBatchesAsync(devicesToLock);

                return lockedDevices;
            }

            return devicesToLock;
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
