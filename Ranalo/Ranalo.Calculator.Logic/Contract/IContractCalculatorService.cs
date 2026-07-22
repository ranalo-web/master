using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ranalo.Calculator.Logic.Contract
{
    public interface IContractCalculatorService
    {
        ContractFinancialInfo Calculate(decimal basePrice, decimal dailyAmount, decimal weeklyAmount, decimal monthlyAmount, int minimumDays, DateTime firstPaymentDate, int termInMonths = 12, DateTime? currentDate = null);

        decimal CalculateDeposit(decimal totalAmount);
        decimal CalculateTotalDue(decimal dailyRate, decimal weekly, decimal monthly, decimal deposit, DateTime firstPaymentDate, decimal termInMonths);
        decimal CalculateDailyRate(decimal totalAmount);
        decimal CalculateDailyRate365(decimal totalAmount, decimal deposit);
        decimal CalculateMonthlyRate(decimal dailyRate);
        decimal CalculateWeekleyRate(decimal dailyRate);

        decimal CalculateSalesDeposit(decimal totalAmount, decimal dailyRate);

        DateTime ContractEndDate(DateTime firstPaymentDate, double termInMonths);

        decimal CalculateArears(decimal totalPaid, decimal totalDue);
        int CalculateLagDays(DateTime firstPaymentDate, string enrolleOnDate);

        bool HasNotPaidInLast7Days(DateTime? lastPaymentDate);

        bool HasNotPaidInLast90Days(DateTime? lastPaymentDate);

        decimal CalculateTotalCost(decimal dailyRate, decimal deposit, decimal termInMonths);

        decimal CalculateOutstandingAmount(decimal deposit, decimal daily, decimal weekly, decimal monthly, decimal totalPaid, decimal termInMonths);
        double CalculateDaysContractEnd(DateTime firstPaymentDate, double termInMonths);

        decimal CalculateRestructured(decimal arrears, int remainingDays);

        double CalculateNoDaysUnit(DateTime firstPaymentDate);
        decimal CalculateTotalLoan(decimal dailyRate, decimal termInMonths);
        double CalculateNoDaysLeft(DateTime firstPaymentDate, double termInMonths);
    }
}
