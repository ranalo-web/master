using Ranalo.DataStore;
using Ranalo.Models;
using Ranalo.Services;
using Ranalo.SumsungKnox;

namespace Ranalo.ScheduledServices
{
    // One-off messages, once per contract (2026-10-27): "Welcome" when it
    // starts and the phone is enrolled, "FullyPaid" once it's paid off. Works
    // from Contract_Info and MessageLogs, so it doesn't matter which job or
    // page created or ended the contract.
    public class ScheduledMilestoneMessages : BackgroundService
    {
        private readonly TimeSpan _interval = TimeSpan.FromMinutes(10);

        // Contracts older than this get no welcome: on first deploy only the
        // last day's customers are greeted, not everyone.
        public static readonly TimeSpan WelcomeWindow = TimeSpan.FromDays(1);
        // The fully-paid job ends contracts dated the paying-off payment,
        // which can be days before the job runs.
        public static readonly TimeSpan FullyPaidWindow = TimeSpan.FromDays(7);
        // A phone that can't be reached is tried this often, then left.
        public const int MaxFailures = 3;

        private readonly ILogger<ScheduledMilestoneMessages> _logger;
        private readonly IServiceScopeFactory _scopeFactory;

        public ScheduledMilestoneMessages(ILogger<ScheduledMilestoneMessages> logger, IServiceScopeFactory scopeFactory)
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
                    var services = scope.ServiceProvider;
                    var (welcomed, paidOff) = await SendAsync(
                        services.GetRequiredService<IMilestoneMessagesRepository>(),
                        services.GetRequiredService<IPaymentReminderService>(),
                        services.GetRequiredService<IPaymentsRepository>(),
                        services.GetRequiredService<IKnoxGuardClient>(),
                        DateTime.UtcNow);

                    if (welcomed + paidOff > 0)
                    {
                        _logger.LogInformation("Milestone messages: {Welcomed} welcome, {PaidOff} fully paid", welcomed, paidOff);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error running milestone messages.");
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

        public static async Task<(int Welcomed, int PaidOff)> SendAsync(
            IMilestoneMessagesRepository repository,
            IPaymentReminderService sender,
            IPaymentsRepository paymentsRepository,
            IKnoxGuardClient knoxGuardClient,
            DateTime nowUtc)
        {
            var welcomes = (await repository.GetDueWelcomesAsync(nowUtc - WelcomeWindow, MaxFailures))
                .Select(a => ToMessage(a, CustomerMessages.Welcome(a.FirstName, a.AccountId, a.Daily)))
                .ToList();
            if (welcomes.Any())
            {
                await sender.SendToEachProviderAsync(welcomes, MilestoneMessagesRepository.WelcomeType, paymentsRepository, knoxGuardClient);
            }

            var paidOff = (await repository.GetDueFullyPaidAsync(nowUtc - FullyPaidWindow, MaxFailures))
                .Select(a => ToMessage(a, CustomerMessages.FullyPaid(a.FirstName, a.AccountId)))
                .ToList();
            if (paidOff.Any())
            {
                await sender.SendToEachProviderAsync(paidOff, MilestoneMessagesRepository.FullyPaidType, paymentsRepository, knoxGuardClient);
            }

            return (welcomes.Count, paidOff.Count);
        }

        private static AccountSendMessage ToMessage(MilestoneAccount account, CustomerMessages.Message message) => new()
        {
            AccountId = account.AccountId,
            FirstName = account.FirstName ?? "",
            Imei = account.Imei,
            LockGroup = account.LockGroup,
            MessageTitle = message.Title,
            MessageText = message.Content
        };
    }
}
