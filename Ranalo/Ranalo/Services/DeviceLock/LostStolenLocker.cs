using Ranalo.DataStore;
using Ranalo.Models;
using Ranalo.PayTrigger;
using Ranalo.SumsungKnox;
using Ranalo.SumsungKnox.Models;
using PayTriggerModels = Ranalo.PayTrigger.Models;

namespace Ranalo.Services.DeviceLock
{
    // Locks a phone reported lost or stolen, now, with its own provider --
    // the same calls the nightly lock jobs make, with the lock date set to
    // now, but keeping each provider's answer so the admin sees whether it
    // worked.
    public interface ILostStolenLocker
    {
        Task<(bool Sent, string Result)> LockNowAsync(long accountNo, int? lockGroup);
    }

    public class LostStolenLocker : ILostStolenLocker
    {
        private readonly IDeviceProcessor _nuovo;
        private readonly IKnoxGuardClient _knox;
        private readonly IPayTriggerClient _payTrigger;
        private readonly IEnrolmentRepository _enrolments;
        private readonly TimeProvider _time;
        private readonly ILogger<LostStolenLocker> _logger;

        public LostStolenLocker(IDeviceProcessor nuovo, IKnoxGuardClient knox, IPayTriggerClient payTrigger,
            IEnrolmentRepository enrolments, TimeProvider time, ILogger<LostStolenLocker> logger)
        {
            _nuovo = nuovo;
            _knox = knox;
            _payTrigger = payTrigger;
            _enrolments = enrolments;
            _time = time;
            _logger = logger;
        }

        public async Task<(bool Sent, string Result)> LockNowAsync(long accountNo, int? lockGroup)
        {
            var provider = DeviceLockRules.ProviderForLockGroup(lockGroup);
            var now = _time.GetUtcNow();
            try
            {
                if (provider == LockProvider.Nuovo)
                {
                    var tx = await _nuovo.ProcessSingleAsync(new LockTransaction { AccountId = accountNo, AutoLockDate = now.UtcDateTime }, _logger);
                    return (true, $"Nuovo: {tx.Result ?? "sent"}");
                }

                var enrolment = await _enrolments.GetByAccountIdAsync(accountNo);
                if (string.IsNullOrWhiteSpace(enrolment?.IMEI))
                {
                    return (false, $"{provider}: no IMEI on the enrolment for account {accountNo}.");
                }

                if (provider == LockProvider.Transsion)
                {
                    var response = await _payTrigger.UpdateRepayInfoAsync(new PayTriggerModels.UpdateRepayInfoRequest
                    {
                        Imei = enrolment.IMEI,
                        NextRepayTime = now.ToUnixTimeSeconds(),
                    });
                    return (response.IsSuccess, $"Transsion: {response.Code} {response.Message}");
                }

                var knoxResponse = await _knox.ExecuteDeviceActionsAsync(new DeviceActionsRequest
                {
                    DeviceUid = enrolment.IMEI,
                    ApproveId = enrolment.VeriTechTransId,
                    Actions = new List<DeviceActionItem>
                    {
                        new() { Action = "unLock", Timestamp = 0 },
                        new() { Action = "lock", Timestamp = now.ToUnixTimeMilliseconds(), Message = "This phone has been reported lost or stolen." },
                    },
                });
                var body = await knoxResponse.Content.ReadAsStringAsync();
                return (knoxResponse.IsSuccessStatusCode, $"Knox: {(int)knoxResponse.StatusCode} {(body.Length > 300 ? body[..300] : body)}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Locking lost/stolen account {AccountNo} with {Provider} failed", accountNo, provider);
                return (false, $"{provider}: {ex.Message}");
            }
        }
    }
}
