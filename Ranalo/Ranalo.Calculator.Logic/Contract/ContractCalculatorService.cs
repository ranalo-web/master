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

        public decimal CalculateSalesDeposit(decimal totalAmount, decimal dailyRate)
        {
            return totalAmount - (dailyRate * 365);
        }

        public decimal CalculateDailyRate(decimal totalAmount)
        {
            return Math.Round((0.0066733m * totalAmount) + 8.1015m, 2);
        }

        public decimal CalculateWeekleyRate(decimal dailyRate)
        {
            return dailyRate * 7;
        }

        public decimal CalculateOutstandingAmount(decimal deposit, decimal daily, decimal weekly, decimal monthly, decimal totalPaid, decimal termInMonths)
        {
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

        public bool HasNotPaidInLast90Days(DateTime? lastPaymentDate)
        {
            if (!lastPaymentDate.HasValue)
            {
                // No payment date means they have not paid at all → you can decide if that's true or false
                return true; // Or false, depending on your business logic
            }

            DateTime today = DateTime.UtcNow.Date;
            DateTime lastPayment = lastPaymentDate.Value.Date;

            // If last payment was more than 90 days ago
            return lastPayment.AddDays(90) < today;
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

        public decimal CalculateArears(decimal totalPaid, decimal totalDue)
        {
            return totalPaid - totalDue;
        }

        public int CalculateMinimumDays(double? daysContractEnd, double noDaysUnits)
        {
            if (daysContractEnd == null) return (int)noDaysUnits;

            if (daysContractEnd < noDaysUnits) return (int)daysContractEnd;

            return (int)noDaysUnits;
        }

        public double CalculateDaysContractEnd(DateTime firstPaymentDate, double termInMonths)
        {
            // Contract ends after termInMonths * 30 days
            DateTime contractEndDate = firstPaymentDate.AddDays(termInMonths * 30);

            // Difference between NOW and contract end date
            return (contractEndDate - DateTime.UtcNow).TotalDays; // could be negative if already expired
        }

        public decimal CalculateTotalDue(decimal dailyAmount, decimal weekly, decimal monthly, decimal deposit, DateTime firstPaymentDate, decimal termInMonths)
        {

            var now = DateTime.UtcNow;
            DateTime? firstPayDate = firstPaymentDate;
            //firstPayDate = DateTime.SpecifyKind(firstPaymentDate, DateTimeKind.Local).ToUniversalTime();

            double? noDaysLifetime = firstPayDate.HasValue
                ? Math.Round((now - firstPayDate.Value).TotalDays, 7)
                : null;

            double? noDaysUnits = noDaysLifetime;

            // Step 2: Contract End Date
            DateTime? contractEndDate = firstPayDate.HasValue
                ? firstPayDate.Value.AddDays((double)termInMonths * 30)
                : null;

            // Step 3: Days until contract end
            double? daysContractEnd = (firstPayDate.HasValue && contractEndDate.HasValue)
                ? (contractEndDate.Value - firstPayDate.Value).TotalDays
                : null;

            // Step 4: Minimum days (handles nulls gracefully)
            double? minimumDays = daysContractEnd switch
            {
                null => noDaysUnits,
                _ when noDaysUnits is null => daysContractEnd,
                _ => Math.Min(daysContractEnd.Value, noDaysUnits.Value)
            };

            // Step 5: Totals
            var weeklyAmount = weekly;
            var monthlyAmount = monthly;

            decimal? totalDue =
                deposit
                + (dailyAmount * (decimal)(minimumDays ?? 0))
                + (weeklyAmount * (decimal)((minimumDays ?? 0) / 7.0))
                + (monthlyAmount * (decimal)((minimumDays ?? 0) / 30.0));

            decimal? dailyPaymentAll =
                dailyAmount + (weeklyAmount / 7.0m) + (monthlyAmount / 30.0m);

            // Step 6: Return rounded total
            return Math.Round(totalDue ?? 0m, 2);

            //var weeklyAmount = CalculateWeekleyRate(0);
            //var monthlyAmount = CalculateMonthlyRate(0);
            //var contractEndDays = CalculateDaysContractEnd(firstPaymentDate);
            //var numberOfDaysUnits = CalculateNoDaysUnit(firstPaymentDate);
            //var minimumDays = CalculateMinimumDays(contractEndDays, numberOfDaysUnits);

            //var computedMinimumDays = (int)ComputeMinimumDays(firstPaymentDate, contractEndDays);
            //decimal totalDue =
            //   deposit
            //   + (dailyAmount * computedMinimumDays)
            //   + (weeklyAmount * (computedMinimumDays / 7.0m))
            //   + (monthlyAmount * (computedMinimumDays / 30.0m));

            //totalDue = Math.Round(totalDue, 2);

            //return totalDue;

        }

        private double ComputeMinimumDays(DateTime? firstPaymentDate, double? daysContractEnd)
        {
            var today = DateTime.Now;

            // Step 1: Calculate No_Days_Units (equivalent to DATEDIFF * 1.0)
            double? noDaysUnits = null;
            if (firstPaymentDate.HasValue)
            {
                noDaysUnits = (today - firstPaymentDate.Value).TotalDays;
            }

            // Step 2: Compute Minimum_Days using the same CASE logic
            double? minimumDays;

            if (daysContractEnd == null)
            {
                minimumDays = noDaysUnits;
            }
            else if (noDaysUnits == null)
            {
                minimumDays = daysContractEnd;
            }
            else
            {
                minimumDays = Math.Min(daysContractEnd.Value, noDaysUnits.Value);
            }

            return (double)minimumDays;
        }

        public decimal CalculateTotalCost(decimal dailyRate, decimal deposit, decimal termInMonths)
        {
            if (dailyRate <= 0)
                return 0;
            return (dailyRate * 30 * termInMonths) + deposit;
        }

        public decimal CalculateTotalLoan(decimal dailyRate, decimal termInMonths)
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

        public DateTime ContractEndDate(DateTime firstPaymentDate, double termInMonths)
        {
            return firstPaymentDate.AddDays(termInMonths * 30);
        }

        public double CalculateNoDaysLeft(DateTime firstPaymentDate, double termInMonths)
        {
            DateTime contractEndDate = firstPaymentDate.AddDays(termInMonths * 30);
            double daysContractEnd = (contractEndDate - firstPaymentDate).Days;

            return daysContractEnd;
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
