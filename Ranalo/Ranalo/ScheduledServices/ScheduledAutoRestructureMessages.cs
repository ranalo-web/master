using Org.BouncyCastle.Pqc.Crypto.Lms;
using Ranalo.DataStore;
using Ranalo.Models;
using Ranalo.Services;
using System.Globalization;

namespace Ranalo.ScheduledServices
{
    public class ScheduledAutoRestructureMessages : BackgroundService
    {
        private readonly ILogger<ScheduledAutoRestructureMessages> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly TimeSpan _interval = TimeSpan.FromMinutes(120); // Run every 30 min

        public ScheduledAutoRestructureMessages(
            ILogger<ScheduledAutoRestructureMessages> logger,
            IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Auto Restructured lock Reminder started at: {time}", DateTime.UtcNow);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Example: perform a database operation
                    _logger.LogInformation("Auto Restructured lock Reminder scheduled task at: {time}", DateTime.UtcNow);

                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var syncService = scope.ServiceProvider.GetRequiredService<IPaymentReminderService>();
                        //IPaymentsRepository
                        var reminderService = scope.ServiceProvider.GetRequiredService<IApplicationReportService>();
                        var paymentsRepository = scope.ServiceProvider.GetRequiredService<IPaymentsRepository>();
                        var inactiveUsers = await Process(syncService, reminderService, paymentsRepository);
                        foreach (var order in inactiveUsers)
                        {
                            _logger.LogInformation("Auto Restructured lock Reminder sent to: {user}", order.AccountId);
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
                    _logger.LogError(ex, "Error while running Auto Restructured lock Reminder scheduled task.");
                }
            }

            _logger.LogInformation("Auto Restructured lock Reminder stopped at: {time}", DateTime.UtcNow);
        }

        public async Task<List<AccountSendMessage>?> Process(IPaymentReminderService syncService, IApplicationReportService applicationReportService, IPaymentsRepository paymentsRepository)
        {
            var records = await applicationReportService.GetStatusReportByDealer(null, null, 1, 1000, ""); ;

            // Get all restructured records to remove from the auto restructure list
            
            var manualRestructured = await applicationReportService.GetAllRestructuredNoCalculation();
            if (records == null && records?.StatusReports?.Any() == false)
            { return null; }

            //Only take records where the last payment is in the last 24hrs
            // Only take records where the last payment is in the last 24hrs
            var autoRestructured = records?.StatusReports?
            .Where(r => r.LastPaymentDate >= DateTime.UtcNow.AddHours(-24))
            .ToList();

            //Remove all fully paid
            autoRestructured?.RemoveAll(x => x.Arrears > 0 && x.LoanBalance < 0);

            // Remove positive arrears
            autoRestructured?.RemoveAll(x => x.Arrears > 0);

            //Remove all on agreed restructure
            autoRestructured?.RemoveAll(a => manualRestructured?.Any(m => m.AccountNo == a.AccountNo) == true);

            
            var accountMessages = new List<AccountSendMessage>();
            //Not sure why this removes negative arrears
            //records.Records.RemoveAll(a => a.ArrearsR > 0);
            if (autoRestructured != null && autoRestructured.Any())
            {
                

                foreach (var account in autoRestructured)
                {
                    DateTime nextLock = DateTime.ParseExact(
                        account.NextLockDateIsoFormat,
                        "dd/MM/yyyy HH:mm:ss",
                        CultureInfo.InvariantCulture
                    );
                    var accountMessage = new AccountSendMessage()
                    {
                        AccountId = account.AccountNo,
                        FirstName = account.FirstName,
                        NewDaily = account.NewDaily,
                        AutoLockDatePmtR = nextLock
                    };

                    accountMessages.Add(accountMessage);
                }

                var sentMessages = await syncService.RunRemindersAsync(accountMessages, paymentsRepository);

                return sentMessages;
            }

            return accountMessages;
        }
    }
}
