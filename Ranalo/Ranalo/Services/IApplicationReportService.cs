using Ranalo.DataStore;
using Ranalo.Models;

namespace Ranalo.Services
{
    public interface IApplicationReportService
    {
        Task<AwaitingApprovalViewModel> GetAwaitingApprovalOrders(string searchTerm = "", int page = 1, int pageSize = 10);
        Task<AwaitingApprovalViewModel> GetAwaitingApprovalOrdersByUser(int userId, string searchTerm = "", int page = 1, int pageSize = 10);

        Task<List<KosePayments>?> GetOrphanedPaymentsAsync();

        Task<IEnumerable<PaymentsSummaryTotals>> PaymentsSummary();

        Task<KosePaymentsViewModel> GetAllPaymentsAsync(string searchTerm = "", int page = 1, int pageSize = 10);
        Task<KosePaymentsViewModel> GetAllPaymentsAsync(int userId, string searchTerm = "", int page = 1, int pageSize = 10);

        Task<List<Device>> GetAllDevicesAsync();


        Task<List<AwaitingApprovalDto>> GetAllOrdersAsync();
        Task<List<Dealer>> GetAllDealersAsync();
        Task<CustomerDetails> GetCustomerDetailsByOrderIdAsync(long orderId);

        Task<int> ApproveOrderAsync(long orderId);

        Task<int> RejectOrderAsync(long orderId);
    }
}