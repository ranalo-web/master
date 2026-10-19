namespace Ranalo.Services
{
    // Restructure rules (2026-10-19), as the original R script ran them, with
    // the business's decisions where the R broke down:
    //
    // Manual (agreed) restructure: units count from the customer's first
    // payment after the agreed date. Paid since then minus days x agreed daily
    // is ArrearsR; ahead, the phone stays open ArrearsR / agreed daily days
    // (24h each). Behind on the plan, the auto restructure rule below applies.
    //
    // Auto restructure (anyone in arrears): new daily = arrears spread over the
    // days left to the contract end, plus 10%, plus the normal daily. Paying at
    // least that within 24h opens the phone until last payment + 30h. Less
    // than that -- 1 shilling, say -- buys nothing. Past the contract end there
    // are no days left to spread over, so there's no auto restructure: the
    // phone stays locked until an agent agrees a manual one.
    public static class RestructureCalculator
    {
        public const decimal AutoUplift = 1.1m;
        public const int AutoOpenHours = 30;
        public const int HoursPerUnit = 24;

        public static decimal DailyPaymentAll(decimal daily, decimal weekly, decimal monthly) =>
            daily + weekly / 7m + monthly / 30m;

        public static DateTime ContractEndDate(DateTime firstPaymentDate, decimal termInMonths) =>
            firstPaymentDate.AddDays((double)termInMonths * 30);

        // Whole days left, as R's as.integer(difftime(...)).
        public static int DaysLeft(DateTime contractEnd, DateTime now) =>
            (int)Math.Truncate((contractEnd - now).TotalDays);

        // Null when there's nothing to auto restructure: not in arrears, or
        // the contract has reached its end.
        public static decimal? AutoNewDaily(decimal arrears, decimal dailyPaymentAll, DateTime contractEnd, DateTime now)
        {
            var daysLeft = DaysLeft(contractEnd, now);
            if (arrears >= 0 || daysLeft < 1)
            {
                return null;
            }

            return -arrears / daysLeft * AutoUplift + dailyPaymentAll;
        }

        // The auto restructure's lock date, or null when it doesn't open the phone.
        public static DateTime? AutoLockDate(decimal? newDaily, decimal paidLast24Hours, DateTime? lastPaymentDate, DateTime now) =>
            newDaily.HasValue && paidLast24Hours >= newDaily.Value
                ? (lastPaymentDate ?? now).AddHours(AutoOpenHours)
                : null;

        // Paid since the agreed date minus what was due since the first of
        // those payments. No payment yet: nothing paid, nothing due.
        public static decimal ManualArrears(decimal paidSinceAgreed, DateTime? firstPaymentAfterAgreed, decimal amountRes, DateTime now) =>
            firstPaymentAfterAgreed.HasValue
                ? paidSinceAgreed - (decimal)Math.Max(0, (now - firstPaymentAfterAgreed.Value).TotalDays) * amountRes
                : 0;

        public static int ManualDaysRestructured(DateTime? firstPaymentAfterAgreed, DateTime now) =>
            firstPaymentAfterAgreed.HasValue ? Math.Max(0, (int)(now - firstPaymentAfterAgreed.Value).TotalDays) : 0;

        // Ahead on the plan: open for the units paid. Otherwise now (locks).
        public static DateTime ManualLockDate(decimal arrearsR, decimal amountRes, DateTime now) =>
            arrearsR > 0 && amountRes > 0
                ? now.AddHours((double)(arrearsR / amountRes) * HoursPerUnit)
                : now;
    }
}
