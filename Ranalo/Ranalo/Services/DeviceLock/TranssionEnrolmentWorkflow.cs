using Ranalo.DataStore;
using Ranalo.DataStore.DataModels;
using Ranalo.Models;
using Ranalo.PayTrigger;
using Ranalo.Services.Helpers;
using Ranalo.Woocommece.Api.DataStore;
using PayTriggerModels = Ranalo.PayTrigger.Models;

namespace Ranalo.Services.DeviceLock
{
    // Transsion (itel/TECNO/Infinix) enrolment, see DeviceLockRules:
    //
    //   upload checks pass
    //     -> pre-enrol with PayTrigger, locked as soon as it's switched on
    //     -> Devices row created with Status 'pending_activation' (the lock
    //        jobs only touch Status 'enrolled', so they leave it alone)
    //     -> enrolment PendingActivation
    //   customer switches on + connects  (webhook)   -> ActivatedAt, Enrolled
    //   approver/admin calls the customer (approve)   -> CallApprovedAt
    //   both done, in either order
    //     -> PayTrigger updateRepayInfo unlocks it to the first lock date
    //     -> Devices row Status 'enrolled', enrolment Approved
    //
    // From then on ScheduledLockPaying (every 10 minutes) keeps the lock date
    // in line with payments, as for every other device.
    public interface ITranssionEnrolmentWorkflow
    {
        Task<Enrolment> PreEnrolAsync(Enrolment enrolment, int? deviceGroupId);
        Task<WorkflowResult> ApproveCallAsync(Guid enrolmentId, User actor);
        Task<WorkflowResult> RetryUnlockAsync(Guid enrolmentId, User actor);
        Task OnActivatedAsync(string imei);
    }

    public record WorkflowResult(bool Success, string Message);

    public class TranssionEnrolmentWorkflow : ITranssionEnrolmentWorkflow
    {
        public const string PendingActivationDeviceStatus = "pending_activation";
        public const string EnrolledDeviceStatus = "enrolled";

        // First lock date after unlocking. Short on purpose: the paying-lock
        // job recalculates it from the customer's payments within minutes.
        public static readonly TimeSpan FirstUnlockPeriod = TimeSpan.FromDays(1);

        private readonly IEnrolmentRepository _enrolments;
        private readonly IKosePaymentsRepository _devices;
        private readonly IPayTriggerClient _payTrigger;
        private readonly IDeviceLockRepository _deviceLock;
        private readonly TimeProvider _time;
        private readonly ILogger<TranssionEnrolmentWorkflow> _logger;

        public TranssionEnrolmentWorkflow(
            IEnrolmentRepository enrolments,
            IKosePaymentsRepository devices,
            IPayTriggerClient payTrigger,
            IDeviceLockRepository deviceLock,
            TimeProvider time,
            ILogger<TranssionEnrolmentWorkflow> logger)
        {
            _enrolments = enrolments;
            _devices = devices;
            _payTrigger = payTrigger;
            _deviceLock = deviceLock;
            _time = time;
            _logger = logger;
        }

        private DateTime UtcNow => _time.GetUtcNow().UtcDateTime;

        public async Task<Enrolment> PreEnrolAsync(Enrolment enrolment, int? deviceGroupId)
        {
            enrolment.UpdatedBy = "PAYTRIGGER";

            // The Devices table holds one device per account (Id = account).
            var existing = await _devices.GetDeviceByAccountId(enrolment.AccountId);
            if (existing != null && existing.ImeiNo != enrolment.IMEI && existing.Status != "removed" && existing.EnrollmentStatus != "Removed")
            {
                return await FailAsync(enrolment,
                    $"Account {enrolment.AccountId} already has device {existing.ImeiNo} ({existing.Status}). Remove it before enrolling another.");
            }

            PayTriggerModels.PreEnrollImeiResponse response;
            try
            {
                // preLockFlag=true: lock the moment it's activated, until the
                // approval call and activation have both happened.
                response = await _payTrigger.PreEnrollImeiAsync(
                    new List<PayTriggerModels.ImeiEnrollItem>
                    {
                        new() { Imei = enrolment.IMEI, OrderNum = enrolment.OrderId.ToString() }
                    },
                    preLockFlag: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PayTrigger pre-enrol failed for IMEI {Imei}", enrolment.IMEI);
                return await FailAsync(enrolment, $"Could not reach Transsion: {ex.Message}");
            }

            // PayTrigger lists the IMEIs it couldn't take in Data.
            var failed = response.Data?.FirstOrDefault(d => d.Imei == enrolment.IMEI);
            if (!response.IsSuccess || failed != null)
            {
                enrolment.PayTriggerStatus = response.Message;
                return await FailAsync(enrolment,
                    $"Transsion refused the IMEI: {failed?.Message ?? response.Message} (code {failed?.ErrCode ?? response.Code})");
            }

            var device = existing ?? new Ranalo.Woocommece.Api.Models.Device { Id = (int)enrolment.AccountId };
            device.Name = enrolment.FirstName;
            device.ImeiNo = enrolment.IMEI;
            device.IsTv = false;
            device.Model = enrolment.ProductName ?? "";
            device.Make = enrolment.DeviceBrand ?? "";
            device.SdkVersion ??= "";
            device.Status = PendingActivationDeviceStatus;
            device.Locked = true;
            device.LockType = "complete";
            device.AdminLockType = "admin_complete";
            device.DeviceGroupId = deviceGroupId;
            device.CreatedAt = UtcNow.ToString("dd-MM-yy HH:mm:ss 'UTC'");
            device.IsActivated = false;
            device.EnrollmentStatus = "Pending";
            device.LockGroup = 3; // PayTrigger/Transsion

            if (existing == null)
            {
                await _devices.SaveDeviceToDatabaseAsync(device);
            }
            else
            {
                await _devices.UpdateDeviceToDatabaseAsync(device);
            }

            enrolment.Status = DeviceLockRules.WaitingStatus(enrolment);
            enrolment.Updated = UtcNow;
            enrolment.PayTriggerStatus = response.Message;
            enrolment.PayTriggerResponse = "Pre-enrolled. Waiting for the customer to switch the phone on and connect to the internet.";
            await _enrolments.UpdateEnrolmentAsync(enrolment);

            return enrolment;
        }

        public async Task<WorkflowResult> ApproveCallAsync(Guid enrolmentId, User actor)
        {
            var enrolment = await _enrolments.GetByEnrolmentIdAsync(enrolmentId);
            if (enrolment == null)
            {
                return new WorkflowResult(false, "Enrolment not found.");
            }

            if (!DeviceLockRules.IsTranssion(enrolment.DeviceBrand))
            {
                return new WorkflowResult(false, "This isn't a Transsion enrolment.");
            }

            var decision = DeviceLockRules.CanApproveEnrolment(actor.RoleId, actor.UserId, enrolment);
            if (!decision.Allowed)
            {
                return new WorkflowResult(false, decision.Reason!);
            }

            enrolment.CallApprovedByUserId = actor.UserId;
            enrolment.CallApprovedAt = UtcNow;
            enrolment.Status = DeviceLockRules.WaitingStatus(enrolment);
            enrolment.Updated = UtcNow;
            enrolment.UpdatedBy = actor.Name ?? actor.Email;
            await _enrolments.UpdateEnrolmentAsync(enrolment);

            if (!enrolment.ActivatedAt.HasValue)
            {
                return new WorkflowResult(true,
                    "Approved. The phone stays locked until the customer switches it on and connects to the internet, then it unlocks automatically.");
            }

            return await TryUnlockAsync(enrolment);
        }

        public async Task<WorkflowResult> RetryUnlockAsync(Guid enrolmentId, User actor)
        {
            if (actor.RoleId is not (UserRole.Admin or UserRole.Approver))
            {
                return new WorkflowResult(false, "Only an approver or admin can retry an unlock.");
            }

            var enrolment = await _enrolments.GetByEnrolmentIdAsync(enrolmentId);
            if (enrolment == null || !DeviceLockRules.ReadyToUnlock(enrolment))
            {
                return new WorkflowResult(false, "This enrolment isn't waiting to be unlocked.");
            }

            return await TryUnlockAsync(enrolment);
        }

        public async Task OnActivatedAsync(string imei)
        {
            var enrolment = await _enrolments.GetByImeiNumberAsync(imei);
            if (enrolment == null || !DeviceLockRules.IsTranssion(enrolment.DeviceBrand))
            {
                return;
            }

            // Transsion has confirmed this IMEI is theirs: remember its TAC
            // so a later order for another brand on this TAC is caught.
            var tac = DeviceLockRules.Tac(imei);
            if (tac != null)
            {
                var conflict = await _deviceLock.RecordTacAsync(tac, enrolment.DeviceBrand!, "Transsion", imei);
                if (conflict != null)
                {
                    _logger.LogWarning("TAC {Tac} is recorded as {Stored} but Transsion activated {Imei} as {Brand}",
                        tac, conflict, imei, enrolment.DeviceBrand);
                }
            }

            if (enrolment.UnlockedAt.HasValue)
            {
                return;
            }

            if (!enrolment.ActivatedAt.HasValue)
            {
                enrolment.ActivatedAt = UtcNow;
                if (enrolment.Status is EnrolmentStatus.PendingActivation or EnrolmentStatus.Pending)
                {
                    enrolment.Status = DeviceLockRules.WaitingStatus(enrolment);
                }
                enrolment.Updated = UtcNow;
                enrolment.UpdatedBy = "PAYTRIGGER";
                await _enrolments.UpdateEnrolmentAsync(enrolment);
            }

            await TryUnlockAsync(enrolment);
        }

        private async Task<WorkflowResult> TryUnlockAsync(Enrolment enrolment)
        {
            if (!DeviceLockRules.ReadyToUnlock(enrolment))
            {
                return new WorkflowResult(true, "Approved.");
            }

            var firstLock = UtcNow.Add(FirstUnlockPeriod);
            var firstLockSeconds = new DateTimeOffset(firstLock).ToUnixTimeSeconds();

            string failure;
            try
            {
                var response = await _payTrigger.UpdateRepayInfoAsync(new PayTriggerModels.UpdateRepayInfoRequest
                {
                    Imei = enrolment.IMEI,
                    OrderNum = enrolment.OrderId.ToString(),
                    NextRepayTime = firstLockSeconds
                });

                if (response.IsSuccess)
                {
                    await MarkUnlockedAsync(enrolment, firstLockSeconds * 1000);
                    return new WorkflowResult(true, $"Approved and unlocked. Next lock date {firstLock:dd/MM/yyyy HH:mm} UTC, then kept in line with payments.");
                }

                failure = $"{response.Message} (code {response.Code})";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PayTrigger unlock failed for IMEI {Imei}", enrolment.IMEI);
                failure = ex.Message;
            }

            enrolment.PayTriggerResponse = $"Unlock failed: {failure}";
            enrolment.Updated = UtcNow;
            await _enrolments.UpdateEnrolmentAsync(enrolment);
            return new WorkflowResult(false, $"Approved, but Transsion didn't unlock the phone: {failure}. Use Retry unlock.");
        }

        private async Task MarkUnlockedAsync(Enrolment enrolment, long firstLockMillis)
        {
            var device = await _devices.GetDeviceByAccountId(enrolment.AccountId);
            if (device != null)
            {
                device.Status = EnrolledDeviceStatus;
                device.Locked = false;
                device.LockType = "unlocked";
                device.IsActivated = true;
                device.EnrollmentStatus = "Completed";
                device.EnrolledOn = UtcNow.ToString("dd-MM-yy HH:mm:ss 'UTC'");
                device.NextLockDate = TimestampHelper.FormatDateOnly(firstLockMillis);
                device.NextLockDateIsoFormat = TimestampHelper.FormatRelockTimestamp(firstLockMillis);
                await _devices.UpdateDeviceToDatabaseAsync(device);
            }
            else
            {
                _logger.LogError("Unlocked Transsion IMEI {Imei} but no Devices row exists for account {AccountId}",
                    enrolment.IMEI, enrolment.AccountId);
            }

            enrolment.UnlockedAt = UtcNow;
            enrolment.ApprovedDate = UtcNow;
            enrolment.Status = EnrolmentStatus.Approved;
            enrolment.Updated = UtcNow;
            enrolment.PayTriggerResponse = "Unlocked.";
            await _enrolments.UpdateEnrolmentAsync(enrolment);
        }

        private async Task<Enrolment> FailAsync(Enrolment enrolment, string message)
        {
            enrolment.Status = EnrolmentStatus.Error;
            enrolment.Updated = UtcNow;
            enrolment.PayTriggerResponse = message;
            await _enrolments.UpdateEnrolmentAsync(enrolment);
            return enrolment;
        }
    }
}
