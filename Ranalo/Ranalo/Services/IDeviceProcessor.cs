using Ranalo.Models;

namespace Ranalo.Services
{
    public interface IDeviceProcessor
    {
        Task<List<LockTransaction>> ProcessBatchesAsync(List<LockTransaction> devices, ILogger logger);
        Task<LockTransaction> ProcessSingleAsync(LockTransaction device,
    ILogger logger);
    }
}