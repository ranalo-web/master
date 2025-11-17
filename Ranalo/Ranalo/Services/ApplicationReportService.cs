using Ranalo.Calculator.Logic.Contract;
using Ranalo.DataStore;
using Ranalo.Models;
using System.Drawing.Printing;
using System.Linq;
using Dealer = Ranalo.DataStore.Dealer;

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

            //These are accounts with no orders
            if (customerDetails == null)
            {
                customerDetails = new CustomerDetails();
                
                customerDetails.Summary = await _applicationReportRepository.GetPaymentSummaryForAccountId(orderId.ToString());

                customerDetails.FirstName = customerDetails?.Summary?.FirstName ?? "";

                var accountPayments = await _applicationReportRepository.GetPaymentsForAccount(orderId.ToString());
                if (accountPayments != null)
                {
                    customerDetails.Payments = accountPayments.Payments!;
                }

                var accountDevice = await _devicesRepository.GetDeviceByAccountId(Convert.ToInt64(customerDetails?.Payments?.FirstOrDefault()?.AccountNo));
                if (accountDevice != null)
                {
                    customerDetails.DeviceDetails = accountDevice;
                }

                customerDetails.Summary = await _applicationReportRepository.GetPaymentSummaryForAccountId(orderId.ToString());

                var totalDueAll = _calculatorService.CalculateTotalDue(customerDetails.Summary.Daily, customerDetails.Summary.Weekly, customerDetails.Summary.Monthly, customerDetails.Summary.Deposit, customerDetails.Summary.FirstPaymentDate, customerDetails.Summary.TermsInMonths);

                customerDetails.Arrears = _calculatorService.CalculateArears(customerDetails.Summary.TotalPaid, totalDueAll);
                customerDetails.DaysLeft = _calculatorService.CalculateDaysContractEnd(customerDetails.Summary.FirstPaymentDate, (double)customerDetails.Summary.TermsInMonths);
                customerDetails.TotalDue = totalDueAll;

                return customerDetails;
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

            if(customerDetails.Summary != null)
            {
                var totalDue = _calculatorService.CalculateTotalDue(customerDetails.Summary.Daily, customerDetails.Summary.Weekly, customerDetails.Summary.Monthly, customerDetails.Summary.Deposit, customerDetails.Summary.FirstPaymentDate, customerDetails.Summary.TermsInMonths);

                customerDetails.Arrears = _calculatorService.CalculateArears(customerDetails.Summary.TotalPaid, totalDue);
                customerDetails.DaysLeft = _calculatorService.CalculateDaysContractEnd(customerDetails.Summary.FirstPaymentDate, (double)customerDetails.Summary.TermsInMonths);
                customerDetails.TotalDue = totalDue;
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

            List<MobileStatusReport> mobileStatusReports = await SetMobileStatusRecords(false, false, accountId, deviceGroupId, page, pageSize, searchTerm, result);

            return new StatusReportViewModel()
            {
                CurrentPage = result.CurrentPage,
                SearchTerm = searchTerm,
                StatusReports = mobileStatusReports,
                TotalPages = result.TotalPages,
                TotalRecords = result.TotalRecords
            };

        }

        private async Task<List<MobileStatusReport>> SetMobileStatusRecords(bool isInArrears, bool notPaidIn90, int? accountId, int? deviceGroupId, int page, int pageSize, string searchTerm, PaymentsViewModel result)
        {
            var mobileStatusReports = new List<MobileStatusReport>();
            //Get payment Summary
            List<PaymentSummary> paymentSummary = new List<PaymentSummary>();

            paymentSummary = result.Payments;

            if (paymentSummary != null)
            {
                foreach (var payment in paymentSummary)
                {
                    //var dailyRate = _calculatorService.CalculateDailyRate(payment.TotalAmount);
                    var totalDue = _calculatorService.CalculateTotalDue(payment.Daily, payment.Weekly, payment.Monthly, payment.Deposit, payment.FirstPaidDate, payment.TermsInMonths);
                    var statusRow = new MobileStatusReport()
                    {
                        TotalPaid = payment.TotalPaid,
                        Deposit = payment.Deposit,
                        TotalDue = totalDue,
                        AccountNo = payment.AccountNo,
                        DeviceGroupId = payment.DeviceGroupId,
                        FirstName = payment.CustomerName ?? "",
                        ImeiNo = payment.ImeiNo,
                        Arrears = _calculatorService.CalculateArears(payment.TotalPaid, totalDue),
                        ArrearsAmt = 0, //Ask Eddie 
                        Comms = 0, //Ask Eddie for Calculation
                        Daily = payment.Daily,
                        DateEnrolled = payment.DateEnrolled,
                        DeviceGroup = "",  //Work this out
                        EnrolledOn = DateHelper.ParseCustomDate(payment.EnrolledOn),
                        LagDays = _calculatorService.CalculateLagDays(payment.FirstPaidDate, payment.EnrolledOn),
                        LastConnectedAt = payment.LastConnectedAt,
                        LastPaymentDate = payment.LastPaidDate,
                        LastPaidAmt = payment.LastPaymentAmount,
                        TotalLast24 = payment.Last24hrPaidAmount,
                        FirstPaidAmt = payment.FirstPaymentAmount,
                        FirstPaymentDate = payment.FirstPaidDate,
                        LiveFlag = "1", //Ask Eddie Not sure what this is
                        Locked = payment.Locked,
                        Make = payment.Make,
                        Model = payment.Model,
                        Monthly = payment.Monthly,
                        NotPaying7D = _calculatorService.HasNotPaidInLast7Days(payment.LastPaidDate) ? 1 : 0,
                        RePaymentIntervals = "Daily",
                        SaleWeek = DateTime.Now, //Ask Eddie Whats this????
                        Weekly = payment.Weekly,
                        LoanBalance = _calculatorService.CalculateOutstandingAmount(payment.Deposit, payment.Daily, payment.Weekly, payment.Monthly, payment.TotalPaid, payment.TermsInMonths),
                        TotalLoan = payment.Daily * 30 * 12,
                        NumberDaysLifeTime = _calculatorService.CalculateNoDaysUnit(payment.FirstPaidDate),
                        NextLockDate = payment.NextLockDate,
                        Status = payment.Status,
                        LockType = payment.LockType,
                        NextLockDateIsoFormat = payment.NextLockDateIsoFormat,
                        NotPaying90D = _calculatorService.HasNotPaidInLast90Days(DateHelper.ParseCustomDate(payment.NextLockDateIsoFormat)),

                    };
                    if (statusRow.Arrears < 0)
                    {
                        
                        var daysLeft = _calculatorService.CalculateNoDaysUnit((DateTime)statusRow.FirstPaymentDate);
                        var effectiveDays = (decimal)daysLeft / 1m;
                        var restructuredAmount = ((statusRow.Arrears * -1m) / effectiveDays) * 1.1m;
                        var newRateToPay = restructuredAmount + payment.Daily + payment.Weekly + payment.Monthly;

                        //Here we have silly units calculation
                        var oldUints = (statusRow.Arrears / (payment.Daily + payment.Weekly + payment.Monthly));
                        var unitsLeft = (statusRow.Arrears / newRateToPay);
                        var now = DateTime.UtcNow; // Or DateTime.Now depending on your context
                        var autoLockDatePmt = now.AddSeconds(Convert.ToDouble(oldUints * 60 * 60 * 30));

                        statusRow.NextLockDateIsoFormat = autoLockDatePmt.ToString("dd/MM/yyyy HH:mm:ss");
                        statusRow.DaysRestructured = (int)daysLeft;
                        statusRow.NewDaily = Math.Round(newRateToPay, 2); ;
                        statusRow.RestructuredAmnt = Math.Round(restructuredAmount, 2);
                    }

                    statusRow.NextLockDate = payment.NextLockDateIsoFormat;
                    mobileStatusReports.Add(statusRow);
                }
            }

            //Remove fully paid
            //mobileStatusReports.RemoveAll(x => x.Arrears > 0 && x.LoanBalance < 0);
            //mobileStatusReports.RemoveAll(x => x.Arrears > 0);
            return mobileStatusReports;
        }

        public async Task<StatusReportViewModel> CallQualifyingFunc(bool isInArrears, bool notPaid90, int? accountId, int? deviceGroupId, int page, int pageSize, string searchTerm)
        {
            deviceGroupId ??= 0;

            var result = await GetQualifyingPageAsync(
                pageNumber: page,
                pageSize: pageSize,
                isInArrears,
                notPaid90,
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
                    return await SetMobileStatusRecords(isInArrears, notPaid90, accountId, deviceGroupId, pageNumber, pageSize, searchTerm, result);
                }
            );
            return new StatusReportViewModel()
            {
                CurrentPage = page,
                SearchTerm = searchTerm,
                StatusReports = result.page.DistinctBy(x => x.AccountNo).ToList(),
                TotalPages = (result.total / pageSize),
                TotalRecords = result.total
            };
        }

        public async Task<(int total, List<MobileStatusReport> page)> GetQualifyingPageAsync(
            int pageNumber, int pageSize,
            bool isInArrears, bool notPaid90,Func<int, int, Task<List<MobileStatusReport>>> fetchPageAsync)
        {
            var qualifying = new List<MobileStatusReport>();
            int total = 0;

            await foreach (var record in GetAllQualifyingRecordsAsync(fetchPageAsync, isInArrears, notPaid90))
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
        bool isInArrears,
        bool notPaidIn90,
        int dbPageSize = 1000)
        {
            int pageNumber = 1;

            while (true)
            {
                var page = await fetchPageAsync(pageNumber, dbPageSize);
                if (page == null || page.Count == 0)
                    yield break;

                if (isInArrears)
                {
                    foreach (var record in page)
                    {
                        if (record.Arrears < 0) // arrears logic in C#
                            yield return record;
                    }
                }
                if (notPaidIn90)
                {
                    foreach (var record in page)
                    {
                        if (record.NotPaying90D && record.Arrears < 0) // arrears logic in C#
                            yield return record;
                    }
                }

                foreach (var record in page)
                {
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
                    var totalDue = _calculatorService.CalculateTotalDue(account.Daily, account.Weekly, account.Monthly, account.Deposit, (DateTime)DateHelper.ParseCustomDate(account.FirstPaidDate), account.TermsInMonths);
                    account.Arrears = _calculatorService.CalculateArears(account.TotalPaid, totalDue);
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

        public async Task<RestructuredViewModel> GetAllRestructured(string searchTerm, int page = 1, int pageSize = 10)
        {
            pageSize = 1000; // We nned to use qualifying records logic here
            var allRestructuredRecords = await _applicationReportRepository.GetAllRestructured(searchTerm, page, pageSize);

            foreach (var record in allRestructuredRecords.Records)
            {
                var paymentSummary = await _applicationReportRepository.GetPaymentSummaryForAccountId(record.AccountNo.ToString());

                var daysRestructured = (DateTime.UtcNow.Date - (DateTime)record.DateAgreed.Date).TotalDays;

                var due = _calculatorService.CalculateTotalDue(record.AmountRes, 0, 0, 0, record.DateAgreed, paymentSummary.TermsInMonths);
                var arrears = (record.TotalPaidR - due);
                record.Arrears = paymentSummary.Arrears;
                record.Daily = paymentSummary.Daily;
                record.LastConnectedAt = paymentSummary.LastConnectedAt;
                record.LastResPaymentDate = paymentSummary.LastPaymentDate;
                record.LastPaidAmount = paymentSummary.LastPaidAmount;
                record.NextLockDate = paymentSummary.NextLockDate;

                if (due <= 0)
                {
                    record.AutoLockDatePmtR = DateTime.UtcNow;
                    record.TotalDueR = (decimal)due;
                    record.ArrearsR = (decimal)arrears;
                    record.DaysRestructured = (int)daysRestructured;
                }
                else
                {
                    var unitsLeft = (arrears / record.AmountRes);
                    var now = DateTime.UtcNow; // Or DateTime.Now depending on your context
                    var autoLockDatePmt = now.AddSeconds(Convert.ToDouble(unitsLeft * 60 * 60 * 24)); // adds 4.5 days (4 days + 12 hours)

                    record.AutoLockDatePmtR = autoLockDatePmt;
                    record.TotalDueR = (decimal)due;
                    record.ArrearsR = arrears;
                    record.DaysRestructured = (int)daysRestructured;
                }
            }

            return allRestructuredRecords;
        }


        public async Task<List<RestructuredRecord>> GetAllRestructuredNoCalculation()
        {
            return await _applicationReportRepository.GetAllRestructuredFlat();
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

        #region Payments Reminders
        public async Task SendMessagesToUpToDate()
        {
            var allQualifying = await _applicationReportRepository.GetCustomersForReminderLockFullyPaid();
        }

        public async Task<KosePaymentsViewModel> GetAssignedPaymentsAsync(string searchTerm, int page, int pageSize)
        {
            var result = await _applicationReportRepository.GetAssignedPaymentsAsync(searchTerm, page, pageSize);
            return result;
        }

        public async Task CreateAssignedPaymentsAsync(string orphanedNo, string mpesaCode, string accountNo)
        {
            await _applicationReportRepository.CreateAssignedPaymentsAsync(orphanedNo, mpesaCode, accountNo);
        }
        #endregion

    }
}
