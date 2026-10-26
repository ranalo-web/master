using Ranalo.DataStore;
using Ranalo.DataStore.DataModels;
using Ranalo.Models;
using Ranalo.PayTrigger;
using Ranalo.SumsungKnox;
using Ranalo.SumsungKnox.Models;
using Ranalo.VeriTechClient;
using Ranalo.Woocommece.Api.DataStore;
using PayTriggerModels = Ranalo.PayTrigger.Models;

namespace Ranalo.Services.DeviceLock
{
    // Stock phones: lock by IMEI with no order, check the lock, and unlock a
    // sold Knox stock phone. Rules in StockPhoneRules.
    public interface IStockPhoneService
    {
        Task<(List<string> Messages, List<string> Errors)> LockAsync(string? imeis, string? brand, int dealerId, User actor);
        Task<WorkflowResult> KnoxApproveAndLockAsync(Guid enrolmentId, User actor);
        Task<WorkflowResult> CheckLockAsync(Guid enrolmentId, User actor);
        Task<WorkflowResult> UnlockSoldKnoxAsync(Enrolment enrolment, User actor);
    }

    public class StockPhoneService : IStockPhoneService
    {
        public const string StockLockMessage = "This phone is locked until it is sold.";

        private readonly IEnrolmentRepository _enrolments;
        private readonly IKosePaymentsRepository _devices;
        private readonly IDeviceLockRepository _deviceLock;
        private readonly IVeritechApiClient _veritech;
        private readonly IKnoxGuardClient _knox;
        private readonly IPayTriggerClient _payTrigger;
        private readonly TimeProvider _time;
        private readonly ILogger<StockPhoneService> _logger;

        public StockPhoneService(IEnrolmentRepository enrolments, IKosePaymentsRepository devices, IDeviceLockRepository deviceLock,
            IVeritechApiClient veritech, IKnoxGuardClient knox, IPayTriggerClient payTrigger, TimeProvider time, ILogger<StockPhoneService> logger)
        {
            _enrolments = enrolments;
            _devices = devices;
            _deviceLock = deviceLock;
            _veritech = veritech;
            _knox = knox;
            _payTrigger = payTrigger;
            _time = time;
            _logger = logger;
        }

        private DateTime UtcNow => _time.GetUtcNow().UtcDateTime;

        public async Task<(List<string> Messages, List<string> Errors)> LockAsync(string? imeis, string? brand, int dealerId, User actor)
        {
            var messages = new List<string>();
            var errors = new List<string>();
            if (!StockPhoneRules.CanManage(actor))
            {
                errors.Add("Only an admin, approver or dealer can lock stock phones.");
                return (messages, errors);
            }
            dealerId = StockPhoneRules.DealerFor(actor, dealerId);

            var (list, parseErrors) = StockPhoneRules.ParseImeis(imeis);
            errors.AddRange(parseErrors);
            brand = DeviceLockRules.NormaliseBrand(brand);

            foreach (var imei in list)
            {
                var tac = DeviceLockRules.Tac(imei);
                var problem = StockPhoneRules.CheckBrand(brand, tac == null ? null : await _deviceLock.GetTacBrandAsync(tac));
                if (problem == null)
                {
                    var live = await _devices.GetDeviceByImeiAsync(imei);
                    var liveAccount = live != null && live.Status != "removed" && live.EnrollmentStatus != "Removed" ? (long?)live.Id : null;
                    problem = StockPhoneRules.CheckFree(await _enrolments.GetByImeiNumberAsync(imei), liveAccount);
                }

                if (problem != null)
                {
                    errors.Add($"{imei}: {problem}");
                    continue;
                }

                var enrolment = new Enrolment
                {
                    Id = Guid.NewGuid(),
                    IMEI = imei,
                    DeviceBrand = brand,
                    DealerId = dealerId,
                    Status = EnrolmentStatus.Stock,
                    Created = UtcNow,
                    Updated = UtcNow,
                    UpdatedBy = actor.Name,
                    StockLockedAt = UtcNow,
                    StockLockedByUserId = actor.UserId,
                };

                var sent = DeviceLockRules.IsTranssion(brand)
                    ? await SendToTranssionAsync(enrolment)
                    : await SendToVeritechAsync(enrolment);

                if (sent == null)
                {
                    await _enrolments.CreateEnrolmentAsync(enrolment);
                    messages.Add(DeviceLockRules.IsTranssion(brand)
                        ? $"{imei}: sent to Transsion. It locks the first time it's switched on and connected."
                        : $"{imei}: uploaded to Knox. Approve & lock it once it reaches Knox (usually a few minutes).");
                }
                else
                {
                    errors.Add($"{imei}: {sent}");
                }
            }

            return (messages, errors);
        }

        // null = sent.
        private async Task<string?> SendToTranssionAsync(Enrolment enrolment)
        {
            try
            {
                // preLockFlag: locked from the moment it activates. Only the
                // IMEI -- there's no order yet.
                var response = await _payTrigger.PreEnrollImeiAsync(
                    new List<PayTriggerModels.ImeiEnrollItem> { new() { Imei = enrolment.IMEI } }, preLockFlag: true);

                var failed = response.Data?.FirstOrDefault(d => d.Imei == enrolment.IMEI);
                if (!response.IsSuccess || failed != null)
                {
                    return $"Transsion refused it: {failed?.Message ?? response.Message} (code {failed?.ErrCode ?? response.Code})";
                }

                enrolment.UpdatedBy = "PAYTRIGGER";
                enrolment.PayTriggerStatus = response.Message;
                enrolment.PayTriggerResponse = "Stock: pre-enrolled, locks when switched on.";
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PayTrigger stock pre-enrol failed for IMEI {Imei}", enrolment.IMEI);
                return $"Could not reach Transsion: {ex.Message}";
            }
        }

        private async Task<string?> SendToVeritechAsync(Enrolment enrolment)
        {
            try
            {
                var enroll = await _veritech.UploadDevicesAsync(new List<string> { enrolment.IMEI });
                enrolment.UpdatedBy = "VERITECH";
                enrolment.VeriTechCode = enroll.Data.Code;
                enrolment.VeriTechData = enroll.Data.Data;
                enrolment.VeriTechTransId = enroll.Data.Transaction_Id;
                enrolment.VeriTechStatus = enroll.Data.Status;
                enrolment.VeriTechMessage = enroll.Data.Message;
                return string.IsNullOrEmpty(enrolment.VeriTechTransId)
                    ? $"Veritech didn't take it: {enroll.Data.Message ?? enroll.Data.Status ?? "no answer"}"
                    : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Veritech stock upload failed for IMEI {Imei}", enrolment.IMEI);
                return $"Could not reach Veritech: {ex.Message}";
            }
        }

        // Knox: approve the phone and lock it now. Safe to press again: once
        // approved it only resends the lock.
        public async Task<WorkflowResult> KnoxApproveAndLockAsync(Guid enrolmentId, User actor)
        {
            var e = await _enrolments.GetByEnrolmentIdAsync(enrolmentId);
            if (!StockPhoneRules.CanManage(actor) || e == null || !StockPhoneRules.CanSee(actor, e) || !StockPhoneRules.IsStock(e)
                || DeviceLockRules.ProviderForBrand(e.DeviceBrand) != LockProvider.Knox)
            {
                return new WorkflowResult(false, "You can only approve and lock your own Knox stock phones.");
            }

            if (!e.StockKnoxApprovedAt.HasValue)
            {
                var (approved, reply) = await KnoxCallAsync(() => _knox.ApproveDeviceAsync(new ApproveDeviceRequest
                {
                    DeviceUid = e.IMEI,
                    ApproveId = e.VeriTechTransId,
                    ApproveComment = "Stock phone (no order yet)",
                }));
                if (!approved)
                {
                    e.KnoxResponse = reply;
                    e.Updated = UtcNow;
                    await _enrolments.UpdateEnrolmentAsync(e);
                    return new WorkflowResult(false, $"Knox hasn't accepted {e.IMEI} yet: {reply}. It usually reaches Knox a few minutes after uploading; try again shortly.");
                }

                e.StockKnoxApprovedAt = UtcNow;
            }

            var (locked, lockReply) = await SendKnoxLockAsync(e, UtcNow, StockLockMessage);
            e.KnoxResponse = lockReply;
            e.UpdatedBy = "KNOX";
            e.Updated = UtcNow;
            await _enrolments.UpdateEnrolmentAsync(e);

            return locked
                ? new WorkflowResult(true, $"{e.IMEI} approved in Knox and locked. Switch it on and connect it to the internet, then Check lock.")
                : new WorkflowResult(false, $"{e.IMEI} is approved in Knox, but the lock was refused: {lockReply}. Try Approve & lock again.");
        }

        public async Task<WorkflowResult> CheckLockAsync(Guid enrolmentId, User actor)
        {
            var e = await _enrolments.GetByEnrolmentIdAsync(enrolmentId);
            if (!StockPhoneRules.CanManage(actor) || e == null || !StockPhoneRules.CanSee(actor, e) || !StockPhoneRules.IsStock(e))
            {
                return new WorkflowResult(false, "You can only check your own stock phones.");
            }

            StockPhoneRules.StockCheckResult result;
            try
            {
                if (DeviceLockRules.IsTranssion(e.DeviceBrand))
                {
                    result = StockPhoneRules.TranssionCheck(
                        await _payTrigger.FindLockStateAsync(new PayTriggerModels.FindLockStateRequest { Imei = e.IMEI }));
                }
                else
                {
                    var list = await _knox.ListDevicesAsync(new ListDevicesRequest
                    {
                        PageNum = 0, PageSize = 20, SortBy = "updateTime", SortOrder = "descending", Search = e.IMEI,
                    });
                    result = StockPhoneRules.KnoxCheck(list?.DeviceList?.FirstOrDefault(d => d.Imei == e.IMEI), UtcNow);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Stock lock check failed for IMEI {Imei}", e.IMEI);
                result = new StockPhoneRules.StockCheckResult(false, $"Couldn't reach the locker: {ex.Message}");
            }

            // A later check that finds it unlocked takes the tick away.
            e.StockVerifiedAt = result.Locked ? UtcNow : null;
            e.StockCheck = result.Text.Length > 500 ? result.Text[..500] : result.Text;
            e.Updated = UtcNow;
            await _enrolments.UpdateEnrolmentAsync(e);
            return new WorkflowResult(result.Locked, $"{e.IMEI}: {result.Text}");
        }

        // A sold Knox stock phone is already approved in Knox and locked:
        // approving the sale moves its lock date out a day (the paying-lock
        // job then keeps it in line with payments), like an unlock.
        public async Task<WorkflowResult> UnlockSoldKnoxAsync(Enrolment e, User actor)
        {
            var decision = DeviceLockRules.CanApproveEnrolment(actor.RoleId, actor.UserId, e);
            if (!decision.Allowed)
            {
                return new WorkflowResult(false, decision.Reason!);
            }

            var firstLock = UtcNow.Add(TranssionEnrolmentWorkflow.FirstUnlockPeriod);
            var (ok, reply) = await SendKnoxLockAsync(e, firstLock, CustomerMessages.LockScreenText(e.AccountId));
            e.KnoxResponse = reply;
            e.UpdatedBy = "KNOX";
            e.Updated = UtcNow;
            if (ok)
            {
                e.Status = EnrolmentStatus.Approved;
                e.ApprovedDate = UtcNow;
                e.CallApprovedAt = UtcNow;
                e.CallApprovedByUserId = actor.UserId;
            }
            await _enrolments.UpdateEnrolmentAsync(e);

            return ok
                ? new WorkflowResult(true, $"Phone {e.IMEI} approved and unlocked. Next lock date {firstLock:dd/MM/yyyy HH:mm} UTC, then kept in line with payments.")
                : new WorkflowResult(false, $"Knox didn't unlock phone {e.IMEI}: {reply}. Try Approve again.");
        }

        // Knox has no plain lock for us: an unLock+lock pair, lock at `at`.
        private Task<(bool Ok, string Reply)> SendKnoxLockAsync(Enrolment e, DateTime at, string message) =>
            KnoxCallAsync(() => _knox.ExecuteDeviceActionsAsync(new DeviceActionsRequest
            {
                DeviceUid = e.IMEI,
                ApproveId = e.VeriTechTransId,
                Actions = new List<DeviceActionItem>
                {
                    new() { Action = "unLock", Timestamp = 0 },
                    new() { Action = "lock", Timestamp = new DateTimeOffset(at, TimeSpan.Zero).ToUnixTimeMilliseconds(), Message = message },
                },
            }));

        private async Task<(bool Ok, string Reply)> KnoxCallAsync(Func<Task<HttpResponseMessage>> call)
        {
            try
            {
                var response = await call();
                var body = await response.Content.ReadAsStringAsync();
                body = body.Length > 300 ? body[..300] : body;
                return response.IsSuccessStatusCode ? (true, body) : (false, $"HTTP {(int)response.StatusCode} {body}");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }
    }
}
