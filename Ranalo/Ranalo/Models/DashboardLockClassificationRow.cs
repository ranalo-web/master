namespace Ranalo.Models
{
    // Paying vs Non-Paying / My Portfolio card classification, based on
    // Devices.NextLockDateIsoFormat rather than the accrual-based "days
    // overdue" formula used elsewhere on this page (PortfolioGoodPct/
    // InDefault) -- see IDashboardReportRepository.GetDealerLockClassificationAsync
    // for why. Same four-tier split as DashboardPortfolioRollupRow
    // (Good/Slow/Arrears/NonPaying), just measuring days past NextLockDate
    // instead of days past the self-calculated accrual due date.
    public class DashboardLockClassificationRow
    {
        // Not yet past NextLockDate (or no lock date set at all).
        public int GoodCount { get; set; }

        // 1-7 days past NextLockDate.
        public int SlowCount { get; set; }

        // More than 7 days past NextLockDate -- "true arrears". Includes
        // NonPayingCount below (a subset), same GoodPct+SlowPct+ArrearsPct=100%/
        // NonPayingPct-overlaps convention as DashboardPortfolioRollupRow.
        public int ArrearsCount { get; set; }

        // More than 90 days past NextLockDate -- subset of ArrearsCount.
        public int NonPayingCount { get; set; }
    }
}
