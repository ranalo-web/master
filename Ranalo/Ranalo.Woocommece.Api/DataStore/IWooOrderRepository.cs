using Ranalo.Woocommece.Api.Models;

namespace Ranalo.Woocommece.Api.DataStore
{
    public interface IWooOrderRepository
    {
        Task<IEnumerable<WooOrder>> GetAllAsync();
        Task<WooOrder?> GetByIdAsync(int id);
        Task<WooOrder?> GetByOrderIdAsync(long orderId);
        Task<int> InsertAsync(WooOrder order);
        Task<WooOrder?> GetLastSyncedOrderAsync();
        Task UpdateAsync(WooOrder order);
    }
}