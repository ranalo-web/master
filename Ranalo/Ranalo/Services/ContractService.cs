using MySqlX.XDevAPI.Common;
using Ranalo.Calculator.Logic.Contract;
using Ranalo.Calculator.Logic.Models;
using Ranalo.DataStore;
using Ranalo.Models;
using Ranalo.Woocommece.Api.Models;
using System.Diagnostics.Contracts;
using MobileStatusReport = Ranalo.Models.MobileStatusReport;

namespace Ranalo.Services
{
    public class ContractService : IContractService
    {
        private readonly IContractRepository _contractRepository;
        private readonly IContractCalculatorService _calculatorService;
        public ContractService(IContractRepository contractRepository, IContractCalculatorService calculatorService)
        {
            _contractRepository = contractRepository;
            _calculatorService = calculatorService;
        }
        public async Task<int> AddContractAsync(ContractInfo contract)
        {
            return await _contractRepository.AddContractAsync(contract);
        }
        public async Task<int> DeleteContractAsync(int contractId)
        {
            return await _contractRepository.DeleteContractAsync(contractId);
        }
        public async Task<ContractViewModel> GetAllContractsAsync(int page, int pageSize, string searchParam = "")
        {
            return await _contractRepository.GetAllContractsAsync(page, pageSize, searchParam);
        }

       public async Task<ContractInfo?> GetContractByDeviceIdAsync(int deviceId)
        {
            return await _contractRepository.GetContractByDeviceIdAsync(deviceId);
        }
        public async Task<ContractInfo?> GetContractByIdAsync(int contractId)
        {
            return await _contractRepository.GetContractByIdAsync(contractId);
        }
        public async Task<int> UpdateContractAsync(ContractInfo contract)
        {
            return await _contractRepository.UpdateContractAsync(contract);
        }

        public async Task<int> CreateRecoveredAccountAsync(ContractCreateDto newContract)
        {
            var constructContract = await CreateContractSingle(newContract);

            return await _contractRepository.CreateRecoveredAccount(constructContract);
        }

        public async Task<ContractInfo> CreateContractSingle(ContractCreateDto order)
        {
            var dailyRate = _calculatorService.CalculateDailyRate(order.TotalAmount);
            var deposit = _calculatorService.CalculateDeposit(order.TotalAmount);
            var contract = new ContractInfo()
            {
                ID = int.Parse(order.AccountNo),
                Deposit = deposit,
                Daily = dailyRate,
                Weekly = 0,
                Monthly = 0,
                RePaymentIntervals = "Daily",
                TotalCost = _calculatorService.CalculateTotalCost(dailyRate, deposit, 12),
                TotalLoan = _calculatorService.CalculateTotalLoan(dailyRate, 12),
                FirstName = order.FirstName,
                TotalAmount = order.TotalAmount
            };

            return contract;
        }

        public async Task AssignContractToCollector(int contractId, int collectorUserId)
        {
            await _contractRepository.AssignContractToCollector(contractId, collectorUserId);
        }

        public async Task AssignAccountToAgent(int contractId, int agentId)
        {
            await _contractRepository.AssignAccountToAgentAsync(contractId, agentId);
        }

        public async Task<StatusReportViewModel> GetCollectorsContractSummaryAsync(int userId, int? accountId, int deviceGroupId = 0, int page = 1, int pageSize = 10, string searchTerm = "")
        {
            var result = await _contractRepository.GetCollectorsContractSummaryAsync(userId, accountId, deviceGroupId, page, pageSize, searchTerm);
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
                        TotalLoan = payment.Daily * 30 * payment.TermsInMonths,
                        NumberDaysLifeTime = _calculatorService.CalculateNoDaysUnit(payment.FirstPaidDate),
                        NextLockDate = payment.NextLockDate,
                        Status = payment.Status,
                        LockType = payment.LockType,
                        NextLockDateIsoFormat = payment.NextLockDateIsoFormat,
                        NotPaying90D = _calculatorService.HasNotPaidInLast90Days(payment.LastPaidDate),

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

            return new StatusReportViewModel()
            {
                CurrentPage = page,
                SearchTerm = searchTerm,
                StatusReports = mobileStatusReports,
                TotalPages = result.TotalPages,
                TotalRecords = result.TotalRecords
            };
        }

        public async Task<ContractViewModel> GetAccountsByDealer(int dealerId, int page, int pageSize, string searchTerm)
        {
            return await _contractRepository.GetAccountsByDealerAsync(dealerId, page, pageSize, searchTerm);
        }

        public async Task<ContractViewModel> GetAssignedAccountsByDealer(int dealerId, int page, int pageSize, string searchTerm)
        {
            return await _contractRepository.GetAssignedAccountsByDealerAsync(dealerId, page, pageSize, searchTerm);
        }
    }
}
