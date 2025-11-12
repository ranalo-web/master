using Ranalo.Models;

namespace Ranalo.Services
{
    public interface IDeviceProcessor
    {
        Task<List<LockTransaction>> ProcessBatchesAsync(List<LockTransaction> devices);
    }
}