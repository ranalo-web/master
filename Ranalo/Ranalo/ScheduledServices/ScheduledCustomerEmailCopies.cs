using Ranalo.DataStore;
using Ranalo.Services;

namespace Ranalo.ScheduledServices
{
    // Emails each customer a copy of what we've sent their phone (reminders,
    // receipts, weekly updates...), when they've given an email in the app.
    // From the no-reply address (EmailSettings); does nothing until it's set
    // up. Each account is emailed up to its last message sent, so nothing
    // goes twice; one failed email is retried next run.
    public class ScheduledCustomerEmailCopies : BackgroundService
    {
        private readonly ILogger<ScheduledCustomerEmailCopies> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly TimeSpan _interval = TimeSpan.FromMinutes(10);
        private const int PerRun = 200;

        public ScheduledCustomerEmailCopies(ILogger<ScheduledCustomerEmailCopies> logger, IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var email = scope.ServiceProvider.GetRequiredService<IEmailSender>();
                    if (email.Ready)
                    {
                        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
                        await RunAsync(scope.ServiceProvider.GetRequiredService<ICustomerAccessRepository>(), email,
                            config["CustomerApp:SupportEmail"] ?? "support@ranalocredit.com");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Customer email copies failed.");
                }

                try
                {
                    await Task.Delay(_interval, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    // Shutting down.
                }
            }
        }

        public async Task<int> RunAsync(ICustomerAccessRepository access, IEmailSender email, string supportEmail)
        {
            var sent = 0;
            var failed = new HashSet<long>();
            foreach (var m in await access.GetMessagesToEmailAsync(PerRun))
            {
                // After one failure, that account's later messages wait too,
                // so they still go in order next run.
                if (failed.Contains(m.AccountNo)) continue;
                try
                {
                    var (subject, body) = CustomerEmail.Copy(CustomerAppRules.InboxItem(m.AsLog()), m.AccountNo, supportEmail);
                    await email.SendAsync(m.Email, subject, body);
                    await access.SetEmailedUpToAsync(m.AccountNo, m.DateSent);
                    sent++;
                }
                catch (Exception ex)
                {
                    failed.Add(m.AccountNo);
                    _logger.LogWarning(ex, "Customer email copy to account {Account} failed; retried next run.", m.AccountNo);
                }
            }
            if (sent > 0) _logger.LogInformation("Customer email copies: {Sent} sent", sent);
            return sent;
        }
    }

    public static class CustomerEmail
    {
        public static (string Subject, string Body) Copy(MyInboxItem item, long accountNo, string supportEmail)
        {
            var lines = new List<string> { item.Title, "" };
            lines.AddRange(item.Lines);
            lines.Add("");
            lines.Add($"Account {accountNo} · sent {item.SentAtKenya:ddd d MMM yyyy, h:mm tt} (Kenya time)");
            lines.Add("See all your messages, your statement and pay in the Ranalo app: https://my.ranalocredit.com");
            lines.Add("");
            lines.Add($"This is an automatic message; replies aren't read. For help call {CustomerMessages.HelpLine} or email {supportEmail}.");
            return ($"Ranalo: {item.Title}", string.Join("\n", lines));
        }
    }
}
