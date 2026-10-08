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

        Task<MpesaRecord?> GetAccountDetailsByMpesa(string mpesaCode);

        // The account a payment belongs to: where it was assigned (Assign
        // Payments / OrphanedPayments) if it was, else the account the
        // customer typed on M-Pesa. Null if there's no such payment.
        Task<string?> GetPaymentAccountNoAsync(string mpesaCode);
    }
}