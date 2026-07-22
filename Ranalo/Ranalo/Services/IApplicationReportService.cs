using Ranalo.DataStore;
using Ranalo.Models;
using Dealer = Ranalo.DataStore.Dealer;

namespace Ranalo.Services
{
    public interface IApplicationReportService
    {
        Task<CustomerDetails> GetCustomerDetailsByAccountIdAsync(long orderId);
        Task<AwaitingApprovalViewModel> GetAwaitingApprovalOrders(string searchTerm = "", int page = 1, int pageSize = 10);
        Task<AwaitingApprovalViewModel> GetAwaitingApprovalOrdersByUser(int userId, string searchTerm = "", int page = 1, int pageSize = 10);

        Task<KosePaymentsViewModel> GetOrphanedPaymentsAsync(int page, int pageSize);

        Task<PaymentsSummaryTotalsViewModel> PaymentsSummary(string searchTerm = "", int page = 1, int pageSize = 10);

        Task<KosePaymentsViewModel> GetAllPaymentsAsync(int? dealerId, string searchTerm = "", int page = 1, int pageSize = 10);
        Task<KosePaymentsViewModel> GetAllPaymentsAsync(int userId, string searchTerm = "", int page = 1, int pageSize = 10);

        Task<List<Device>> GetAllDevicesAsync();

        Task<AllAccountsViewModel> GetAllAccountsAsync(int? dealerId, string searchTerm = "", int page = 1, int pageSize = 10);
        Task<List<AwaitingApprovalDto>> GetAllOrdersAsync();
        Task<List<Dealer>> GetAllDealersAsync();
        Task<CustomerDetails> GetCustomerDetailsByOrderIdAsync(long orderId);

        Task<int> ApproveOrderAsync(long orderId);

        Task<int> RejectOrderAsync(long orderId);

        Task<StatusReportViewModel> GetStatusReportByDealer(int? accountId, int? deviceGroupId, int page = 1, int pageSize = 10, string searchTerm = "");
        Task AddCustomerNoteAsync(int userId, long orderId, string customerNote);

        Task<List<CustomerNote>> GetNotesByOrderIdAsync(long orderId);

        Task<AwaitingApprovalViewModel> GetMissingMpesaOrders(string searchTerm = "", int page = 1, int pageSize = 10);

        Task<AwaitingApprovalViewModel> GetAllNeverPaidOrdersAsync(string searchTerm = "", int page = 1, int pageSize = 10);

        Task<DashboardTotals> GetDashboardTotalsAsync(int dealer = 0);

        Task<List<CustomerDetails>> GetRecentCustomersAsync(int dealerId = 0);

        Task<List<TransactionHistory>> GetTransactionHistoryAsync(int dealerId = 0);

        Task CreateRestructuredAsync(RestructuredRecord record);
        Task<RestructuredViewModel> GetAllRestructured(string searchTerm, int page = 1, int pageSize = 10);
        Task<List<RestructuredRecord>> GetAllRestructuredForAccount(long accountId);

        Task<CustomerDetails?> GetCustomerDetailsByFirstMpesaCodeAsync(string? firstMPesaCode);

        Task<CustomerDetails?> GetCustomerDetailsByAccountIdAsync(int accountId);

        Task<StatusReportViewModel> CallQualifyingFunc(bool isInArrears, bool notPaid90, bool assigned, int? accountId, int? deviceGroupId, int page, int pageSize, string searchTerm);
        Task<KosePaymentsViewModel> GetAssignedPaymentsAsync(string searchTerm, int page, int pageSize);
        Task CreateAssignedPaymentsAsync(string orphanedNo, string mpesaCode, string accountNo);

        Task<List<RestructuredRecord>> GetAllRestructuredNoCalculation();

        Task<CustomerDetails?> GetOrderByOrderIdAsync(long orderId);

        Task<KosePaymentsViewModel> GetAllPaymentAccountsByUserIdAsync(int userId, string searchTerm = "", int page = 1, int pageSize = 10);
    }
}