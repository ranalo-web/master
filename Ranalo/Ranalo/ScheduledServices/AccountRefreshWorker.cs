using Ranalo.DataStore;
using Ranalo.Services;
using Ranalo.Woocommece.Api.Services;

namespace Ranalo.ScheduledServices
{
    // Works through IAccountRefreshQueue one account at a time: the
    // contract, if the order qualifies now, then the same lock-date update
    // ScheduledLockPaying makes every 10 minutes, for this account only.
    // Before this a customer enrolled before their deposit was matched
    // waited up to 2 hours for ScheduledTaskCreateContractOrders, and a
    // Transsion phone's 1-day first lock date could run out meanwhile.
    // Anything that fails here is left to those scheduled runs.
    public class AccountRefreshWorker : BackgroundService
    {
        private readonly IAccountRefreshQueue _queue;
        private readonly ScheduledLockPaying _lockPaying;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<AccountRefreshWorker> _logger;

        public AccountRefreshWorker(
            IAccountRefreshQueue queue,
            ScheduledLockPaying lockPaying,
            IServiceScopeFactory scopeFactory,
            ILogger<AccountRefreshWorker> logger)
        {
            _queue = queue;
            _lockPaying = lockPaying;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await foreach (var accountNo in _queue.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await RefreshAsync(accountNo);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Couldn't refresh account {AccountNo}; the scheduled runs will pick it up", accountNo);
                }
            }
        }

        private async Task RefreshAsync(long accountNo)
        {
            // Devices.Id is held as an int in the app.
            if (accountNo > int.MaxValue)
            {
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var services = scope.ServiceProvider;

            var contracts = await services.GetRequiredService<ISyncService>().CreateContractsForAccountAsync(accountNo);
            if (contracts.Any())
            {
                _logger.LogInformation("Contract created for account {AccountNo} as soon as it qualified", accountNo);
            }

            // As in ScheduledLockPaying: if this lookup fails nothing is sent,
            // rather than risk unlocking a lost/stolen phone.
            var lostStolen = await services.GetRequiredService<ILostStolenRepository>().GetLockedAccountNosAsync();
            await _lockPaying.Process(
                services.GetRequiredService<IDeviceProcessor>(),
                services.GetRequiredService<IApplicationReportService>(),
                services.GetRequiredService<IPaymentsRepository>(),
                services.GetRequiredService<IEnrolmentService>(),
                lostStolen,
                (int)accountNo);
        }
    }
}
