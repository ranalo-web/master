using Ranalo.DataStore.DataModels;
using Ranalo.Models;

namespace Ranalo.DataStore
{
    public interface IApplicationReportRepository
    {
        Task<KosePaymentsViewModel> GetAllPaymentsAsync(string searchTerm = "", int page = 1, int pageSize = 10);
        Task<KosePaymentsViewModel> GetAllPaymentsByDealerIdAsync(int dealerId, string searchTerm = "", int page = 1, int pageSize = 10);

        Task<AwaitingApprovalViewModel> GetAllWaitingApprovalAsync(string searchTerm = "", int page = 1, int pageSize = 10);
        Task<AwaitingApprovalViewModel> GetAllOrdersByUserAsync(int dealerId, string searchTerm, int page, int pageSize);

        Task<IEnumerable<KosePayments>> GetOrphanedPaymentsAsync();

        Task<IEnumerable<AwaitingApprovalDto>> GetAllOrdersAsync();
        Task<IEnumerable<Dealer>> GetAllDealersAsync();

        Task<IEnumerable<Device>> GetAllDevicesAsync();
        Task<CustomerDetails?> GetCustomerDetails(long orderId);
        Task<int> RejectOrder(long orderId);

        Task<int> ApproveOrder(long orderId);
        Task<IEnumerable<ImagesMetadata>> GetIdentityImagesForOrder(long orderId);

        Task<AccountSummary?> GetPaymentSummaryForAccountId(string accountNo);
        Task<string?> GetCustomerAccountByMpesa(string mpesaDepositRef);
        Task<AccountSummary?> GetAccountSummary(string customerAccount);
    }
}