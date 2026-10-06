using System.Globalization;
using Ranalo.DataStore;
using Ranalo.DataStore.DataModels;
using Ranalo.Models;
using Ranalo.PayTrigger;
using Ranalo.SumsungKnox;
using Ranalo.SumsungKnox.Models;
using Ranalo.Services;
using Ranalo.Woocommece.Api.DataStore;
using PayTriggerModels = Ranalo.PayTrigger.Models;

namespace Ranalo.Services.DeviceLock
{
    // The fully-paid removal queue behind the admin Pending Tasks page. A
    // device is only ever removed from its lock provider here, after an
    // admin approves it (DeviceLockRules.CanDecideRemovals).
    public interface IDeviceRemovalService
    {
        Task<int> QueueAsync(IEnumerable<DeviceRemovalTask> candidates);

        // Queues devices already at the 31/12/9999 lock date (fully paid
        // before the queue existed). Returns how many were queued.
        Task<int> QueueExistingFullyPaidAsync();

        Task<WorkflowResult> ApproveAsync(int taskId, User actor);
        Task<WorkflowResult> RejectAsync(int taskId, User actor, string? note);
    }

    public class DeviceRemovalService : IDeviceRemovalService
    {
        public const string FullyPaidReason = "Fully paid";

        private readonly IDeviceRemovalTaskRepository _tasks;
        private readonly IEnrolmentRepository _enrolments;
        private readonly IKosePaymentsRepository _devices;
        private readonly IPayTriggerClient _payTrigger;
        private readonly IKnoxGuardClient _knox;
        private readonly IDeviceProcessor _nuovo;
        private readonly TimeProvider _time;
        private readonly ILogger<DeviceRemovalService> _logger;

        public DeviceRemovalService(
            IDeviceRemovalTaskRepository tasks,
            IEnrolmentRepository enrolments,
            IKosePaymentsRepository devices,
            IPayTriggerClient payTrigger,
            IKnoxGuardClient knox,
            IDeviceProcessor nuovo,
            TimeProvider time,
            ILogger<DeviceRemovalService> logger)
        {
            _nuovo = nuovo;
            _tasks = tasks;
            _enrolments = enrolments;
            _devices = devices;
            _payTrigger = payTrigger;
            _knox = knox;
            _time = time;
            _logger = logger;
        }

        private DateTime UtcNow => _time.GetUtcNow().UtcDateTime;

        public async Task<int> QueueAsync(IEnumerable<DeviceRemovalTask> candidates)
        {
            var queued = 0;
            foreach (var task in candidates)
            {
                task.Provider = DeviceLockRules.ProviderForLockGroup(task.LockGroup).ToString();
                if (string.IsNullOrWhiteSpace(task.Reason))
                {
                    task.Reason = FullyPaidReason;
                }

                if (await _tasks.EnqueueAsync(task))
                {
                    queued++;
                }
            }

            return queued;
        }

        public async Task<int> QueueExistingFullyPaidAsync()
        {
            return await QueueAsync(await _tasks.FindFullyPaidWithoutTaskAsync());
        }

        public async Task<WorkflowResult> ApproveAsync(int taskId, User actor)
        {
            if (!DeviceLockRules.CanDecideRemovals(actor.RoleId))
            {
                return new WorkflowResult(false, "Only an admin can approve removing a device.");
            }

            // Claim first: whoever claims it is the only one who calls the
            // provider, so a double click or two admins can't remove twice.
            if (!await _tasks.TryClaimAsync(taskId, actor.UserId, UtcNow))
            {
                var current = await _tasks.GetAsync(taskId);
                return new WorkflowResult(false, current == null
                    ? "Task not found."
                    : $"Task for account {current.AccountId} is already {current.Status.ToLowerInvariant()}.");
            }

            var task = (await _tasks.GetAsync(taskId))!;

            ProviderResult result;
            try
            {
                result = await RemoveFromProviderAsync(task);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Removing account {AccountId} from {Provider} failed", task.AccountId, task.Provider);
                result = new ProviderResult(false, ex.Message);
            }

            if (!result.Success)
            {
                await _tasks.MarkFailedAsync(taskId, result.Response, UtcNow);
                return new WorkflowResult(false, $"Account {task.AccountId}: {task.Provider} removal failed – {result.Response}. It stays in the queue to retry.");
            }

            await _tasks.MarkCompletedAsync(taskId, result.Response, UtcNow);

            try
            {
                var device = await _devices.GetDeviceByAccountId(task.AccountId);
                if (device != null)
                {
                    // Status stays 'enrolled' so the account keeps showing in
                    // reports and commissions; the 9999 lock date keeps the
                    // lock jobs off it.
                    device.Locked = false;
                    device.LockType = "unlocked";
                    device.EnrollmentStatus = "Removed";
                    await _devices.UpdateDeviceToDatabaseAsync(device);
                }
            }
            catch (Exception ex)
            {
                // The provider removal is done and recorded; only our copy of
                // the device row is stale.
                _logger.LogError(ex, "Removed account {AccountId} from {Provider} but couldn't update Devices", task.AccountId, task.Provider);
            }

            return new WorkflowResult(true, $"Account {task.AccountId} removed from {task.Provider}.");
        }

        public async Task<WorkflowResult> RejectAsync(int taskId, User actor, string? note)
        {
            if (!DeviceLockRules.CanDecideRemovals(actor.RoleId))
            {
                return new WorkflowResult(false, "Only an admin can reject a removal.");
            }

            return await _tasks.RejectAsync(taskId, actor.UserId, string.IsNullOrWhiteSpace(note) ? null : note.Trim(), UtcNow)
                ? new WorkflowResult(true, "Removal rejected. The device stays with its lock provider, unlocked with its fully-paid lock date.")
                : new WorkflowResult(false, "This task can no longer be rejected.");
        }

        private async Task<ProviderResult> RemoveFromProviderAsync(DeviceRemovalTask task)
        {
            var provider = Enum.Parse<LockProvider>(task.Provider);

            // Nuovo identifies the device by its id (= the account); Knox and
            // Transsion by IMEI.
            if (provider == LockProvider.Nuovo)
            {
                var (success, response) = await _nuovo.UnregisterAsync(task.AccountId);
                return new ProviderResult(success, response);
            }

            if (string.IsNullOrWhiteSpace(task.Imei))
            {
                return new ProviderResult(false, "No IMEI recorded for this device.");
            }

            if (provider == LockProvider.Transsion)
            {
                var response = await _payTrigger.RemoveLockAsync(new PayTriggerModels.RemoveLockRequest { Imei = task.Imei });
                return new ProviderResult(response.IsSuccess, $"{response.Code} {response.Message}");
            }

            var enrolment = await _enrolments.GetByImeiNumberAsync(task.Imei)
                            ?? await _enrolments.GetByAccountIdAsync(task.AccountId);

            var knoxResponse = await _knox.CompleteDeviceAsync(new CompleteDeviceRequest
            {
                DeviceUid = task.Imei,
                ApproveId = enrolment?.VeriTechTransId,
                Message = "Fully paid"
            });
            var body = await knoxResponse.Content.ReadAsStringAsync();
            return new ProviderResult(knoxResponse.IsSuccessStatusCode,
                $"{(int)knoxResponse.StatusCode} {Truncate(body, 1000)}");
        }

        private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];

        private record ProviderResult(bool Success, string Response);
    }

    // Picks fully-paid accounts out of the status report for
    // ScheduledLockFullyPaid. Once an account's lock date is 31/12/9999 it no
    // longer matches (year != this year), so each account is handled once --
    // and again the next day only if setting the date failed.
    public static class FullyPaidPlanner
    {
        public static List<MobileStatusReport> Select(IEnumerable<MobileStatusReport>? records, int currentYear) =>
            (records ?? Enumerable.Empty<MobileStatusReport>())
                .Where(x => x.Arrears >= 0 &&
                            x.LoanBalance <= 0 &&
                            ParseLockDate(x.NextLockDate)?.Year == currentYear)
                .ToList();

        // Lock dates come in several formats (see TimestampHelper and the
        // Nuovo sync). An unreadable one is skipped rather than stopping the
        // whole run.
        public static DateTime? ParseLockDate(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;

            string[] formats =
            {
                "dd/MM/yyyy HH:mm:ss",
                "dd/MM/yyyy HH:mm:ss.FFFFFFF",
                "yyyy-MM-dd HH:mm:ss",
                "yyyy-MM-dd HH:mm:ss.FFFFFFF",
                "dd/MM/yyyy'T'HH:mm:ss",
                "dd/MM/yyyy'T'HH:mm:ss.FFFFFFF",
                "d/M/yyyy h:mm:ss tt",
                "dd/MM/yyyy h:mm:ss tt",
                "d/M/yyyy hh:mm:ss tt",
                "dd/MM/yyyy hh:mm:ss tt",
                "dd/MM/yyyy"
            };

            return DateTime.TryParseExact(value.Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
                ? parsed
                : null;
        }

        public static DeviceRemovalTask ToTask(MobileStatusReport account) => new()
        {
            AccountId = account.AccountNo,
            Imei = account.ImeiNo,
            CustomerName = account.FirstName,
            LockGroup = account.LockGroup,
            Reason = DeviceRemovalService.FullyPaidReason
        };
    }
}
