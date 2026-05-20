using Ranalo.DataStore;
using Ranalo.Models;
using Ranalo.Services;
using Ranalo.SumsungKnox;

namespace Ranalo.ScheduledServices
{
    public class ScheduledRestructuredReminderMessages : BackgroundService
    {
        private readonly ILogger<ScheduledRestructuredReminderMessages> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly TimeSpan _interval = TimeSpan.FromMinutes(120); // Run every 30 min

        public ScheduledRestructuredReminderMessages(
            ILogger<ScheduledRestructuredReminderMessages> logger,
            IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Restructured lock Reminder started at: {time}", DateTime.UtcNow);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Example: perform a database operation
                    _logger.LogInformation("Restructured lock Reminder scheduled task at: {time}", DateTime.UtcNow);

                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var syncService = scope.ServiceProvider.GetRequiredService<IPaymentReminderService>();
                        //IPaymentsRepository
                        var reminderService = scope.ServiceProvider.GetRequiredService<IApplicationReportService>();
                        var paymentsRepository = scope.ServiceProvider.GetRequiredService<IPaymentsRepository>();
                        var knoxGuardClient = scope.ServiceProvider.GetRequiredService<IKnoxGuardClient>();
                        var inactiveUsers = await Process(syncService, reminderService, paymentsRepository, knoxGuardClient);
                        foreach (var order in inactiveUsers)
                        {
                            _logger.LogInformation("Restructured lock Reminder sent to: {user}", order.AccountId);
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
                    _logger.LogError(ex, "Error while running Restructured lock Reminder scheduled task.");
                }
            }

            _logger.LogInformation("Restructured lock Reminder stopped at: {time}", DateTime.UtcNow);
        }

        public async Task<List<AccountSendMessage>?> Process(IPaymentReminderService syncService, 
            IApplicationReportService reminderService, 
            IPaymentsRepository paymentsRepository,
            IKnoxGuardClient knoxGuardClient)
        {
            var records = await reminderService.GetAllRestructured("", 1, 1000);

            //Remove all fully paid
            records.Records?.RemoveAll(x => x.Arrears > 0 && x.LoanBalance < 0);

            records.Records.RemoveAll(x => x.ArrearsR < 0);

            if (records == null && records?.Records?.Any() == false)
            { return null; }

            var accountMessages = new List<AccountSendMessage>();
            var knoxReminders = new List<AccountSendMessage>();
            //Not sure why this removes negative arrears
            //records.Records.RemoveAll(a => a.ArrearsR > 0);

            foreach (var account in records.Records)
            {
                var accountMessage = new AccountSendMessage()
                {
                    AccountId = account.AccountNo,
                    FirstName = account.FirstName,
                    NewDaily = account.NewDaily,
                    AutoLockDatePmtR = account.AutoLockDatePmtR,
                    Imei = account.ImeiNo
                };

                if (account.LockGroup == 2)
                {
                    knoxReminders.Add(accountMessage);
                }
                else
                {
                    accountMessages.Add(accountMessage);
                }
            }
            if (accountMessages.Any())
            {
                var sentMessages = await syncService.RunRemindersAsync(accountMessages, paymentsRepository);
            }

            if (knoxReminders.Any())
            {
                await syncService.RunRemindersKnoxAsync(knoxReminders, paymentsRepository, knoxGuardClient);
            }

            return accountMessages;
        }

        private static decimal SafeDivide(decimal numerator, decimal denominator)
        {
            return denominator == 0 ? 0 : numerator / denominator;
        }
    }
}
