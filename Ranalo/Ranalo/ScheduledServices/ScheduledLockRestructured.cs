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
                        var reminderService = scope.ServiceProvider.GetRequiredService<IApplicationReportService>();
                        var paymentsRepository = scope.ServiceProvider.GetRequiredService<IPaymentsRepository>();
                        var enrolmentService = scope.ServiceProvider.GetRequiredService<IEnrolmentService>();
                        var inactiveUsers = await Process(deviceProcessor, reminderService, paymentsRepository, enrolmentService);
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

        public async Task<List<LockTransaction>?> Process(IDeviceProcessor deviceProcessor, IApplicationReportService reminderService, IPaymentsRepository paymentsRepository, IEnrolmentService enrolmentService)
        {
            var records = await reminderService.GetAllRestructured("", 1, 1000); ;

            //Remove all fully paid
            records.Records?.RemoveAll(x => x.Arrears > 0 && x.LoanBalance < 0);


            var qualifying = GetQualifyingRecords(records);

            var devicesToLock = new List<LockTransaction>();
            var devicesToLockKnox = new List<LockTransaction>();

            if (qualifying == null || qualifying.Any() == false)
            {
                return null;
            }

           

            foreach (var account in qualifying)
            {
                var nextLock = account.AutoLockDatePmtR;

                if (account.ArrearsR < 0 && account.PaidLast24Hours > 0)
                {
                    nextLock = (account.LastPaymentDate ?? DateTime.UtcNow).AddHours(30);
                }

                var lockDevice = new LockTransaction()
                {
                    AccountId = account.AccountNo,
                    FirstName = account.FirstName,
                    AutoLockDate = nextLock
                };

                if (account.LockGroup == 2)
                {
                    devicesToLockKnox.Add(lockDevice);
                }
                if (account.LockGroup == 1)
                {
                    devicesToLock.Add(lockDevice);
                }
            }

            var lockedDevices = await deviceProcessor.ProcessBatchesAsync(devicesToLock);

            if (devicesToLockKnox.Any())
            {
                await enrolmentService.LockDevicesKnox(devicesToLockKnox);
            }

            return lockedDevices;
        }

        public static List<RestructuredRecord> GetQualifyingRecords(RestructuredViewModel? records)
        {
            // Robust null checks
            if (records == null || records.Records == null || !records.Records.Any())
                return null;

            // Remove records where arrears are negative (optional — if still needed)
            var qualifying = records.Records
                .Where(r =>
                    // Condition 1: Not in arrears
                    r.ArrearsR >= 0 ||

                    // Condition 2: In arrears but made a payment in last 24 hours
                    (r.ArrearsR < 0 && r.PaidLast24Hours > 0)
                ).ToList();

            return qualifying;
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
