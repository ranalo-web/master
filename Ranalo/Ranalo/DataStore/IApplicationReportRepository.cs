using Ranalo.DataStore.DataModels;
using Ranalo.Models;
using Ranalo.Woocommece.Api.Models;
using ImagesMetadata = Ranalo.Models.ImagesMetadata;

namespace Ranalo.DataStore
{
    public interface IApplicationReportRepository
    {
        #region Payments
        // fromDate/toDateExclusive scope the query to a period (Week/Month/
        // YTD/Last 12 Months on the All Payments and Payment Summary pages) --
        // null means no date filter, i.e. all time.
        Task<KosePaymentsViewModel> GetAllPaymentsAsync(int? dealerId, string searchTerm = "", int page = 1, int pageSize = 10, DateTime? fromDate = null, DateTime? toDateExclusive = null, int? agentUserId = null);
        Task<PaymentsSummaryTotalsViewModel> GetPaymentsSummaryAsync(string searchTerm = "", int page = 1, int pageSize = 10, DateTime? fromDate = null, DateTime? toDateExclusive = null);
        Task<KosePaymentsViewModel> GetAllPaymentsByDealerIdAsync(int dealerId, string searchTerm = "", int page = 1, int pageSize = 10, DateTime? fromDate = null, DateTime? toDateExclusive = null);

        Task<PaymentsViewModel> GetPaymentSummaryAsync(int? accountId, int deviceGroupId = 0, int page = 1, int pageSize = 10, string searchTerm = "", int? agentUserId = null);
        #endregion

        Task<AwaitingApprovalViewModel> GetAllWaitingApprovalAsync(string searchTerm = "", int page = 1, int pageSize = 10);
        Task<AwaitingApprovalViewModel> GetAllOrdersByUserAsync(int dealerId, string searchTerm, int page, int pageSize, int? agentUserId = null);

        Task<KosePaymentsViewModel> GetOrphanedPaymentsAsync(int page, int pageSize, string searchTerm = "");

        Task<IEnumerable<AwaitingApprovalDto>> GetAllOrdersAsync();
        Task<IEnumerable<Dealer>> GetAllDealersAsync();

        Task<IEnumerable<Device>> GetAllDevicesAsync();
        Task<CustomerDetails?> GetCustomerDetails(long orderId);
        Task<CustomerDetails?> GetCustomerDetailsByFirstMpesaCode(string firstMpesaCode);
        Task<int> RejectOrder(long orderId);

        Task<int> ApproveOrder(long orderId);
        Task<IEnumerable<ImagesMetadata>> GetIdentityImagesForOrder(long orderId);

        Task<AccountSummary?> GetPaymentSummaryForAccountId(string accountNo);
        Task<List<AccountSummary>>GetPaymentSummariesForAccounts(List<long> accountIds);
        Task<string?> GetCustomerAccountByMpesa(string mpesaDepositRef);
        Task<AccountSummary?> GetAccountSummary(string customerAccount);
        
        Task CreateCustomerNote(CustomerNote newNote);
        Task<List<CustomerNote>> GetNotesByOrderId(long orderId);
        Task<WooOrderProduct?> GetProductDetailsForOrder(long orderId);
        Task<Contact?> GetNextOfKinForOrder(long orderId, bool isPrimary);

        Task<AwaitingApprovalViewModel> GetAllMissingMpesaAsync(string searchTerm = "", int page = 1, int pageSize = 10);
        Task<KosePaymentsViewModel> GetPaymentsForAccount(string? customerAccount, int page = 1, int pageSize = 10);

        Task<AwaitingApprovalViewModel> GetAllNeverPaidOrdersAsync(string searchTerm = "", int page = 1, int pageSize = 10);

        //Dashboard
        Task<DashboardTotals> GetDashboardTotals(int dealerId = 0);

        Task<List<CustomerDetails>> GetRecentCustomers(int dealerId = 0);

        Task<List<TransactionHistory>> GetTransactionHistory(int dealerId = 0);

        Task<AllAccountsViewModel> GetAllAccountsByUserAsync(int? dealerId, string searchTerm = "", int page = 1, int pageSize = 10, int? agentUserId = null);

        Task<bool> IsAccountAssignedToAgentAsync(long accountId, int agentUserId);

        Task<CustomerDetails?> GetCustomerDetailsByAccountId(int accountId);

        #region Restructured
        Task InsertRestructured(RestructuredRecord restructuringRecord);

        Task<RestructuredViewModel> GetAllRestructured(string searchTerm, int page = 1, int pageSize = 10);

        Task<List<RestructuredRecord>> GetAllRestructuredForAccount(long accountId);
        Task<decimal> GetPaymentTotalAfterDate(DateTime agreedDate, long accountId);

        Task<(decimal?, DateTime)> GetPaymentTotalAfterDateAndFirstPaymentDate(DateTime agreedDate, long accountId);
        #endregion

        #region ReminderMessages
        Task<IEnumerable<AccountSummary>?> GetCustomersForReminderLockFullyPaid();
        Task<KosePaymentsViewModel> GetAssignedPaymentsAsync(string searchTerm, int page, int pageSize);
        Task CreateAssignedPaymentsAsync(string orphanedNo, string mpesaCode, string accountNo);
        Task<List<RestructuredRecord>> GetAllRestructuredFlat();
        #endregion
    }
}