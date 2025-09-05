using Ranalo.DataStore.DataModels;
using Ranalo.Models;
using Ranalo.Woocommece.Api.Models;
using ImagesMetadata = Ranalo.Models.ImagesMetadata;

namespace Ranalo.DataStore
{
    public interface IApplicationReportRepository
    {
        Task<KosePaymentsViewModel> GetAllPaymentsAsync(string searchTerm = "", int page = 1, int pageSize = 10);
        Task<KosePaymentsViewModel> GetAllPaymentsByDealerIdAsync(int dealerId, string searchTerm = "", int page = 1, int pageSize = 10);

        Task<AwaitingApprovalViewModel> GetAllWaitingApprovalAsync(string searchTerm = "", int page = 1, int pageSize = 10);
        Task<AwaitingApprovalViewModel> GetAllOrdersByUserAsync(int dealerId, string searchTerm, int page, int pageSize);

        Task<KosePaymentsViewModel> GetOrphanedPaymentsAsync(int page, int pageSize);

        Task<IEnumerable<AwaitingApprovalDto>> GetAllOrdersAsync();
        Task<IEnumerable<Dealer>> GetAllDealersAsync();

        Task<IEnumerable<Device>> GetAllDevicesAsync();
        Task<CustomerDetails?> GetCustomerDetails(long orderId);
        Task<CustomerDetails?> GetCustomerDetailsByFirstMpesaCode(string firstMpesaCode);
        Task<int> RejectOrder(long orderId);

        Task<int> ApproveOrder(long orderId);
        Task<IEnumerable<ImagesMetadata>> GetIdentityImagesForOrder(long orderId);

        Task<AccountSummary?> GetPaymentSummaryForAccountId(string accountNo);
        Task<string?> GetCustomerAccountByMpesa(string mpesaDepositRef);
        Task<AccountSummary?> GetAccountSummary(string customerAccount);
        Task<IEnumerable<PaymentSummary>> GetPaymentSummaryByDeviceGroupAsync(int deviceGroupId);
        Task<IEnumerable<PaymentSummary>> GetPaymentSummaryAsync();
        Task CreateCustomerNote(CustomerNote newNote);
        Task<List<CustomerNote>> GetNotesByOrderId(long orderId);
        Task<WooOrderProduct?> GetProductDetailsForOrder(long orderId);
        Task<Contact?> GetNextOfKinForOrder(long orderId);

        Task<AwaitingApprovalViewModel> GetAllMissingMpesaAsync(string searchTerm = "", int page = 1, int pageSize = 10);
    }
}