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
        decimal CalculateTotalDue(decimal dailyRate, decimal deposit, DateTime firstPaymentDate);
        decimal CalculateDailyRate(decimal totalAmount);
        decimal CalculateMonthlyRate(decimal dailyRate);
        decimal CalculateWeekleyRate(decimal dailyRate);

        decimal CalculateArears(decimal totalPaid, decimal totalDue);
        int CalculateLagDays(DateTime firstPaymentDate, string enrolleOnDate);

        bool HasNotPaidInLast7Days(DateTime? lastPaymentDate);

        decimal CalculateTotalCost(decimal dailyRate, decimal deposit);

        decimal CalculateOutstandingAmount(decimal deposit, decimal daily, decimal weekly, decimal monthly, decimal totalPaid);
        double CalculateDaysContractEnd(DateTime firstPaymentDate);

        decimal CalculateRestructured(decimal arrears, int remainingDays);

        double CalculateNoDaysUnit(DateTime firstPaymentDate);
        decimal CalculateTotalLoan(decimal dailyRate);
        double CalculateNoDaysLeft(DateTime firstPaymentDate);
    }
}
