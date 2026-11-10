using Ranalo.DataStore;
using Ranalo.DataStore.DataModels;
using Ranalo.Models;
using Ranalo.SumsungKnox;
using Ranalo.SumsungKnox.Models;

namespace Ranalo.Services
{
    public interface IKnoxSwitchService
    {
        // (done, message for staff)
        Task<(bool Done, string Message)> SwitchToKnoxAsync(long accountNo, int userId);
    }

    // "Switch to Knox" on the Knox check page: a phone Knox has active while
    // we send its locks and messages elsewhere. Does what Recovery's "Move to
    // Knox" does (RecoveryService.MoveToKnoxAsync) for a phone not in
    // Recovery: the daily lock jobs find a Knox phone by Devices.LockGroup = 2
    // and its Enrolments row, whose VeriTechTransId is Knox's approve id.
    // Checks Knox live first; anything unclear is refused with the reason.
    public class KnoxSwitchService : IKnoxSwitchService
    {
        private readonly IKnoxStatusRepository _status;
        private readonly ILockRecoveryRepository _recoveries;
        private readonly IEnrolmentRepository _enrolments;
        private readonly IKnoxGuardClient _knox;
        private readonly ILogger<KnoxSwitchService> _logger;

        public KnoxSwitchService(IKnoxStatusRepository status, ILockRecoveryRepository recoveries,
            IEnrolmentRepository enrolments, IKnoxGuardClient knox, ILogger<KnoxSwitchService> logger)
        {
            _status = status;
            _recoveries = recoveries;
            _enrolments = enrolments;
            _knox = knox;
            _logger = logger;
        }

        public async Task<(bool Done, string Message)> SwitchToKnoxAsync(long accountNo, int userId)
        {
            var row = await _status.GetAsync(accountNo);
            if (row == null || string.IsNullOrWhiteSpace(row.Imei)) return (false, $"Account {accountNo}: no phone found.");
            if (row.LockGroup == RecoveryRules.KnoxLockGroup) return (false, $"Account {accountNo} is already on Knox.");
            if (row.OpenRecoveryId is int rid) return (false, $"Account {accountNo} is in Recovery (#{rid}); it moves from there once Knox has it active.");

            // Live, not the last 2-hourly read.
            var list = await _knox.ListDevicesAsync(new ListDevicesRequest
            {
                PageNum = 0, PageSize = 20, SortBy = "updateTime", SortOrder = "descending", Search = row.Imei,
            });
            var device = list?.DeviceList?.FirstOrDefault(d => d.Imei == row.Imei || d.Imei2 == row.Imei);
            if (device == null) return (false, $"Knox doesn't have IMEI {row.Imei}.");
            if (!KnoxStatusRules.Active(device.Status))
                return (false, $"Knox shows the phone as {device.Status}: {KnoxStatusRules.Explain(device.Status)}. It can switch once Knox has it active.");
            if (string.IsNullOrWhiteSpace(device.ApproveId))
                return (false, "Knox gives no approve id for this phone; the lock jobs need one. Sort it out in Recovery.");

            var enrolment = await _enrolments.GetByAccountIdAsync(accountNo);
            if (enrolment == null)
            {
                var now = DateTime.UtcNow;
                await _enrolments.CreateEnrolmentAsync(new Enrolment
                {
                    Id = Guid.NewGuid(),
                    AccountId = accountNo,
                    OrderId = 0,
                    FirstName = row.FirstName,
                    IMEI = row.Imei,
                    DeviceBrand = "Samsung",
                    Created = now,
                    Updated = now,
                    ApprovedDate = now,
                    Status = EnrolmentStatus.Approved,
                    UpdatedBy = "KNOX CHECK",
                    VeriTechTransId = device.ApproveId,
                    EnrolledByUserId = userId,
                    EnrolledByRole = (int)UserRole.Admin,
                    ProductName = row.Model,
                });
            }
            else if (string.IsNullOrWhiteSpace(enrolment.VeriTechTransId))
            {
                enrolment.VeriTechTransId = device.ApproveId;
                enrolment.Updated = DateTime.UtcNow;
                enrolment.UpdatedBy = "KNOX CHECK";
                await _enrolments.UpdateEnrolmentAsync(enrolment);
            }
            else if (!string.Equals(enrolment.VeriTechTransId, device.ApproveId, StringComparison.OrdinalIgnoreCase))
            {
                return (false, $"Account {accountNo}'s enrolment has a different Knox id ({enrolment.VeriTechTransId}) than Knox ({device.ApproveId}); it needs sorting out by hand.");
            }

            await _recoveries.SetLockGroupAsync(accountNo, RecoveryRules.KnoxLockGroup, null);
            _logger.LogInformation("Knox check: account {Account} switched from {From} to Knox by user {User}.",
                accountNo, RecoveryRules.LockGroupName(row.LockGroup), userId);
            return (true, $"Account {accountNo} switched from {RecoveryRules.LockGroupName(row.LockGroup)} to Knox. Its locks and messages now go through Knox.");
        }
    }
}
