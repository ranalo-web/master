
namespace Ranalo.Services
{
    public interface IContractCalculatorService
    {
        ContractFinancialInfo Calculate(decimal basePrice, decimal dailyAmount, decimal weeklyAmount, decimal monthlyAmount, int minimumDays, DateTime firstPaymentDate, int termInMonths = 12, DateTime? currentDate = null);

        decimal CalculateDeposit(decimal totalAmount);
        decimal CalculateTotalDue(decimal totalPaid, DateTime firstPaymentDate);
        decimal CalculateDailyRate(decimal totalAmount);
        decimal CalculateMonthlyRate(decimal dailyRate);
        decimal CalculateWeekleyRate(decimal dailyRate);

        decimal CalculateArears(decimal totalAmount, decimal totalPaid, DateTime firstPaymentDate);
        int CalculateLagDays(DateTime firstPaymentDate, string enrolleOnDate);

        bool HasNotPaidInLast7Days(DateTime? lastPaymentDate);

        decimal CalculateOutstandingAmount(decimal totalAmount, decimal totalPaid);
        double CalculateDaysContractEnd(DateTime firstPaymentDate);
    }
}