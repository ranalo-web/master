using Ranalo.Woocommece.Api.Models;

namespace Ranalo.Woocommece.Api.DataStore
{
    public interface IWooOrderProductRepository
    {
        Task<IEnumerable<OrderProduct>> GetAllAsync();
        Task<OrderProduct?> GetByIdAsync(int id);
        Task<IEnumerable<OrderProduct>?> GetByProductsForOrderIdAsync(int orderId);
        Task<OrderProduct?> GetLastCreatedProductOrderAsync();
        Task<int> InsertAsync(OrderProduct product);
        Task<int> InsertImageDetailsAsync(long orderId, ImagesMetadata imageDetail);
    }
}