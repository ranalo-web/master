using Ranalo.Calculator.Logic.Contract;
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
        private readonly IDevicesRepository _devicesRepository;
        public ApplicationReportService(IApplicationReportRepository applicationReportRepository, 
                                        IRepository repository, 
                                        IContractCalculatorService calculatorService,
                                        IDevicesRepository devicesRepository)
        {
            _applicationReportRepository = applicationReportRepository;
            _repository = repository;
            _calculatorService = calculatorService;
            _devicesRepository = devicesRepository;
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
            CustomerDetails? customerDetails = null;

            customerDetails = await _applicationReportRepository.GetCustomerDetails(orderId);
            if(customerDetails == null)
            {
                customerDetails = await _applicationReportRepository.GetCustomerDetailsByAccountId((int)orderId);
            }

            var identityImages = await _applicationReportRepository.GetIdentityImagesForOrder(customerDetails.OrderID);

            customerDetails?.IdentityImages?.AddRange(identityImages.ToList());

            //Populate Order device details
            customerDetails.Product = await _applicationReportRepository.GetProductDetailsForOrder(customerDetails.Id);

            customerDetails.NextOfKin = await _applicationReportRepository.GetNextOfKinForOrder(customerDetails.OrderID);
            //Now lets get AccountId by Mpesa
            var customerAccount = await _applicationReportRepository.GetCustomerAccountByMpesa(customerDetails.MpesaDepositRef);
            if(!string.IsNullOrEmpty(customerAccount))
            {
                customerDetails.Summary = await _applicationReportRepository.GetPaymentSummaryForAccountId(customerAccount);
            }

            //Lets get payments
            var payments = await _applicationReportRepository.GetPaymentsForAccount(customerAccount);
            if(payments != null)
            {
                customerDetails.Payments = payments.Payments!;
            }

            var device = await _devicesRepository.GetDeviceByAccountId(Convert.ToInt64(customerDetails?.Payments?.FirstOrDefault()?.AccountNo));
            if (device != null)
            {
                customerDetails.DeviceDetails = device;
            }

            return customerDetails;
        }

        public async Task<AwaitingApprovalViewModel> GetAllNeverPaidOrdersAsync(string searchTerm = "", int page = 1, int pageSize = 10)
        {
            return await _applicationReportRepository.GetAllNeverPaidOrdersAsync(searchTerm, page, pageSize);
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

        public async Task<StatusReportViewModel> GetStatusReportByDealer(int? accountId, int? deviceGroupId, int page, int pageSize, string searchTerm)
        {
            deviceGroupId ??= 0;
            var result = await _applicationReportRepository.GetPaymentSummaryAsync(accountId, deviceGroupId.Value, page, pageSize, searchTerm);

            List<MobileStatusReport> mobileStatusReports = await SetMobileStatusRecords(accountId, deviceGroupId, page, pageSize, searchTerm, result);

            return new StatusReportViewModel()
            {
                CurrentPage = result.CurrentPage,
                SearchTerm = searchTerm,
                StatusReports = mobileStatusReports,
                TotalPages = result.TotalPages,
                TotalRecords = result.TotalRecords
            };

        }

        private async Task<List<MobileStatusReport>> SetMobileStatusRecords(int? accountId, int? deviceGroupId, int page, int pageSize, string searchTerm, PaymentsViewModel result)
        {
            var mobileStatusReports = new List<MobileStatusReport>();
            //Get payment Summary
            List<PaymentSummary> paymentSummary = new List<PaymentSummary>();

            paymentSummary = result.Payments;

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
                    if (statusRow.Arrears < 0)
                    {
                        var daysLeft = _calculatorService.CalculateNoDaysUnit((DateTime)statusRow.FirstPaymentDate);
                        statusRow.RestructuredAmnt = Math.Round(_calculatorService.CalculateRestructured(statusRow.TotalDue, (int)daysLeft), 2);
                    }

                    mobileStatusReports.Add(statusRow);
                }
            }

            return mobileStatusReports;
        }

        public async Task<StatusReportViewModel> CallQualifyingFunc(int? accountId, int? deviceGroupId, int page, int pageSize, string searchTerm)
        {
            deviceGroupId ??= 0;

            var result = await GetQualifyingPageAsync(
                pageNumber: page,
                pageSize: pageSize,
                fetchPageAsync: async (pageNumber, pageSize) =>
                {
                    // Fetch *this page* directly from repo
                    var result = await _applicationReportRepository.GetPaymentSummaryAsync(
                        accountId,
                        deviceGroupId.Value,
                        pageNumber,
                        pageSize,
                        searchTerm
                    );

                    // Apply your business transformation (IsInArrears calc)
                    return await SetMobileStatusRecords(accountId, deviceGroupId, pageNumber, pageSize, searchTerm, result);
                }
            );
            return new StatusReportViewModel()
            {
                CurrentPage = page,
                SearchTerm = searchTerm,
                StatusReports = result.page,
                TotalPages = (result.total / pageSize),
                TotalRecords = result.total
            };
        }

        public async Task<(int total, List<MobileStatusReport> page)> GetQualifyingPageAsync(
            int pageNumber, int pageSize,
            Func<int, int, Task<List<MobileStatusReport>>> fetchPageAsync)
        {
            var qualifying = new List<MobileStatusReport>();
            int total = 0;

            await foreach (var record in GetAllQualifyingRecordsAsync(fetchPageAsync))
            {
                total++;
                qualifying.Add(record);
            }

            var page = qualifying
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return (total, page);
        }


        public async IAsyncEnumerable<MobileStatusReport> GetAllQualifyingRecordsAsync(
        Func<int, int, Task<List<MobileStatusReport>>> fetchPageAsync,
        int dbPageSize = 100)
        {
            int pageNumber = 1;

            while (true)
            {
                var page = await fetchPageAsync(pageNumber, dbPageSize);
                if (page == null || page.Count == 0)
                    yield break;

                foreach (var record in page)
                {
                    if (record.Arrears < 0) // arrears logic in C#
                        yield return record;
                }

                pageNumber++;
            }
        }
        public async Task<AllAccountsViewModel> GetAllAccountsAsync(int? dealerId, string searchTerm = "", int page = 1, int pageSize = 10)
        {
            var allAccounts = await _applicationReportRepository.GetAllAccountsByUserAsync(dealerId, searchTerm, page, pageSize);
            if (allAccounts.Accounts != null) 
            {
                foreach (var account in allAccounts.Accounts)
                {
                    account.Arrears = _calculatorService.CalculateArears(account.TotalAmount, account.TotalPaid, (DateTime)DateHelper.ParseCustomDate(account.FirstPaidDate));
                }
            }

            return allAccounts;
        }

        private async Task<string> GetFirstNameByMpesa(string? firstMPesaCode)
        {
            if (string.IsNullOrEmpty(firstMPesaCode)) return "";

            var customerDetails = await _applicationReportRepository.GetCustomerDetailsByFirstMpesaCode(firstMPesaCode);

            if (customerDetails == null) return "";
            return customerDetails.FirstName;
        }

        public async Task<CustomerDetails?> GetCustomerDetailsByFirstMpesaCodeAsync(string? firstMPesaCode)
        {
            if (string.IsNullOrEmpty(firstMPesaCode)) return null;

            var customerDetails = await _applicationReportRepository.GetCustomerDetailsByFirstMpesaCode(firstMPesaCode);

            return customerDetails;
        }

        public async Task<CustomerDetails?> GetCustomerDetailsByAccountIdAsync(int accountId)
        {
            return await _applicationReportRepository.GetCustomerDetailsByAccountId(accountId);
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

        #region Dashboard
        public async Task<DashboardTotals> GetDashboardTotalsAsync(int dealer = 0)
        {
            return await _applicationReportRepository.GetDashboardTotals(dealer);
        }

        public async Task<List<CustomerDetails>> GetRecentCustomersAsync(int dealerId = 0)
        {
            return await _applicationReportRepository.GetRecentCustomers(dealerId);
        }

        public async Task<List<TransactionHistory>> GetTransactionHistoryAsync(int dealerId = 0)
        {
            return await _applicationReportRepository.GetTransactionHistory(dealerId);
        }
        #endregion

        #region Restructured

        public async Task CreateRestructuredAsync(RestructuredRecord record)
        {
            await _applicationReportRepository.InsertRestructured(record);
        }

        public async Task<List<RestructuredRecord>> GetAllRestructured()
        {
            return await _applicationReportRepository.GetAllRestructured();
        }

        public async Task<List<RestructuredRecord>> GetAllRestructuredForAccount(long accountId)
        {
            return await _applicationReportRepository.GetAllRestructuredForAccount(accountId);
        }

        Task<CustomerDetails?> IApplicationReportService.GetCustomerDetailsByFirstMpesaCodeAsync(string? firstMPesaCode)
        {
            return GetCustomerDetailsByFirstMpesaCodeAsync(firstMPesaCode);
        }

        #endregion

    }
}
