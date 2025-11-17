using Ranalo.DataStore;
using Ranalo.Models;
using Ranalo.Services;
using System.Globalization;

namespace Ranalo.ScheduledServices
{
    public class ScheduledLockRestructured : BackgroundService
    {
        private readonly ILogger<ScheduledLockRestructured> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly TimeSpan _interval = TimeSpan.FromMinutes(10); // Run every 30 min

        public ScheduledLockRestructured(
            ILogger<ScheduledLockRestructured> logger,
            IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Restructured locking started at: {time}", DateTime.UtcNow);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Example: perform a database operation
                    _logger.LogInformation("Restructured locking scheduled task at: {time}", DateTime.UtcNow);

                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var deviceProcessor = scope.ServiceProvider.GetRequiredService<IDeviceProcessor>();
                        //IPaymentsRepository
                        var reminderService = scope.ServiceProvider.GetRequiredService<IApplicationReportService>();
                        var paymentsRepository = scope.ServiceProvider.GetRequiredService<IPaymentsRepository>();
                        var inactiveUsers = await Process(deviceProcessor, reminderService, paymentsRepository);
                        foreach (var device in inactiveUsers)
                        {
                            _logger.LogInformation("Restructured locking locked: {device}", device.AccountId);
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
                    _logger.LogError(ex, "Error while running Restructured locking scheduled task.");
                }
            }

            _logger.LogInformation("Restructured locking task stopped at: {time}", DateTime.UtcNow);
        }

        public async Task<List<LockTransaction>?> Process(IDeviceProcessor deviceProcessor, IApplicationReportService reminderService, IPaymentsRepository paymentsRepository)
        {
            var records = await reminderService.GetAllRestructured("", 1, 1000); ;

            records.Records.RemoveAll(x => x.ArrearsR < 0);

            if (records == null && records?.Records?.Any() == false)
            { return null; }


            var qualifying = records?.Records
            //.Where(r => NeedsUpdate(r.NextLockDate))
            .ToList();

            var devicesToLock = new List<LockTransaction>();
            //Not sure why this removes negative arrears
            //records.Records.RemoveAll(a => a.ArrearsR > 0);

            foreach (var account in records.Records)
            {
                var lockDevice = new LockTransaction()
                {
                    AccountId = account.AccountNo,
                    FirstName = account.FirstName,
                    AutoLockDate = account.AutoLockDatePmtR
                };

                devicesToLock.Add(lockDevice);
            }

            var lockedDevices = await deviceProcessor.ProcessBatchesAsync(devicesToLock);

            return lockedDevices;
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
