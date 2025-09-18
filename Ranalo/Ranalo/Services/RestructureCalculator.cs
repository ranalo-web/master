using Ranalo.Models;

namespace Ranalo.Services
{
    public static class RestructureCalculator
    {
        public static int CalculateDaysRestructured(DateTime dateAgreed) =>
            (DateTime.UtcNow.Date - dateAgreed.Date).Days;

        public static decimal CalculateTotalDue(decimal amountRes, DateTime dateAgreed) =>
            amountRes * (DateTime.UtcNow.Date - dateAgreed.Date).Days;

        public static decimal CalculateTotalPaid(IEnumerable<KosePayments> payments, DateTime dateAgreed) =>
            payments
                .Where(p => p.PaymentDateValue >= dateAgreed)
                .Sum(p => p.AmountValue);

        public static decimal CalculateArrears(decimal totalDue, decimal totalPaid) =>
            totalDue - totalPaid;

        public static DateTime CalculateAutoLockDate(decimal arrears, decimal amountRes)
        {
            if (amountRes <= 0) return DateTime.UtcNow.AddHours(27); // fail-safe
            int arrearsDays = (int)Math.Ceiling(arrears / amountRes);

            return DateTime.UtcNow
                .AddDays(arrearsDays)
                .AddHours(27);
        }
    }
}
