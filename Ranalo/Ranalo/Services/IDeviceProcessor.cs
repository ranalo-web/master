using Ranalo.Models;

namespace Ranalo.Services
{
    public interface IDeviceProcessor
    {
        Task<List<LockTransaction>> ProcessBatchesAsync(List<LockTransaction> devices, ILogger logger);
        Task<LockTransaction> ProcessSingleAsync(LockTransaction device,
    ILogger logger);

        // Releases a fully-paid device from Nuovo (keeps it in the Nuovo
        // dashboard: delete_device=false). Only called from the admin
        // removal approval -- see DeviceRemovalService.
        Task<(bool Success, string Response)> UnregisterAsync(long deviceId);
    }
}