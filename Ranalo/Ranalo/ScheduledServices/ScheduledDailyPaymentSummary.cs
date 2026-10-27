using MySqlX.XDevAPI;
using Newtonsoft.Json;
using Ranalo.DataStore;
using Ranalo.Models;
using Ranalo.Services;
using Ranalo.SumsungKnox;
using Ranalo.Woocommece.Api.Services;
using System.Data;
using System.Globalization;
using System.Net.Http.Headers;

namespace Ranalo.ScheduledServices
{
    public class ScheduledDailyPaymentSummary : BackgroundService
    {
        private readonly ILogger<ScheduledDailyPaymentSummary> _logger;
        private readonly IServiceScopeFactory _scopeFactory;

        // A weekly update: Sundays at 6 PM Kenya time (15:00 UTC). It ran
        // daily at 11:15 UTC until 2026-10-27.
        private const DayOfWeek RunDay = DayOfWeek.Sunday;
        private static readonly TimeSpan RunTimeUtc = new TimeSpan(15, 0, 0);

        public static DateTime NextRunUtc(DateTime utcNow)
        {
            var next = utcNow.Date.AddDays(((int)RunDay - (int)utcNow.DayOfWeek + 7) % 7).Add(RunTimeUtc);
            return next <= utcNow ? next.AddDays(7) : next;
        }

        public ScheduledDailyPaymentSummary(
            ILogger<ScheduledDailyPaymentSummary> logger,
            IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Weekly Payment Summary service started at {time} (UTC)", DateTime.UtcNow);

            while (!stoppingToken.IsCancellationRequested)
            {
                DateTime utcNow = DateTime.UtcNow;

                DateTime nextRunUtc = NextRunUtc(utcNow);

                TimeSpan delay = nextRunUtc - utcNow;

                _logger.LogInformation("Next scheduled Weekly Payment Summary run: {next}", nextRunUtc);

                try
                {
                    await Task.Delay(delay, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    _logger.LogInformation("Service cancellation detected, stopping.");
                    break;
                }

                try
                {
                    _logger.LogInformation("Executing Weekly Payment Summary at {time} (UTC)", DateTime.UtcNow);

                    using var scope = _scopeFactory.CreateScope();

                    var syncService = scope.ServiceProvider.GetRequiredService<IPaymentReminderService>();
                    var reminderService = scope.ServiceProvider.GetRequiredService<IApplicationReportService>();
                    var paymentsRepository = scope.ServiceProvider.GetRequiredService<IPaymentsRepository>();
                    var knoxGuardClient = scope.ServiceProvider.GetRequiredService<IKnoxGuardClient>();

                    var inactiveUsers = await Process(syncService, reminderService, paymentsRepository, knoxGuardClient);

                    foreach (var order in inactiveUsers)
                    {
                        _logger.LogInformation("Weekly Payment Summary sent to {user}", order.AccountId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error running Weekly Payment Summary job.");
                }
            }

            _logger.LogInformation("Weekly Payment Summary service stopped at {time} (UTC)", DateTime.UtcNow);
        }
        private async Task<List<AccountSendMessage>?> Process(IPaymentReminderService paymentReminder, 
            IApplicationReportService applicationReportService, 
            IPaymentsRepository paymentsRepository, 
            IKnoxGuardClient knoxGuardClient)
        {
            var records = await applicationReportService.GetStatusReportByDealer(null, null, 1, ReportPaging.All, ""); ;

            // Get all restructured records to remove from the auto restructure list

            if (records == null && records?.StatusReports?.Any() == false)
            { return null; }

            //Only take records where the last payment is in the last 24hrs
            var autoRestructured = records?.StatusReports;

            //Remove all fully paid
            autoRestructured?.RemoveAll(x => x.Arrears > 0 && x.LoanBalance < 0);

            autoRestructured?.RemoveAll(x => x.NotPaying90D == true);

            var accountMessages = new List<AccountSendMessage>();
            var knoxReminders = new List<AccountSendMessage>();
            var transsionReminders = new List<AccountSendMessage>();
            //Not sure why this removes negative arrears
            if (autoRestructured != null && autoRestructured.Any())
            {
                foreach (var account in autoRestructured)
                {
                    DateTime nextLock = DateTimeFormat(account.NextLockDateIsoFormat);
                    var summary = Summary(account, nextLock);

                    var accountMessage = new AccountSendMessage()
                    {
                        AccountId = account.AccountNo,
                        FirstName = account.FirstName,
                        NewDaily = account.NewDaily,
                        AutoLockDatePmtR = nextLock,
                        Imei = account.ImeiNo,
                        MessageTitle = summary.Title,
                        MessageText = summary.Content
                    };

                    if (account.LockGroup == 2)
                    {
                        knoxReminders.Add(accountMessage);
                    }
                    else if (account.LockGroup == 3)
                    {
                        transsionReminders.Add(accountMessage);
                    }
                    else
                    {
                        accountMessages.Add(accountMessage);
                    }
                }

                if (accountMessages.Any())
                {
                    var sentMessages = await paymentReminder.RunPaymentsSummariesAsync(accountMessages, paymentsRepository);
                }
                if (knoxReminders.Any())
                {
                    await paymentReminder.RunKnoxPaymentsSummariesAsync(knoxReminders, paymentsRepository, knoxGuardClient);
                }
                if (transsionReminders.Any())
                {
                    await paymentReminder.RunTranssionPaymentsSummariesAsync(transsionReminders, paymentsRepository);
                }
            }

            return accountMessages;
        }

        // NextLockDateIsoFormat is UTC; the message shows it in Kenya time.
        private static CustomerMessages.Message Summary(MobileStatusReport record, DateTime nextLockUtc)
        {
            var contractEnds = CalculateContractEndDate(record.FirstPaymentDate.ToString(), record.TermInMonths);
            return CustomerMessages.Summary(record, nextLockUtc, contractEnds);
        }

        private static DateTime DateTimeFormat(string firstPaidDate)
        {
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

        private static DateTime CalculateContractEndDate(string firstPaidDate, int termInMonths)
        {
            // Try both formats (with and without fractional seconds: %OS in R)
            string[] formats =
            {
                 "dd/MM/yyyy HH:mm:ss",
                "dd/MM/yyyy HH:mm:ss.FFFFFFF",
                "yyyy-MM-dd HH:mm:ss",
                "yyyy-MM-dd HH:mm:ss.FFFFFFF",
                "dd/MM/yyyy'T'HH:mm:ss",
                "dd/MM/yyyy'T'HH:mm:ss.FFFFFFF",
                "d/M/yyyy h:mm:ss tt",
                "dd/MM/yyyy h:mm:ss tt",
                "d/M/yyyy hh:mm:ss tt",
                "dd/MM/yyyy hh:mm:ss tt",

                // ✅ U.S. formats (your actual input)
                "M/d/yyyy h:mm:ss tt",
                "MM/dd/yyyy h:mm:ss tt",
                "M/d/yyyy hh:mm:ss tt",
                "MM/dd/yyyy hh:mm:ss tt"
            };

            DateTime parsedDate = DateTime.ParseExact(
                firstPaidDate,
                formats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None
            );

            // R adds term_in_months * 30 days (fixed)
            return parsedDate.AddDays(termInMonths * 30);
        }

    }
}
