using Ranalo.VeriTechClient.Models;

namespace Ranalo.VeriTechClient
{
    public interface IVeritechApiClient
    {
        Task<GetDevicesResponse> GetDevicesAsync();
        Task<CreateDeviceResponse> UploadDevicesAsync(List<string> devices);
        Task<DeleteDeviceResponse> DeleteDevicesAsync(List<string> devices);
        Task<SuccessTransactionStatusResponse> GetTransactionStatusAsync(string transactionId);

        Task<LockDeviceResponse> LockDeviceAsync(LockDeviceInput input);
        Task<UnlockDeviceResponse> UnlockDeviceAsync(UnlockDeviceInput input);
    }
}
