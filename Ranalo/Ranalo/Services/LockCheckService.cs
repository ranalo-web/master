using Ranalo.DataStore;
using Ranalo.Models;
using Ranalo.Services.DeviceLock;

namespace Ranalo.Services
{
    public interface ILockCheckService
    {
        Task<LockCheckViewModel> GetAsync(LockCheckStage stage, string brand, int minDaysWithoutPayment);
        Task<LockCheckResult> SendToRecoveryAsync(IEnumerable<long> accounts, int minDaysWithoutPayment, int userId);
        Task<LockCheckResult> SendNuovoLockAsync(IEnumerable<long> accounts, int minDaysWithoutPayment, int userId);
        Task<LockCheckResult> CloseAndReleaseAsync(IEnumerable<long> accounts, int userId);
    }

    // Lock check (LockCheckRules): sorts Nuovo phones into stages and runs
    // the stage's action on the ones an admin ticks. Each action re-checks
    // the phone's stage first, and every result is logged.
    public class LockCheckService : ILockCheckService
    {
        private readonly ILockCheckRepository _repository;
        private readonly IRecoveryService _recoveries;
        private readonly ILostStolenLocker _locker;
        private readonly IDeviceRemovalService _removals;
        private readonly TimeProvider _time;
        private readonly ILogger<LockCheckService> _logger;

        public LockCheckService(ILockCheckRepository repository, IRecoveryService recoveries, ILostStolenLocker locker,
            IDeviceRemovalService removals, TimeProvider time, ILogger<LockCheckService> logger)
        {
            _repository = repository;
            _recoveries = recoveries;
            _locker = locker;
            _removals = removals;
            _time = time;
            _logger = logger;
        }

        private DateTime Today => _time.GetUtcNow().AddHours(3).Date;   // Kenya's date, as payments are dated

        private async Task<List<LockCheckRow>> EvaluatedRowsAsync(int minDaysWithoutPayment)
        {
            var rows = await _repository.GetNuovoRowsAsync();
            foreach (var r in rows)
            {
                LockCheckRules.Evaluate(r, Today, minDaysWithoutPayment);
            }
            return rows;
        }

        public async Task<LockCheckViewModel> GetAsync(LockCheckStage stage, string brand, int minDaysWithoutPayment)
        {
            minDaysWithoutPayment = Math.Clamp(minDaysWithoutPayment, 1, 365);
            var rows = await EvaluatedRowsAsync(minDaysWithoutPayment);
            var branded = rows.Where(r => LockCheckRules.MatchesBrand(r, brand)).ToList();
            return new LockCheckViewModel
            {
                Stage = stage,
                Brand = brand,
                MinDaysWithoutPayment = minDaysWithoutPayment,
                Counts = branded.Where(r => r.Stage != LockCheckStage.None)
                    .GroupBy(r => r.Stage).ToDictionary(g => g.Key, g => g.Count()),
                Rows = branded.Where(r => r.Stage == stage)
                    .OrderByDescending(r => r.DaysSinceLastPayment).ThenByDescending(r => r.Arrears).ToList(),
            };
        }

        private async Task<List<LockCheckRow>> PickAsync(IEnumerable<long> accounts, int minDaysWithoutPayment)
        {
            var wanted = accounts.ToHashSet();
            return (await EvaluatedRowsAsync(minDaysWithoutPayment)).Where(r => wanted.Contains(r.AccountNo)).ToList();
        }

        public async Task<LockCheckResult> SendToRecoveryAsync(IEnumerable<long> accounts, int minDaysWithoutPayment, int userId)
        {
            var done = 0; var failed = 0; var messages = new List<string>();
            foreach (var r in await PickAsync(accounts, minDaysWithoutPayment))
            {
                if (!LockCheckRules.CanSendToRecovery(r.Stage))
                {
                    failed++; messages.Add($"{r.AccountNo}: {LockCheckRules.Label(r.Stage)}, not sent.");
                    continue;
                }

                var reason = r.Stage == LockCheckStage.LostFromNuovo ? RecoveryReason.LockLost : "Collections";
                var note = r.Stage == LockCheckStage.LostFromNuovo
                    ? $"From Lock check: Nuovo lost the phone (un_registered), {r.Arrears:N0} owed."
                    : $"From Lock check: unlocked on Nuovo, no payment for {r.DaysSinceLastPayment} days, {r.Arrears:N0} in arrears.";
                var (id, error) = await _recoveries.AddAsync(r.AccountNo, reason, note, userId);
                var ok = id.HasValue && error == null;
                await _repository.LogAsync(r.AccountNo, LockCheckAction.ToRecovery, ok, ok ? $"Recovery {id}" : error, userId);
                if (ok) done++; else { failed++; messages.Add($"{r.AccountNo}: {error}"); }
            }
            return new(done, failed, messages);
        }

        public async Task<LockCheckResult> SendNuovoLockAsync(IEnumerable<long> accounts, int minDaysWithoutPayment, int userId)
        {
            var done = 0; var failed = 0; var messages = new List<string>();
            foreach (var r in await PickAsync(accounts, minDaysWithoutPayment))
            {
                if (!LockCheckRules.CanSendNuovoLock(r.Stage))
                {
                    failed++; messages.Add($"{r.AccountNo}: {LockCheckRules.Label(r.Stage)}, not sent.");
                    continue;
                }

                var (sent, result) = await _locker.LockNowAsync(r.AccountNo, r.LockGroup);
                await _repository.LogAsync(r.AccountNo, LockCheckAction.NuovoLock, sent, result, userId);
                if (sent) done++; else { failed++; messages.Add($"{r.AccountNo}: {result}"); }
            }
            _logger.LogInformation("Lock check: Nuovo lock sent to {Done} phone(s) by user {UserId}, {Failed} failed", done, userId, failed);
            return new(done, failed, messages);
        }

        // Ends the paid-off contract (dated when it was paid off) and queues the phone for release
        // in Pending Tasks (admin-approved, as for every fully paid phone),
        // unless Nuovo has already released it (un_registered).
        public async Task<LockCheckResult> CloseAndReleaseAsync(IEnumerable<long> accounts, int userId)
        {
            var done = 0; var failed = 0; var messages = new List<string>();
            foreach (var r in await PickAsync(accounts, LockCheckRules.DefaultMinDaysWithoutPayment))
            {
                if (!LockCheckRules.CanCloseAndRelease(r.Stage))
                {
                    failed++; messages.Add($"{r.AccountNo}: {LockCheckRules.Label(r.Stage)}, not closed.");
                    continue;
                }

                try
                {
                    var ended = await _repository.EndContractAsync(r.ContractId, r.AccountNo);
                    string result;
                    if (string.Equals(r.NuovoStatus, "un_registered", StringComparison.OrdinalIgnoreCase))
                    {
                        result = $"Contract {r.ContractId} closed; Nuovo had already released the phone.";
                    }
                    else
                    {
                        var queued = await _removals.QueueAsync(new[]
                        {
                            new DeviceRemovalTask { AccountId = r.AccountNo, Imei = r.Imei, CustomerName = r.CustomerName, LockGroup = r.LockGroup, Reason = DeviceRemovalService.FullyPaidReason },
                        });
                        result = $"Contract {r.ContractId} closed; " + (queued > 0 ? "release waiting in Pending Tasks." : "release was already queued.");
                    }

                    await _repository.LogAsync(r.AccountNo, LockCheckAction.CloseRelease, ended, ended ? result : "Contract was no longer open.", userId);
                    if (ended) done++; else { failed++; messages.Add($"{r.AccountNo}: contract was no longer open."); }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lock check: closing account {Account} failed", r.AccountNo);
                    await _repository.LogAsync(r.AccountNo, LockCheckAction.CloseRelease, false, ex.Message, userId);
                    failed++; messages.Add($"{r.AccountNo}: {ex.Message}");
                }
            }
            return new(done, failed, messages);
        }
    }
}
