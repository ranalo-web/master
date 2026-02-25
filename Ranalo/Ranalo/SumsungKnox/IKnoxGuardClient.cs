using Ranalo.SumsungKnox.Models;

namespace Ranalo.SumsungKnox
{
    public interface IKnoxGuardClient
    {
        Task<HttpResponseMessage> ApproveDeviceAsync(ApproveDeviceRequest request);

        Task<HttpResponseMessage> ExecuteDeviceActionsAsync(DeviceActionsRequest request);
        Task<HttpResponseMessage> SetBlinkingReminderAsync(SetBlinkingReminderRequest request);
        Task<HttpResponseMessage> CompleteDeviceAsync(CompleteDeviceRequest request);
        Task<HttpResponseMessage> DeleteDeviceAsync(DeleteDeviceRequest request);
        Task<HttpResponseMessage> LockDeviceAsync(LockDeviceRequest request);
        Task<HttpResponseMessage> SendMessageAsync(SendMessageRequest request);
        Task<HttpResponseMessage> GetDeviceInfoAsync(string deviceId);

        Task<ListDevicesResponse> ListDevicesAsync(ListDevicesRequest request);

        Task<HttpResponseMessage> UnlockDeviceAsync(UnlockDeviceRequest request);
    }
}
