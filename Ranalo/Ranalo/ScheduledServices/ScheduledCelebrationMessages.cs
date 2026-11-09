using System.Globalization;
using Ranalo.DataStore;
using Ranalo.Models;
using Ranalo.Services;
using Ranalo.SumsungKnox;

namespace Ranalo.ScheduledServices
{
    // Holiday greetings, birthdays and "halfway" (CelebrationRules), sent in
    // daytime Kenya time. Runs every 30 minutes; whoever was missed (phone
    // off, provider down) is tried again on the next run, up to MaxFailures.
    public class ScheduledCelebrationMessages : BackgroundService
    {
        private readonly TimeSpan _interval = TimeSpan.FromMinutes(30);
        public const int MaxFailures = 3;
        public const int BatchSize = 25;

        private readonly ILogger<ScheduledCelebrationMessages> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _config;

        public ScheduledCelebrationMessages(ILogger<ScheduledCelebrationMessages> logger, IServiceScopeFactory scopeFactory,
            IConfiguration config)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
            _config = config;
        }

        // Celebrations:EidAlFitr = ["2027-03-10", ...]: the gazetted dates.
        public static List<DateTime> EidDates(IConfiguration config) =>
            config.GetSection("Celebrations:EidAlFitr").Get<string[]>()?
                .Select(s => DateTime.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : (DateTime?)null)
                .Where(d => d.HasValue).Select(d => d!.Value).ToList()
            ?? new List<DateTime>();

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var services = scope.ServiceProvider;
                    var sent = await SendAsync(
                        services.GetRequiredService<ICelebrationsRepository>(),
                        services.GetRequiredService<IPaymentReminderService>(),
                        services.GetRequiredService<IPaymentsRepository>(),
                        services.GetRequiredService<IKnoxGuardClient>(),
                        DateTime.UtcNow, EidDates(_config));

                    if (sent.Holiday + sent.Birthdays + sent.Halfway + sent.Upgrades > 0)
                    {
                        _logger.LogInformation("Celebration messages: {Holiday} holiday, {Birthdays} birthday, {Halfway} halfway, {Upgrades} upgrade offer",
                            sent.Holiday, sent.Birthdays, sent.Halfway, sent.Upgrades);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error running celebration messages.");
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

        public static async Task<(int Holiday, int Birthdays, int Halfway, int Upgrades)> SendAsync(
            ICelebrationsRepository repository,
            IPaymentReminderService sender,
            IPaymentsRepository paymentsRepository,
            IKnoxGuardClient knoxGuardClient,
            DateTime nowUtc,
            IEnumerable<DateTime> eidAlFitr)
        {
            var nowKenya = nowUtc + CustomerMessages.KenyaOffset;
            if (!CelebrationRules.SendingHours(nowKenya)) return (0, 0, 0, 0);

            async Task<int> Send(List<AccountSendMessage> records, string type)
            {
                foreach (var batch in records.Chunk(BatchSize))
                {
                    await sender.SendToEachProviderAsync(batch.ToList(), type, paymentsRepository, knoxGuardClient);
                }
                return records.Count;
            }

            var holidaySent = 0;
            var holiday = CelebrationRules.HolidayOn(nowKenya.Date, eidAlFitr);
            if (holiday != null)
            {
                var type = CelebrationRules.HolidayType(holiday);
                var records = (await repository.GetOpenAsync(type, MaxFailures))
                    .Select(a => ToMessage(a, CelebrationRules.For(holiday, a.FirstName))).ToList();
                holidaySent = await Send(records, type);
            }

            var birthdayType = CelebrationRules.BirthdayType(nowKenya.Year);
            var birthdays = (await repository.GetWithDobAsync(birthdayType, MaxFailures))
                .Where(a => CelebrationRules.ParseDob(a.Dob, nowKenya.Date) is DateTime dob && CelebrationRules.IsBirthday(dob, nowKenya.Date))
                .Select(a => ToMessage(a, CelebrationRules.Birthday(a.FirstName))).ToList();
            var birthdaysSent = await Send(birthdays, birthdayType);

            var halfway = (await repository.GetHalfwayAsync(CelebrationRules.HalfwayFrom, CelebrationRules.HalfwayTo, MaxFailures))
                .Select(a => ToMessage(a, CelebrationRules.Halfway(a.FirstName, a.AccountId))).ToList();
            var halfwaySent = await Send(halfway, CelebrationRules.HalfwayType);

            var upgrades = (await repository.GetNearlyPaidAsync(CelebrationRules.UpgradeFrom, MaxFailures))
                .Where(a => CelebrationRules.IsUpgradeCandidate(a.Paid, a.Owed, a.Behind, a.Daily, a.Restructured))
                .Select(a => ToMessage(a, CelebrationRules.Upgrade(a.FirstName, a.AccountId))).ToList();
            var upgradesSent = await Send(upgrades, CelebrationRules.UpgradeType);

            return (holidaySent, birthdaysSent, halfwaySent, upgradesSent);
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
