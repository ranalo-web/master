using Ranalo.Calculator.Logic.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ranalo.Calculator.Logic.Contract
{
    public class ContractCalculatorService : IContractCalculatorService
    {
        private const int termInMonths = 12;
        /// <summary>
        /// Calculates all contract financial + date-related values
        /// </summary>
        public ContractFinancialInfo Calculate(
            decimal basePrice,             // b_price_numeric
            decimal dailyAmount,
            decimal weeklyAmount,
            decimal monthlyAmount,
            int minimumDays,
            DateTime firstPaymentDate,
            int termInMonths = 12,
            DateTime? currentDate = null)
        {
            DateTime now = currentDate ?? DateTime.Now;

            // ✅ 1. Deposit = (BasePrice + 5000) * 0.235
            decimal deposit = Math.Round((basePrice + 5000m) * 0.235m, 2);

            // ✅ 2. Total Due = Deposit + (Daily * MinDays) + (Weekly * MinDays/7) + (Monthly * MinDays/30)
            decimal totalDue =
                deposit
                + (dailyAmount * minimumDays)
                + (weeklyAmount * (minimumDays / 7.0m))
                + (monthlyAmount * (minimumDays / 30.0m));

            totalDue = Math.Round(totalDue, 2);

            // ✅ 3. Contract Dates
            DateTime firstPayDate = firstPaymentDate;

            int noDaysLifetime = (now - firstPaymentDate).Days;
            double noDaysUnits = (now - firstPaymentDate).TotalDays;

            DateTime contractEndDate = firstPaymentDate.AddDays(termInMonths * 30);
            int daysContractEnd = (contractEndDate - firstPaymentDate).Days;

            return new ContractFinancialInfo
            {
                Deposit = deposit,
                TotalDue = totalDue,
                FirstPayDate = firstPayDate,
                NoDaysLifetime = noDaysLifetime,
                NoDaysUnits = noDaysUnits,
                ContractEndDate = contractEndDate,
                DaysContractEnd = daysContractEnd
            };
        }

        public decimal CalculateDeposit(decimal totalAmount)
        {
            decimal convertedPrice = Math.Round(totalAmount, 2); // 12345.68

            // formular (b_price_numeric + 5000) * 0.235
            decimal deposit = Math.Round((convertedPrice + 5000m) * 0.235m, 2); // Rounded to 2 dp

            return deposit;
        }

        public decimal CalculateDailyRate(decimal totalAmount)
        {
            return Math.Round((0.0066733m * totalAmount) + 8.1015m, 2);
        }

        public decimal CalculateWeekleyRate(decimal dailyRate)
        {
            return dailyRate * 7;
        }

        public decimal CalculateOutstandingAmount(decimal totalAmount, decimal totalPaid)
        {
            decimal deposit = CalculateDeposit(totalAmount);
            decimal daily = CalculateDailyRate(totalAmount);
            decimal weekly = CalculateWeekleyRate(0);
            decimal monthly = CalculateMonthlyRate(0);

            // Daily * 30 days * term in months
            decimal dailyTotal = daily * 30 * termInMonths;

            // Weekly * (30/7) weeks per month * term in months
            decimal weeklyTotal = weekly * (30m / 7m) * termInMonths;

            // Monthly * term in months
            decimal monthlyTotal = monthly * termInMonths;

            // Total contract value
            decimal totalDue = deposit + dailyTotal + weeklyTotal + monthlyTotal;

            // Outstanding = total contract value - total already paid
            return Math.Round(totalDue - totalPaid, 2);
        }

        public bool HasNotPaidInLast7Days(DateTime? lastPaymentDate)
        {
            if (!lastPaymentDate.HasValue)
            {
                // No payment date means they have not paid at all → you can decide if that's true or false
                return true; // Or false, depending on your business logic
            }

            DateTime today = DateTime.UtcNow.Date;
            DateTime lastPayment = lastPaymentDate.Value.Date;

            // If last payment was more than 7 days ago
            return lastPayment.AddDays(7) < today;
        }

        public decimal CalculateMonthlyRate(decimal dailyRate)
        {
            return dailyRate * 30;
        }

        public int CalculateLagDays(DateTime firstPaymentDate, string enrolleOnDate)
        {
            var parsedDate = DateHelper.ParseCustomDate(enrolleOnDate);
            if (parsedDate == null)
            {
                return 0;
            }

            TimeSpan difference = (TimeSpan)(firstPaymentDate - parsedDate);

            return Math.Abs(difference.Days);
        }

        public decimal CalculateArears(decimal totalAmount, decimal totalPaid, DateTime firstPaymentDate)
        {
            var totalDue = CalculateTotalDue(totalAmount, firstPaymentDate);
            return totalPaid - totalDue;
        }

        public int CalculateMinimumDays(double? daysContractEnd, double noDaysUnits)
        {
            if (daysContractEnd == null) return (int)noDaysUnits;

            if (daysContractEnd < noDaysUnits) return (int)daysContractEnd;

            return (int)noDaysUnits;
        }

        public double CalculateDaysContractEnd(DateTime firstPaymentDate)
        {
            // Contract ends after termInMonths * 30 days
            DateTime contractEndDate = firstPaymentDate.AddDays(termInMonths * 30);

            // Difference between NOW and contract end date
            return (contractEndDate - DateTime.UtcNow).TotalDays; // could be negative if already expired
        }

        public decimal CalculateTotalDue(decimal totalAmount, DateTime firstPaymentDate)
        {

            var deposit = CalculateDeposit(totalAmount);
            var dailyAmount = CalculateDailyRate(totalAmount);
            var weeklyAmount = CalculateWeekleyRate(0);
            var monthlyAmount = CalculateMonthlyRate(0);
            var contractEndDays = CalculateDaysContractEnd(firstPaymentDate);
            var numberOfDaysUnits = CalculateNoDaysUnit(firstPaymentDate);
            var minimumDays = CalculateMinimumDays(contractEndDays, numberOfDaysUnits);

            decimal totalDue =
               deposit
               + (dailyAmount * minimumDays)
               + (weeklyAmount * (minimumDays / 7.0m))
               + (monthlyAmount * (minimumDays / 30.0m));

            totalDue = Math.Round(totalDue, 2);

            return totalDue;

        }

        public decimal CalculateTotalCost(decimal dailyRate, decimal deposit)
        {
            if (dailyRate <= 0)
                return 0;
            return (dailyRate * 30 * termInMonths) + deposit;
        }

        public decimal CalculateTotalLoan(decimal dailyRate)
        {
            if (dailyRate <= 0)
                return 0;
            return (dailyRate * 30 * termInMonths);
        }

        public decimal CalculateRestructured(decimal arrears, int remainingDays)
        {
            if (remainingDays <= 0)
                return 0;
            return arrears / remainingDays;
        }
        public double CalculateNoDaysUnit(DateTime firstPaymentDate)
        {
            DateTime now = DateTime.Now;
            double noDaysUnits = (now - firstPaymentDate).TotalDays;

            return noDaysUnits;
        }

        public DateTime ContractEndDate(DateTime firstPaymentDate)
        {
            return firstPaymentDate.AddDays(termInMonths * 30);
        }
    }

    public class ContractFinancialInfo
    {
        // Financial fields
        public decimal Deposit { get; set; }
        public decimal TotalDue { get; set; }

        // Date fields
        public DateTime FirstPayDate { get; set; }
        public int NoDaysLifetime { get; set; }
        public double NoDaysUnits { get; set; }
        public DateTime ContractEndDate { get; set; }
        public int DaysContractEnd { get; set; }
    }
}
