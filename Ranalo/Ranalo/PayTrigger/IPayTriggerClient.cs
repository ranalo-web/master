using Ranalo.PayTrigger.Models;

namespace Ranalo.PayTrigger
{
    // Client for Transsion's PayTrigger Partner API. Method set matches the
    // subset of the real API this codebase actually needs (enrolment,
    // payment-driven lock extension, full-payoff removal, device lookup,
    // reminder messages) -- see PayTriggerClient.cs for endpoint paths and
    // the PayTrigger PDF doc in this session for the full 28-endpoint API.
    public interface IPayTriggerClient
    {
        // Pre-enrols one or more IMEIs (api/partner/lock/v1/imei/input).
        Task<PreEnrollImeiResponse> PreEnrollImeiAsync(List<ImeiEnrollItem> items, bool preLockFlag);

        // Payment-driven lock-date extension (api/partner/lock/v1/updateRepayInfo).
        Task<PayTriggerApiResponse> UpdateRepayInfoAsync(UpdateRepayInfoRequest request);

        // Full-payoff device release (api/partner/lock/v1/removeLock).
        Task<PayTriggerApiResponse> RemoveLockAsync(RemoveLockRequest request);

        // Device lookup by IMEI/deviceTag (api/partner/lock/v1/findLockState).
        Task<FindLockStateResponse> FindLockStateAsync(FindLockStateRequest request);

        // Reminder/notification push to a device (api/partner/push/v1/sendPushInfo).
        Task<PayTriggerApiResponse> SendPushInfoAsync(SendPushInfoRequest request);
    }
}
