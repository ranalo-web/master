using Ranalo.DataStore;
using Ranalo.Models;
using System.Drawing.Printing;
using System.Linq;

namespace Ranalo.Services
{
    public class ApplicationReportService : IApplicationReportService
    {
        private readonly IApplicationReportRepository _applicationReportRepository;
        private readonly IRepository _repository;
        private readonly IContractCalculatorService _calculatorService;
        public ApplicationReportService(IApplicationReportRepository applicationReportRepository, IRepository repository, IContractCalculatorService calculatorService  )
        {
            _applicationReportRepository = applicationReportRepository;
            _repository = repository;
            _calculatorService = calculatorService;
        }

        public async Task<AwaitingApprovalViewModel> GetAwaitingApprovalOrders(string searchTerm = "", int page = 1, int pageSize = 10)
        {
            var result = await _applicationReportRepository.GetAllWaitingApprovalAsync(searchTerm, page, pageSize);
            return result;

        }

        public async Task<AwaitingApprovalViewModel> GetMissingMpesaOrders(string searchTerm = "", int page = 1, int pageSize = 10)
        {
            var result = await _applicationReportRepository.GetAllMissingMpesaAsync(searchTerm, page, pageSize);
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


        public async Task<KosePaymentsViewModel> GetOrphanedPaymentsAsync(int page, int pageSize)
        {
            var result = await _applicationReportRepository.GetOrphanedPaymentsAsync(page, pageSize);
            return result;

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
            var allOrphaned = await GetOrphanedPaymentsAsync(1, 10000);

            var orphaned = allOrphaned.Payments?.DistinctBy(r => r.MpesaCode).ToList();
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

            //Populate Order device details
            customerDetails.Product = await _applicationReportRepository.GetProductDetailsForOrder(customerDetails.Id);

            customerDetails.NextOfKin = await _applicationReportRepository.GetNextOfKinForOrder(orderId);
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

        public async Task<IEnumerable<MobileStatusReport>> GetStatusReportByDealer(int? deviceGroupId)
        {

            var mobileStatusReports = new List<MobileStatusReport>();
            //Get payment Summary
            IEnumerable<PaymentSummary> paymentSummary;
            if(deviceGroupId != null)
            {
                paymentSummary = await _applicationReportRepository.GetPaymentSummaryByDeviceGroupAsync((int)deviceGroupId);
            }
            else
            {
                paymentSummary = await _applicationReportRepository.GetPaymentSummaryAsync();
            }

            if (paymentSummary != null) 
            {
                foreach (var payment in paymentSummary)
                {
                    var dailyRate = _calculatorService.CalculateDailyRate(payment.TotalAmount);
                    var statusRow = new MobileStatusReport()
                    {
                        TotalPaid = payment.TotalPaid,
                        Deposit = _calculatorService.CalculateDeposit(payment.TotalAmount),
                        TotalDue = _calculatorService.CalculateTotalDue(payment.TotalAmount, payment.FirstPaidDate),
                        AccountNo = payment.AccountNo,
                        DeviceGroupId = payment.DeviceGroupId,
                        FirstName = await GetFirstNameByMpesa(payment.FirstMPesaCode),
                        ImeiNo = payment.ImeiNo,
                        Arrears = _calculatorService.CalculateArears(payment.TotalAmount, payment.TotalPaid, payment.FirstPaidDate),
                        ArrearsAmt = 0, //Ask Eddie 
                        Comms = 0, //Ask Eddie for Calculation
                        Daily = dailyRate,
                        DateEnrolled = payment.DateEnrolled,
                        DeviceGroup = "",  //Work this out
                        EnrolledOn = DateHelper.ParseCustomDate(payment.EnrolledOn),
                        LagDays = _calculatorService.CalculateLagDays(payment.FirstPaidDate, payment.EnrolledOn),
                        LastConnectedAt = payment.LastConnectedAt,
                        LastPaymentDate = payment.LastPaidDate,
                        LastPaidAmt = payment.LastPaymentAmount,
                        FirstPaidAmt = payment.FirstPaymentAmount,
                        FirstPaymentDate = payment.FirstPaidDate,
                        LiveFlag = "1", //Ask Eddie Not sure what this is
                        Locked = payment.Locked,
                        Make = payment.Make,
                        Model = payment.Model,
                        Monthly = _calculatorService.CalculateMonthlyRate(dailyRate),
                        NotPaying7D = _calculatorService.HasNotPaidInLast7Days(payment.LastPaidDate) ? 1 : 0,
                        RePaymentIntervals = "Daily",
                        SaleWeek = DateTime.Now, //Ask Eddie Whats this????
                        Weekly = _calculatorService.CalculateWeekleyRate(dailyRate),
                        LoanBalance = _calculatorService.CalculateOutstandingAmount(payment.TotalAmount, payment.TotalPaid),
                        TotalLoan = dailyRate * 30 * 12,
                        NumberDaysLifeTime = _calculatorService.CalculateDaysContractEnd(payment.FirstPaidDate),
                        NextLockDate = payment.NextLockDate,
                        Status = payment.Status,
                        LockType = payment.LockType
                    };

                    mobileStatusReports.Add(statusRow);
                }
            }

            return mobileStatusReports;

        }

        private async Task<string> GetFirstNameByMpesa(string? firstMPesaCode)
        {
            if (string.IsNullOrEmpty(firstMPesaCode)) return "";

            var customerDetails = await _applicationReportRepository.GetCustomerDetailsByFirstMpesaCode(firstMPesaCode);

            if (customerDetails == null) return "";
            return customerDetails.FirstName;
        }

        public async Task AddCustomerNoteAsync(int userId, long orderId, string customerNote)
        {
            var newNote = new CustomerNote()
            {
                UserId = userId,
                Created = DateTime.UtcNow,
                Note = customerNote,
                OrderId = orderId,
                Id = Guid.NewGuid()
            };
            await _applicationReportRepository.CreateCustomerNote(newNote);
        }

        public async Task<List<CustomerNote>> GetNotesByOrderIdAsync(long orderId)
        {
            return await _applicationReportRepository.GetNotesByOrderId(orderId);
        }
    }
}
