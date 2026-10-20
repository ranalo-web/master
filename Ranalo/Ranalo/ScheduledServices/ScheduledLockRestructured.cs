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
                        // If this lookup fails the run is skipped rather than risk
                        // unlocking a lost/stolen phone.
                        var lostStolen = await scope.ServiceProvider.GetRequiredService<ILostStolenRepository>().GetLockedAccountNosAsync();
                        var inactiveUsers = await Process(deviceProcessor, reminderService, paymentsRepository, enrolmentService, lostStolen);
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

        public async Task<List<LockTransaction>?> Process(IDeviceProcessor deviceProcessor, IApplicationReportService reminderService, IPaymentsRepository paymentsRepository, IEnrolmentService enrolmentService,
            IReadOnlySet<long>? lostStolenLocked = null)
        {
            var records = await reminderService.GetAllRestructured("", 1, ReportPaging.All); ;

            // Lost/stolen phones stay locked whatever the customer pays.
            LostStolenRules.LeaveLocked(records?.Records, r => r.AccountNo, lostStolenLocked);

            //Remove all fully paid
            records.Records?.RemoveAll(x => x.Arrears > 0 && x.LoanBalance < 0);


            var now = DateTime.UtcNow;
            var qualifying = GetQualifyingRecords(records, now);

            var devicesToLock = new List<LockTransaction>();
            var devicesToLockKnox = new List<LockTransaction>();
            var devicesToLockPayTrigger = new List<LockTransaction>();

            if (qualifying == null || qualifying.Any() == false)
            {
                return null;
            }

           

            foreach (var account in qualifying)
            {
                var nextLock = NextLockDate(account, now)!.Value;

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
                if (account.LockGroup == 3)
                {
                    devicesToLockPayTrigger.Add(lockDevice);
                }
            }

            var lockedDevices = await deviceProcessor.ProcessBatchesAsync(devicesToLock, _logger);

            if (devicesToLockKnox.Any())
            {
                await enrolmentService.LockDevicesKnox(devicesToLockKnox);
            }

            if (devicesToLockPayTrigger.Any())
            {
                await enrolmentService.LockDevicesPayTrigger(devicesToLockPayTrigger);
            }

            return lockedDevices;
        }

        public static List<RestructuredRecord> GetQualifyingRecords(RestructuredViewModel? records, DateTime now)
        {
            // Robust null checks
            if (records == null || records.Records == null || !records.Records.Any())
                return null;

            // The rest are left to lock when their current lock date passes.
            return records.Records.Where(r => NextLockDate(r, now).HasValue).ToList();
        }

        // Ahead on the agreed plan: open for the units paid since the first
        // payment on it. Behind: only the auto restructure can open it --
        // paying at least the auto new daily in 24h, before the contract end.
        // (Until 2026-10-19 any payment at all opened it for 30h, so
        // 1 shilling every day or two kept a phone open.)
        public static DateTime? NextLockDate(RestructuredRecord r, DateTime now) =>
            r.ArrearsR > 0
                ? r.AutoLockDatePmtR
                : RestructureCalculator.AutoLockDate(r.AutoNewDaily, r.PaidLast24Hours, r.LastPaymentDate, now);

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
