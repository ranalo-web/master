namespace Ranalo.Models
{
    // Total Arrears card: splits every account with a dollar arrears
    // shortfall (accrual-based Arrears < 0 -- same $ formula used elsewhere,
    // e.g. ComputePortfolioClassificationRollupAsync) into two groups based
    // on Devices.NextLockDateIsoFormat:
    //   - "True" arrears: NextLockDate has already passed -- genuinely
    //     overdue, nobody is actively managing it right now.
    //   - "Restructured" arrears: still shows a dollar shortfall, but
    //     NextLockDate is in the future (or unset) -- the customer is on a
    //     plan and catching up, not truly delinquent.
    // See IDashboardReportRepository.GetDealerArrearsClassificationAsync.
    public class DashboardArrearsClassificationRow
    {
        public decimal TrueArrearsTotal { get; set; }
        public int TrueArrearsCount { get; set; }

        // Average days past NextLockDate, across TrueArrearsCount accounts only.
        public decimal TrueArrearsAvgDaysLocked { get; set; }

        public decimal RestructuredArrearsTotal { get; set; }
        public int RestructuredArrearsCount { get; set; }

        // Bad Debt card: subset of "true" arrears (above) that's more than
        // 90 days past NextLockDate -- same threshold as
        // DashboardPortfolioRollupRow/DashboardLockClassificationRow's
        // NonPaying tier, just with the dollar total/avg-days this card
        // needs that the classification-only rows don't carry.
        public decimal BadDebtTotal { get; set; }
        public int BadDebtCount { get; set; }
        public decimal BadDebtAvgDaysLocked { get; set; }

        // Write-off card: the contract's full term has already elapsed
        // (StartDate + Term_in_Months, same cap the accrual Arrears formula
        // already applies) and there's still a dollar shortfall -- independent
        // of lock-date status, so this can include accounts from either the
        // "true" or "restructured" bucket above.
        public decimal WriteOffTotal { get; set; }
        public int WriteOffCount { get; set; }

        // Payments received this calendar month from accounts currently
        // classified as write-off (above) -- recoveries clawed back on debt
        // already written off, not the dealer's overall monthly revenue.
        public decimal WriteOffRecoveredThisMonth { get; set; }
    }
}
