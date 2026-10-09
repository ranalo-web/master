using System.Text.Json;
using Ranalo.DataStore;
using Ranalo.DataStore.DataModels;
using Ranalo.Models;
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
    }

    // Runs one recovery step at a time (RecoveryRules) against Veritech and
    // Knox, the same calls enrolment uses (EnrolmentService), and logs every
    // reply on the recovery.
    public class RecoveryService : IRecoveryService
    {
        public const string LockMessage = "This phone is locked by Ranalo Credit. Please call us to settle your account.";
        private const int MaxResponseLength = 8000;

        private readonly ILockRecoveryRepository _repository;
        private readonly IEnrolmentRepository _enrolments;
        private readonly IVeritechApiClient _veritech;
        private readonly IKnoxGuardClient _knox;
        private readonly ILogger<RecoveryService> _logger;

        public RecoveryService(ILockRecoveryRepository repository, IEnrolmentRepository enrolments,
            IVeritechApiClient veritech, IKnoxGuardClient knox, ILogger<RecoveryService> logger)
        {
            _repository = repository;
            _enrolments = enrolments;
            _veritech = veritech;
            _knox = knox;
            _logger = logger;
        }

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

            note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
            try
            {
                var (success, response, transId) = action switch
                {
                    RecoveryAction.Upload => await UploadAsync(r),
                    RecoveryAction.UploadStatus => await UploadStatusAsync(r),
                    RecoveryAction.Approve => await KnoxCallAsync(() => _knox.ApproveDeviceAsync(new ApproveDeviceRequest
                    {
                        DeviceUid = r.Imei,
                        ApproveId = r.VeritechTransId,
                        ApproveComment = $"Recovery {r.Id} - account {r.AccountNo}",
                    })),
                    RecoveryAction.KnoxCheck => await KnoxCheckAsync(r),
                    RecoveryAction.Lock => await KnoxCallAsync(() => _knox.LockDeviceAsync(new LockDeviceRequest
                    {
                        DeviceUid = r.Imei,
                        ApproveId = r.VeritechTransId,
                        Message = LockMessage,
                    })),
                    RecoveryAction.Unlock => await KnoxCallAsync(() => _knox.UnlockDeviceAsync(new UnlockDeviceRequest
                    {
                        DeviceUid = r.Imei,
                        ApproveId = r.VeritechTransId,
                        Message = "Unlocked by Ranalo Credit.",
                    })),
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

                var newStatus = RecoveryRules.StatusAfter(action, success, r.Status);
                if (action != RecoveryAction.MoveToKnox && (newStatus != r.Status || transId != null))
                {
                    await _repository.UpdateAsync(r.Id, newStatus, transId);
                }

                await _repository.LogAsync(r.Id, action.ToString(), success, Truncate(response), userId);
                _logger.LogInformation("Recovery {Id} (account {Account}): {Action} by user {UserId}, success {Success}",
                    r.Id, r.AccountNo, action, userId, success);

                return new(success, success
                    ? $"{Describe(action)}: done. {FirstLine(reply)}"
                    : $"{Describe(action)} failed. {FirstLine(reply)}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Recovery {Id}: {Action} failed", r.Id, action);
                await _repository.LogAsync(r.Id, action.ToString(), false, Truncate(ex.Message), userId);
                return new(false, $"{Describe(action)} failed: {ex.Message}");
            }
        }

        private async Task<(bool, string?, string?)> UploadAsync(LockRecovery r)
        {
            var result = await _veritech.UploadDevicesAsync(new List<string> { r.Imei });
            var transId = result?.Data?.Transaction_Id;
            var ok = !string.IsNullOrWhiteSpace(transId);
            return (ok, Json(result), ok ? transId : null);
        }

        private async Task<(bool, string?, string?)> UploadStatusAsync(LockRecovery r)
        {
            var result = await _veritech.GetTransactionStatusAsync(r.VeritechTransId!);
            var d = result?.Data;
            var summary = d == null ? "" : $"Status {d.Status}, result {d.Result}, {d.Message}\n";
            return (d != null, summary + Json(result), null);
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
            await _repository.MarkMovedToKnoxAsync(r.Id);

            return (true, $"Account {r.AccountNo} moved from {RecoveryRules.LockGroupName(r.CurrentLockGroup)} to Knox. "
                + "The daily lock jobs now use Knox for this phone.", null);
        }

        private static string Describe(RecoveryAction action) => action switch
        {
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
