using System.Text.Json;
using Ranalo.DataStore;
using Ranalo.DataStore.DataModels;
using Ranalo.Models;
using Ranalo.PayTrigger;
using Ranalo.PayTrigger.Models;
using Ranalo.Services.DeviceLock;
using Ranalo.SumsungKnox;
using Ranalo.SumsungKnox.Models;
using Ranalo.VeriTechClient;

namespace Ranalo.Services
{
    public interface IRecoveryService
    {
        Task<RecoveryListViewModel> GetListAsync(string status);
        Task<RecoveryDetailViewModel?> GetDetailAsync(int id);
        Task<RecoveryDeviceLookup?> LookupAsync(long accountNo);
        Task<(int? Id, string? Error)> AddAsync(long accountNo, string reason, string? notes, int userId);
        Task<RecoveryActionResult> RunAsync(int id, RecoveryAction action, string? note, int userId);
        Task<int> SweepTranssionAsync();
        Task<int> SweepKnoxApprovalsAsync();
        Task<List<string>> CheckLockersAsync();
        Task OnTranssionActivatedAsync(string imei);
    }

    // Runs one recovery step at a time (RecoveryRules) against Veritech and
    // Knox, or Transsion PayTrigger, the same calls enrolment uses
    // (EnrolmentService, TranssionEnrolmentWorkflow), and logs every reply
    // on the recovery.
    public class RecoveryService : IRecoveryService
    {
        public const string LockMessage = "This phone is locked by Ranalo Credit. Please call us to settle your account.";
        private const int MaxResponseLength = 8000;

        private readonly ILockRecoveryRepository _repository;
        private readonly IEnrolmentRepository _enrolments;
        private readonly IVeritechApiClient _veritech;
        private readonly IKnoxGuardClient _knox;
        private readonly IPayTriggerClient _payTrigger;
        private readonly TimeProvider _time;
        private readonly ILogger<RecoveryService> _logger;

        public RecoveryService(ILockRecoveryRepository repository, IEnrolmentRepository enrolments,
            IVeritechApiClient veritech, IKnoxGuardClient knox, IPayTriggerClient payTrigger,
            TimeProvider time, ILogger<RecoveryService> logger)
        {
            _repository = repository;
            _enrolments = enrolments;
            _veritech = veritech;
            _knox = knox;
            _payTrigger = payTrigger;
            _time = time;
            _logger = logger;
        }

        private DateTime UtcNow => _time.GetUtcNow().UtcDateTime;

        public async Task<RecoveryListViewModel> GetListAsync(string status) => new()
        {
            Status = status,
            Recoveries = await _repository.GetListAsync(status),
        };

        public async Task<RecoveryDetailViewModel?> GetDetailAsync(int id)
        {
            var recovery = await _repository.GetAsync(id);
            if (recovery == null) return null;
            return new RecoveryDetailViewModel
            {
                Recovery = recovery,
                Attempts = await _repository.GetAttemptsAsync(id),
                Actions = RecoveryRules.AllowedActions(recovery),
            };
        }

        public Task<RecoveryDeviceLookup?> LookupAsync(long accountNo) => _repository.LookupDeviceAsync(accountNo);

        public async Task<(int? Id, string? Error)> AddAsync(long accountNo, string reason, string? notes, int userId)
        {
            var device = await _repository.LookupDeviceAsync(accountNo);
            if (device == null || string.IsNullOrWhiteSpace(device.Imei))
                return (null, $"No device with an IMEI on account {accountNo}.");

            var openId = await _repository.GetOpenIdForAccountAsync(accountNo);
            if (openId.HasValue)
                return (openId, $"Account {accountNo} is already in Recovery.");

            var id = await _repository.CreateAsync(new LockRecovery
            {
                AccountNo = accountNo,
                Imei = device.Imei.Trim(),
                Make = device.Make,
                Model = device.Model,
                CustomerName = device.CustomerName,
                DealerName = device.DealerName,
                OriginalLockGroup = device.LockGroup,
                Reason = RecoveryReason.All.Contains(reason) ? reason : "Other",
                Status = RecoveryStatus.Investigating,
                Notes = notes,
                CreatedByUserId = userId,
            });
            await _repository.LogAsync(id, "Added", true,
                $"On {RecoveryRules.LockGroupName(device.LockGroup)}, {device.Make} {device.Model}, IMEI {device.Imei}.", userId);
            return (id, null);
        }

        public async Task<RecoveryActionResult> RunAsync(int id, RecoveryAction action, string? note, int userId)
        {
            var r = await _repository.GetAsync(id);
            if (r == null) return new(false, "Recovery not found.");
            if (!RecoveryRules.AllowedActions(r).Contains(action))
                return new(false, $"{action} isn't available while the phone is {RecoveryStatus.Label(r.Status)}.");

            // One open recovery per account (UQ_LockRecoveries_OpenAccount).
            if (action == RecoveryAction.Reopen)
            {
                var openId = await _repository.GetOpenIdForAccountAsync(r.AccountNo);
                if (openId.HasValue && openId != r.Id)
                    return new(false, $"Account {r.AccountNo} already has an open recovery ({openId}). Carry on there instead.");
            }

            note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
            var transsion = RecoveryRules.IsTranssion(r.Make);
            var statusBefore = r.Status;
            int? transsionState = null;
            try
            {
                var (success, response, transId) = action switch
                {
                    RecoveryAction.TranssionSend => await TranssionSendAsync(r),
                    RecoveryAction.TranssionCheck => await TranssionCheckAsync(r, s => transsionState = s),
                    RecoveryAction.Lock when transsion => await TranssionRepayAsync(r, UtcNow, "Locked by Ranalo Credit recovery"),
                    RecoveryAction.Unlock when transsion => await TranssionRepayAsync(r, UtcNow.Add(TranssionEnrolmentWorkflow.FirstUnlockPeriod), "Unlocked by Ranalo Credit"),
                    RecoveryAction.MoveToTranssion => await MoveToTranssionAsync(r, userId),
                    RecoveryAction.Upload => await UploadAsync(r),
                    RecoveryAction.UploadStatus => await UploadStatusAsync(r),
                    RecoveryAction.Approve => await KnoxCallAsync(() => _knox.ApproveDeviceAsync(new ApproveDeviceRequest
                    {
                        DeviceUid = r.Imei,
                        ApproveId = r.VeritechTransId,
                        ApproveComment = $"Recovery {r.Id} - account {r.AccountNo}",
                    })),
                    RecoveryAction.KnoxCheck => await KnoxCheckAsync(r),
                    // Knox's lock/unlock calls need the LockDevice permission our
                    // Knox account doesn't have (HTTP 403 WRONG_ACCESS, Recovery
                    // 4). Device actions -- an unLock + relock pair, as the
                    // daily lock jobs send (EnrolmentService.LockDevicesKnox);
                    // Knox wants exactly the two ("PAYG Actions must be two sets
                    // of arrays"). Relock now locks; a day ahead unlocks.
                    RecoveryAction.Lock => await KnoxCallAsync(() => _knox.ExecuteDeviceActionsAsync(KnoxRelock(r, UtcNow))),
                    RecoveryAction.Unlock => await KnoxCallAsync(() => _knox.ExecuteDeviceActionsAsync(KnoxRelock(r, UtcNow.Add(TranssionEnrolmentWorkflow.FirstUnlockPeriod)))),
                    RecoveryAction.MoveToKnox => await MoveToKnoxAsync(r, userId),
                    _ => (true, note, (string?)null),
                };

                var reply = response;
                var statusOnly = action is RecoveryAction.NotCatchable or RecoveryAction.Close or RecoveryAction.Reopen or RecoveryAction.Note;
                if (statusOnly && response == null)
                {
                    if (action == RecoveryAction.Note) return new(false, "Write a note first.");
                    response = reply = RecoveryStatus.Label(RecoveryRules.StatusAfter(action, true, r.Status));
                }
                else if (!statusOnly && note != null)
                {
                    // The admin's note goes first, then the provider's reply.
                    response = $"{note}\n{response}";
                }

                var newStatus = action == RecoveryAction.TranssionCheck
                    ? RecoveryRules.StatusAfterTranssionCheck(r.Status, success ? transsionState : null)
                    : RecoveryRules.StatusAfter(action, success, r.Status);
                if (action is not (RecoveryAction.MoveToKnox or RecoveryAction.MoveToTranssion) && (newStatus != r.Status || transId != null))
                {
                    await _repository.UpdateAsync(r.Id, newStatus, transId);
                }

                // Re-approved a phone already on Knox (Knox had lost it): the
                // daily lock jobs read the approve id from its enrolment.
                if (action == RecoveryAction.Approve && success && r.CurrentLockGroup == RecoveryRules.KnoxLockGroup
                    && !string.IsNullOrWhiteSpace(r.VeritechTransId))
                {
                    var enrolment = await _enrolments.GetByAccountIdAsync(r.AccountNo);
                    if (enrolment != null && enrolment.VeriTechTransId != r.VeritechTransId)
                    {
                        response += $"\nEnrolment approve id updated from {enrolment.VeriTechTransId} to {r.VeritechTransId} for the daily lock jobs.";
                        enrolment.VeriTechTransId = r.VeritechTransId;
                        enrolment.Updated = UtcNow;
                        enrolment.UpdatedBy = "RECOVERY";
                        await _enrolments.UpdateEnrolmentAsync(enrolment);
                    }
                }

                await _repository.LogAsync(r.Id, action.ToString(), success, Truncate(response), userId);
                _logger.LogInformation("Recovery {Id} (account {Account}): {Action} by user {UserId}, success {Success}",
                    r.Id, r.AccountNo, action, userId, success);

                // Active in Transsion: it's enrolled, so the daily lock jobs
                // take it over straight away (Move to Transsion).
                if (action == RecoveryAction.TranssionCheck && newStatus == RecoveryStatus.Approved && statusBefore != newStatus)
                {
                    r.Status = newStatus;
                    var (moved, moveReply, _) = await MoveToTranssionAsync(r, userId);
                    await _repository.LogAsync(r.Id, RecoveryAction.MoveToTranssion.ToString(), moved, moveReply, userId);
                    reply = $"{FirstLine(reply)} {(moved ? moveReply : "Not moved: " + moveReply)}";
                }

                return new(success, success
                    ? $"{Describe(action, transsion)}: done. {FirstLine(reply)}"
                    : $"{Describe(action, transsion)} failed. {FirstLine(reply)}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Recovery {Id}: {Action} failed", r.Id, action);
                await _repository.LogAsync(r.Id, action.ToString(), false, Truncate(ex.Message), userId);
                return new(false, $"{Describe(action, transsion)} failed: {ex.Message}");
            }
        }

        // Automatic checks run as no user (the log's By is blank).
        public const int SystemUserId = 0;

        // Recovery phones waiting as "Sent to Transsion" (ScheduledRecovery-
        // TranssionCheck, daily): asks Transsion about each and runs Check
        // in Transsion only for the ones now active -- which moves them to
        // Transsion -- so waiting phones don't fill the log. Returns how
        // many moved.
        public async Task<int> SweepTranssionAsync()
        {
            var moved = 0;
            foreach (var r in await WaitingForTranssionAsync())
            {
                try
                {
                    var state = await _payTrigger.FindLockStateAsync(new FindLockStateRequest { Imei = r.Imei });
                    if (!state.IsSuccess || state.Data?.LockState != RecoveryRules.TranssionActiveState)
                    {
                        continue;
                    }

                    await RunAsync(r.Id, RecoveryAction.TranssionCheck, "Automatic check: Transsion has the phone active.", SystemUserId);
                    if ((await _repository.GetAsync(r.Id))?.Status == RecoveryStatus.MovedToTranssion)
                    {
                        moved++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Recovery {Id}: automatic Transsion check failed", r.Id);
                }
            }
            return moved;
        }

        // Samsung recoveries sent to Knox ("Sent to Knox", with the Veritech
        // transaction id): approve the ones Knox now has (ScheduledRecovery-
        // KnoxApproval, daily). The phone only reaches Knox a while after the
        // upload, so approving straight away fails; this saves pressing
        // Approve on each. Phones not in Knox yet are left alone and unlogged.
        // Returns how many were approved.
        public async Task<int> SweepKnoxApprovalsAsync()
        {
            var approved = 0;
            var waiting = (await _repository.GetListAsync("open"))
                .Where(r => RecoveryRules.IsSamsung(r.Make, r.Model) && r.Status == RecoveryStatus.UploadSent
                            && !string.IsNullOrWhiteSpace(r.VeritechTransId))
                .ToList();
            foreach (var r in waiting)
            {
                try
                {
                    if (await InKnoxAsync(r.Imei) != true)
                    {
                        continue;
                    }

                    var result = await RunAsync(r.Id, RecoveryAction.Approve, "Automatic approval: Knox has the phone.", SystemUserId);
                    if (result.Success)
                    {
                        approved++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Recovery {Id}: automatic Knox approval failed", r.Id);
                }
            }
            return approved;
        }

        // PayTrigger's webhook says this IMEI just activated: check (and so
        // move) its recovery straight away, if one is waiting on it.
        public async Task OnTranssionActivatedAsync(string imei)
        {
            var r = (await WaitingForTranssionAsync()).FirstOrDefault(x => x.Imei == imei);
            if (r != null)
            {
                await RunAsync(r.Id, RecoveryAction.TranssionCheck, "Transsion reported the phone active.", SystemUserId);
            }
        }

        // Daily (ScheduledLockerCheck): every Knox/Transsion phone with an
        // open contract must still be in Knox/Transsion. One that isn't gets
        // a Recovery ("Lock lost") with the locker's reply, unless it already
        // has an open one. Only a definite "not there" counts -- an error
        // reaching Knox or Transsion is logged and the phone left alone.
        // Added after 41109528 dropped out of Knox in June unnoticed.
        public async Task<List<string>> CheckLockersAsync()
        {
            var opened = new List<string>();
            foreach (var d in await _repository.GetLockerDevicesAsync())
            {
                try
                {
                    var missing = d.LockGroup == RecoveryRules.KnoxLockGroup
                        ? await MissingFromKnoxAsync(d)
                        : await MissingFromTranssionAsync(d);
                    if (missing == null || (await _repository.GetOpenIdForAccountAsync(d.AccountNo)).HasValue)
                    {
                        continue;
                    }

                    var (id, error) = await AddAsync(d.AccountNo, RecoveryReason.LockLost, missing, SystemUserId);
                    if (id.HasValue && error == null)
                    {
                        opened.Add($"{d.AccountNo}: {missing}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Locker check failed for account {Account}", d.AccountNo);
                }
            }
            return opened;
        }

        private async Task<string?> MissingFromKnoxAsync(LockerDevice d)
        {
            foreach (var imei in new[] { d.Imei, d.Imei2 }.Where(i => !string.IsNullOrWhiteSpace(i)))
            {
                var list = await _knox.ListDevicesAsync(new ListDevicesRequest
                {
                    PageNum = 0, PageSize = 20, SortBy = "updateTime", SortOrder = "descending", Search = imei,
                });
                if (list == null || !string.Equals(list.Result, "SUCCESS", StringComparison.OrdinalIgnoreCase))
                {
                    return null;   // couldn't ask Knox: not proof it's gone
                }
                if (list.DeviceList?.Any(x => x.Imei == imei || x.Imei2 == imei || x.DeviceUid == imei) == true)
                {
                    return null;
                }
            }
            return $"Not in Knox any more (searched IMEI {d.Imei}{(d.Imei2 != null ? " and " + d.Imei2 : "")}). Send it to Knox again.";
        }

        private async Task<string?> MissingFromTranssionAsync(LockerDevice d)
        {
            var state = await _payTrigger.FindLockStateAsync(new FindLockStateRequest { Imei = d.Imei });
            if (state.Code == TranssionNotFoundCode)
            {
                return $"Not in Transsion any more: {state.Message} (code {state.Code}). Send it to Transsion again.";
            }
            if (state.IsSuccess && state.Data?.LockState == RecoveryRules.TranssionRemovedState)
            {
                return "Removed in Transsion (lock state 5000) while the contract is open. Send it to Transsion again.";
            }
            return null;
        }

        // findLockState: "The device-lock not exist".
        private const int TranssionNotFoundCode = 50071;

        private async Task<List<LockRecovery>> WaitingForTranssionAsync() =>
            (await _repository.GetListAsync("open"))
                .Where(r => RecoveryRules.IsTranssion(r.Make) && r.Status == RecoveryStatus.UploadSent)
                .ToList();

        private async Task<(bool, string?, string?)> UploadAsync(LockRecovery r)
        {
            var result = await _veritech.UploadDevicesAsync(new List<string> { r.Imei });
            var transId = result?.Data?.Transaction_Id;
            var ok = !string.IsNullOrWhiteSpace(transId);
            return (ok, Json(result), ok ? transId : null);
        }

        // Veritech says "Complete" whether or not Knox took the phone
        // (5871723, 8511324, 8583526, 8680519 never reached Knox), so a
        // complete upload is checked against Knox too.
        private async Task<(bool, string?, string?)> UploadStatusAsync(LockRecovery r)
        {
            var result = await _veritech.GetTransactionStatusAsync(r.VeritechTransId!);
            var d = result?.Data;
            if (d == null)
            {
                return (false, Json(result), null);
            }

            var summary = $"Status {d.Status}, result {d.Result}, {d.Message}";
            if (string.Equals(d.Status, "Complete", StringComparison.OrdinalIgnoreCase))
            {
                var inKnox = await InKnoxAsync(r.Imei);
                if (inKnox == false)
                {
                    return (false, $"Veritech says the upload is complete, but Knox doesn't have the phone. "
                        + $"Ask Veritech why transaction {r.VeritechTransId} didn't reach Knox (often: the phone is registered to another Knox account).\n"
                        + Json(result), null);
                }
                if (inKnox == true)
                {
                    summary += " -- Knox has the phone; approve it next.";
                }
            }
            return (true, summary + "\n" + Json(result), null);
        }

        // True/false when Knox answered; null when it couldn't be asked.
        private async Task<bool?> InKnoxAsync(string imei)
        {
            var list = await _knox.ListDevicesAsync(new ListDevicesRequest
            {
                PageNum = 0, PageSize = 20, SortBy = "updateTime", SortOrder = "descending", Search = imei,
            });
            if (list == null || !string.Equals(list.Result, "SUCCESS", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
            return list.DeviceList?.Any(x => x.Imei == imei || x.Imei2 == imei || x.DeviceUid == imei) == true;
        }

        private static async Task<(bool, string?, string?)> KnoxCallAsync(Func<Task<HttpResponseMessage>> call)
        {
            var response = await call();
            var body = await response.Content.ReadAsStringAsync();
            return (response.IsSuccessStatusCode, $"HTTP {(int)response.StatusCode} {body}", null);
        }

        private async Task<(bool, string?, string?)> KnoxCheckAsync(LockRecovery r)
        {
            var list = await _knox.ListDevicesAsync(new ListDevicesRequest
            {
                PageNum = 0,
                PageSize = 20,
                SortBy = "updateTime",
                SortOrder = "descending",
                Search = r.Imei,
            });
            var device = list?.DeviceList?.FirstOrDefault(d => d.Imei == r.Imei || d.Imei2 == r.Imei || d.DeviceUid == r.Imei);
            if (device == null)
            {
                return (false, "Not found in Knox. " + Json(list), null);
            }

            static string When(long ms) => ms > 0 ? DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime.ToString("dd/MM/yyyy HH:mm 'UTC'") : "never";
            var summary = $"Knox status {device.Status}; last seen {When(device.LastSeen)}; relock {When(device.RelockTimestamp)}"
                + $"; offline locked {device.IsOfflineLocked}; approve id {device.ApproveId}.";
            return (true, summary + "\n" + Json(device), null);
        }

        // Daily lock jobs find a Knox phone through Devices.LockGroup = 2 and
        // its Enrolments row (IMEI + Veritech id as the Knox approveId).
        private async Task<(bool, string?, string?)> MoveToKnoxAsync(LockRecovery r, int userId)
        {
            var existing = await _enrolments.GetByAccountIdAsync(r.AccountNo);
            var reason = RecoveryRules.CannotMoveToKnox(r, existing != null);
            if (reason != null)
            {
                return (false, reason, null);
            }

            var device = await _repository.LookupDeviceAsync(r.AccountNo);
            var now = DateTime.UtcNow;
            await _enrolments.CreateEnrolmentAsync(new Enrolment
            {
                Id = Guid.NewGuid(),
                AccountId = r.AccountNo,
                OrderId = 0,
                DealerId = device?.DealerId ?? 0,
                FirstName = r.CustomerName,
                IMEI = r.Imei,
                DeviceBrand = "Samsung",
                Created = now,
                Updated = now,
                ApprovedDate = now,
                Status = EnrolmentStatus.Approved,
                UpdatedBy = "RECOVERY",
                VeriTechTransId = r.VeritechTransId,
                EnrolledByUserId = userId,
                EnrolledByRole = (int)UserRole.Admin,
                ProductName = $"{r.Make} {r.Model}".Trim(),
            });

            await _repository.SetLockGroupAsync(r.AccountNo, RecoveryRules.KnoxLockGroup,
                r.Status == RecoveryStatus.LockSent ? true : null);
            await _repository.MarkMovedAsync(r.Id, RecoveryStatus.MovedToKnox);

            return (true, $"Account {r.AccountNo} moved from {RecoveryRules.LockGroupName(r.CurrentLockGroup)} to Knox. "
                + "The daily lock jobs now use Knox for this phone.", null);
        }

        // Pre-enrol with preLock: a phone in Recovery is assumed hacked or
        // flashed, so it locks the moment Transsion has it active. Nuovo is
        // left on the phone as it is.
        private async Task<(bool, string?, string?)> TranssionSendAsync(LockRecovery r)
        {
            var response = await _payTrigger.PreEnrollImeiAsync(
                new List<ImeiEnrollItem> { new() { Imei = r.Imei, OrderNum = r.AccountNo.ToString() } },
                preLockFlag: true);

            // PayTrigger lists the IMEIs it couldn't take in Data.
            var failed = response.Data?.FirstOrDefault(d => d.Imei == r.Imei);
            var ok = response.IsSuccess && failed == null;
            var summary = ok
                ? "Transsion took the IMEI. It locks as soon as the phone connects; Check in Transsion then hands it to the daily lock jobs."
                : $"Transsion refused the IMEI: {failed?.Message ?? response.Message} (code {failed?.ErrCode ?? response.Code})";
            return (ok, summary + "\n" + Json(response), null);
        }

        private async Task<(bool, string?, string?)> TranssionCheckAsync(LockRecovery r, Action<int> state)
        {
            var response = await _payTrigger.FindLockStateAsync(new FindLockStateRequest { Imei = r.Imei });
            var d = response.Data;
            if (!response.IsSuccess || d == null)
            {
                return (false, $"Not found in Transsion: {response.Message} (code {response.Code})\n" + Json(response), null);
            }

            state(d.LockState);
            if (d.LockState == 0 && d.LastConnectTime is null or 0)
            {
                // Nothing from the phone yet: the lock fields are defaults.
                return (true, "Transsion has the IMEI, but the phone hasn't contacted Transsion yet, so it isn't locked yet. "
                    + "Switch it on and connect it to the internet, then check again.\n" + Json(d), null);
            }
            static string When(long? s) => s is > 0 ? DateTimeOffset.FromUnixTimeSeconds(s.Value > 99999999999 ? s.Value / 1000 : s.Value).UtcDateTime.ToString("dd/MM/yyyy HH:mm 'UTC'") : "never";
            var summary = $"Transsion: {RecoveryRules.TranssionStateName(d.LockState)}, {(d.MobileStatus == 1000 ? "locked" : d.MobileStatus == 2000 ? "unlocked" : $"phone status {d.MobileStatus}")}"
                + $"; last connected {When(d.LastConnectTime)}; lock date {When(d.Expiration)}; model {d.Model}.";
            return (true, summary + "\n" + Json(d), null);
        }

        // updateRepayInfo moves the lock date: now locks, a day ahead unlocks
        // (the paying-lock job sets it from payments once moved).
        private async Task<(bool, string?, string?)> TranssionRepayAsync(LockRecovery r, DateTime lockAtUtc, string description)
        {
            var response = await _payTrigger.UpdateRepayInfoAsync(new UpdateRepayInfoRequest
            {
                Imei = r.Imei,
                NextRepayTime = new DateTimeOffset(lockAtUtc).ToUnixTimeSeconds(),
                Description = description,
            });
            return (response.IsSuccess, $"Lock date {lockAtUtc:dd/MM/yyyy HH:mm} UTC: {response.Message} (code {response.Code})", null);
        }

        // As Move to Knox: the daily lock jobs find a Transsion phone through
        // Devices.LockGroup = 3 and its Enrolments row (the IMEI). The row is
        // marked activated, call-approved and unlocked so the enrolment page
        // and the activation webhook leave it alone.
        private async Task<(bool, string?, string?)> MoveToTranssionAsync(LockRecovery r, int userId)
        {
            var existing = await _enrolments.GetByAccountIdAsync(r.AccountNo);
            var reason = RecoveryRules.CannotMoveToTranssion(r, existing != null);
            if (reason != null)
            {
                return (false, reason, null);
            }

            var device = await _repository.LookupDeviceAsync(r.AccountNo);
            var now = UtcNow;
            await _enrolments.CreateEnrolmentAsync(new Enrolment
            {
                Id = Guid.NewGuid(),
                AccountId = r.AccountNo,
                OrderId = 0,
                DealerId = device?.DealerId ?? 0,
                FirstName = r.CustomerName,
                IMEI = r.Imei,
                DeviceBrand = RecoveryRules.TranssionBrand(r.Make),
                Created = now,
                Updated = now,
                ApprovedDate = now,
                ActivatedAt = now,
                CallApprovedAt = now,
                CallApprovedByUserId = userId,
                UnlockedAt = now,
                Status = EnrolmentStatus.Approved,
                UpdatedBy = "RECOVERY",
                PayTriggerStatus = "Recovery",
                PayTriggerResponse = $"Moved from {RecoveryRules.LockGroupName(r.CurrentLockGroup)} by Recovery {r.Id}.",
                EnrolledByUserId = userId,
                EnrolledByRole = (int)UserRole.Admin,
                ProductName = $"{r.Make} {r.Model}".Trim(),
            });

            await _repository.SetLockGroupAsync(r.AccountNo, RecoveryRules.TranssionLockGroup,
                r.Status == RecoveryStatus.LockSent ? true : null);
            await _repository.MarkMovedAsync(r.Id, RecoveryStatus.MovedToTranssion);

            return (true, $"Account {r.AccountNo} moved from {RecoveryRules.LockGroupName(r.CurrentLockGroup)} to Transsion. "
                + "The daily lock jobs now use Transsion for this phone.", null);
        }

        private static DeviceActionsRequest KnoxRelock(LockRecovery r, DateTime relockAtUtc) => new()
        {
            DeviceUid = r.Imei,
            ApproveId = r.VeritechTransId,
            Actions = new List<DeviceActionItem>
            {
                new() { Action = "unLock", Timestamp = 0 },
                new() { Action = "lock", Timestamp = new DateTimeOffset(relockAtUtc).ToUnixTimeMilliseconds(), Message = LockMessage },
            },
        };

        private static string Describe(RecoveryAction action, bool transsion = false) => action switch
        {
            RecoveryAction.TranssionSend => "Send to Transsion",
            RecoveryAction.TranssionCheck => "Check in Transsion",
            RecoveryAction.MoveToTranssion => "Move to Transsion",
            RecoveryAction.Lock when transsion => "Lock (Transsion)",
            RecoveryAction.Unlock when transsion => "Unlock (Transsion)",
            RecoveryAction.Upload => "Send to Knox (Veritech upload)",
            RecoveryAction.UploadStatus => "Check upload",
            RecoveryAction.Approve => "Approve in Knox",
            RecoveryAction.KnoxCheck => "Check in Knox",
            RecoveryAction.Lock => "Lock",
            RecoveryAction.Unlock => "Unlock",
            RecoveryAction.MoveToKnox => "Move to Knox",
            RecoveryAction.NotCatchable => "Mark not catchable",
            _ => action.ToString(),
        };

        private static string Json(object? value)
        {
            try { return JsonSerializer.Serialize(value); }
            catch { return value?.ToString() ?? ""; }
        }

        private static string FirstLine(string? text)
        {
            var line = (text ?? "").Split('\n')[0];
            return line.Length > 300 ? line[..300] + "…" : line;
        }

        private static string? Truncate(string? text) =>
            text != null && text.Length > MaxResponseLength ? text[..MaxResponseLength] : text;
    }
}
