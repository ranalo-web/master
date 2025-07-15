using Ranalo.DataStore;
using Ranalo.Models;
using System.Linq;

namespace Ranalo.Services
{
    public class ApplicationReportService : IApplicationReportService
    {
        private readonly IApplicationReportRepository _applicationReportRepository;
        private readonly IRepository _repository;
        public ApplicationReportService(IApplicationReportRepository applicationReportRepository, IRepository repository)
        {
            _applicationReportRepository = applicationReportRepository;
            _repository = repository;
        }

        public async Task<AwaitingApprovalViewModel> GetAwaitingApprovalOrders(string searchTerm = "", int page = 1, int pageSize = 10)
        {
            var result = await _applicationReportRepository.GetAllWaitingApprovalAsync(searchTerm, page, pageSize);
            return result;

        }
        public async Task<AwaitingApprovalViewModel> GetAwaitingApprovalOrdersByUser(int userId, string searchTerm, int page, int pageSize)
        {
            var dealerDetails = await _repository.GetDealerByUserIdAsync(userId);

            if (dealerDetails == null)
            {
                return null;
            }

            var result = await _applicationReportRepository.GetAllOrdersByUserAsync(dealerDetails.DealerId, searchTerm, page, pageSize);

            return result;
        }


        public async Task<List<KosePayments>?> GetOrphanedPaymentsAsync()
        {
            var result = await _applicationReportRepository.GetOrphanedPaymentsAsync();
            return result.ToList();

        }
        public async Task<List<AwaitingApprovalDto>> GetAllOrdersAsync()
        {
            var result = await _applicationReportRepository.GetAllOrdersAsync();

            return result.ToList();
        }

        public async Task<KosePaymentsViewModel> GetAllPaymentsAsync(string searchTerm = "", int page = 1, int pageSize = 10)
        {
            var result = await _applicationReportRepository.GetAllPaymentsAsync(searchTerm, page, pageSize);
            return result;

        }
        public async Task<KosePaymentsViewModel> GetAllPaymentsAsync(int userId, string searchTerm = "", int page = 1, int pageSize = 10)
        {
            var dealerDetails = await _repository.GetDealerByUserIdAsync(userId);

            if (dealerDetails == null)
            {
                return null;
            }

            var dealerPayments = await _applicationReportRepository.GetAllPaymentsByDealerIdAsync(dealerDetails.DealerId, searchTerm, page, pageSize);

            return dealerPayments;
        }

        public async Task<IEnumerable<PaymentsSummaryTotals>> PaymentsSummary()
        {
            var payments = await GetAllPaymentsAsync();
            var allOrphaned = await GetOrphanedPaymentsAsync();

            var orphaned = allOrphaned.DistinctBy(r => r.MpesaCode).ToList();
            //.DistinctBy(r => r.MpesaCode).ToList();
            var merged = from p in payments.Payments
                         join o in orphaned on p.MpesaCode equals o.MpesaCode into oo
                         select new { Payment = p, Orphan = oo.FirstOrDefault() };

            //Producing Eddie report
            var pTotals = merged.GroupBy(m => m.Payment.AccountNo)
                .Select(g => new PaymentsSummaryTotals
                {
                    Account = g.Key,
                    TotalPaid = g.Sum(m => m.Payment.AmountValue),
                    First = g.Min(m => m.Payment.PaymentDateValue),
                    Last = g.Max(m => m.Payment.PaymentDateValue),
                    FirstPayment = g.OrderBy(m => m.Payment.PaymentDateValue).First().Payment,
                    LastPayment = g.OrderByDescending(m => m.Payment.PaymentDateValue).First().Payment,
                });

            return pTotals;
        }

        public async Task<List<Dealer>> GetAllDealersAsync()
        {
            var result = await _applicationReportRepository.GetAllDealersAsync();
            return result.ToList();
        }
        public async Task<List<Device>> GetAllDevicesAsync()
        {
            var result = await _applicationReportRepository.GetAllDevicesAsync();
            return result.ToList();
        }
            
        public async Task<CustomerDetails> GetCustomerDetailsByOrderIdAsync(long orderId)
        {
            var customerDetails = await _applicationReportRepository.GetCustomerDetails(orderId);

            var identityImages = await _applicationReportRepository.GetIdentityImagesForOrder(orderId);

            customerDetails?.IdentityImages?.AddRange(identityImages.ToList());

            //Now lets get AccountId by Mpesa
            var customerAccount = await _applicationReportRepository.GetCustomerAccountByMpesa(customerDetails.MpesaDepositRef);
            if(!string.IsNullOrEmpty(customerAccount))
            {
                customerDetails.Summary = await _applicationReportRepository.GetPaymentSummaryForAccountId(customerAccount);
            }
            return customerDetails;
        }

        public async Task<int> RejectOrderAsync(long orderId)
        {
            var client = new WooCommerceClient(
                "https://ranalocredit.com/wp-json/wc/v3",
                "ck_9bf5ade6a031f04b53bd31938d462895db40e00c",
                "cs_b2d5d61f3eae5093d85b7319905eb5942c614f99"
            );

            string result = await client.UpdateOrderStatusAsync(orderId, "rejected");

            return await _applicationReportRepository.RejectOrder(orderId);
        }

        public async Task<int> ApproveOrderAsync(long orderId)
        {
            var client = new WooCommerceClient(
                "https://ranalocredit.com/wp-json/wc/v3",
                "ck_9bf5ade6a031f04b53bd31938d462895db40e00c",
                "cs_b2d5d61f3eae5093d85b7319905eb5942c614f99"
            );

            string result = await client.UpdateOrderStatusAsync(orderId, "approved");

            return await _applicationReportRepository.ApproveOrder(orderId);
        }
    }
}
