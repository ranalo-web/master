using WooCommerceNET.WooCommerce.v3;

namespace Ranalo.ScheduledServices
{
    public interface IWooCommerceService
    {
        Task<List<Order>> GetOrders();
    }
}