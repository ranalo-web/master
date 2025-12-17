using Ranalo.DataStore;
using Ranalo.Models;
using Ranalo.Services;
using Ranalo.Woocommece.Api.Services;

namespace Ranalo.ScheduledServices
{
    public class ScheduledActiveLockReminderMessages : BackgroundService
    {
        private readonly ILogger<ScheduledActiveLockReminderMessages> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly TimeSpan _interval = TimeSpan.FromMinutes(120); // Run every 30 min

        public ScheduledActiveLockReminderMessages(
            ILogger<ScheduledActiveLockReminderMessages> logger,
            IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Live lock Reminder started at: {time}", DateTime.UtcNow);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Example: perform a database operation
                    _logger.LogInformation("Live lock Reminder scheduled task at: {time}", DateTime.UtcNow);

                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var syncService = scope.ServiceProvider.GetRequiredService<IPaymentReminderService>();
                        //IPaymentsRepository
                        var reminderService = scope.ServiceProvider.GetRequiredService<IApplicationReportService>();
                        var paymentsRepository = scope.ServiceProvider.GetRequiredService<IPaymentsRepository>();
                        var inactiveUsers = await Process(syncService, reminderService, paymentsRepository);
                        foreach (var order in inactiveUsers)
                        {
                            _logger.LogInformation("lock Reminder sent to: {user}", order.AccountId);
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
                    _logger.LogError(ex, "Error while running Live lock Reminder scheduled task.");
                }
            }

            _logger.LogInformation("Live lock Reminder stopped at: {time}", DateTime.UtcNow);
        }

        public async Task<List<AccountSendMessage>?> Process(IPaymentReminderService syncService, IApplicationReportService applicationReportsService, IPaymentsRepository paymentsRepository)
        {
            var records = await applicationReportsService.GetStatusReportByDealer(null, null, 1, 1000, ""); ;

            if (records == null && records?.StatusReports?.Any() == false)
            { return null; }

            var accountMessages = new List<AccountSendMessage>();
            records.StatusReports.RemoveAll(a => a.Arrears > 0 && a.LoanBalance < 0);

            foreach (var account in records.StatusReports)
            {
                var dailyAll = ((account.Daily) + (account.Weekly / 7) + (account.Monthly / 30));
                var unitsLeft = SafeDivide(account.Arrears, dailyAll);

                var now = DateTime.Now;
                var autoLockDatePmt = now.AddSeconds(Convert.ToDouble(unitsLeft * 60 * 60 * 24));
                //DateTime.Now.AddSeconds(Convert.ToDouble(unitsLeft * 60 * 60 * 24))

                var accountMessage = new AccountSendMessage() 
                { 
                    AccountId = account.AccountNo,
                    FirstName = account.FirstName,
                    NewDaily = account.Daily,
                    AutoLockDatePmtR = autoLockDatePmt
                };

                accountMessages.Add(accountMessage);
            }

            var sentMessages = await syncService.RunRemindersAsync(accountMessages, paymentsRepository);

            return sentMessages;
        }

        private static decimal SafeDivide(decimal numerator, decimal denominator)
        {
            return denominator == 0 ? 0 : numerator / denominator;
        }
    }
}