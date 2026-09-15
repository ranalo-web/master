namespace Ranalo.Models
{
    // One row per dealer, plus one row with DealerId == null for the
    // company-wide (Admin) total -- produced by
    // IDashboardReportRepository.ComputePortfolioClassificationRollupAsync()
    // in a single GROUPING SETS pass over Contract_Info/KosePayments.
    //
    // Classification (per account, based on "days overdue" against the
    // self-calculated lock date -- see ScheduledDashboardRollup's class
    // comment for the full writeup):
    //   Good      : daysOverdue <= 0   (on schedule or ahead)
    //   Slow      : 0 < daysOverdue <= 7
    //   Arrears   : daysOverdue > 7    (broad band -- INCLUDES NonPaying)
    //   NonPaying : daysOverdue > 90   (severe subset reported separately,
    //                                   already counted within Arrears)
    // GoodPct + SlowPct + ArrearsPct == 100 (mutually exclusive, exhaustive).
    // NonPayingPct is not a fourth slice -- it overlaps with ArrearsPct.
    public class DashboardPortfolioRollupRow
    {
        public int? DealerId { get; set; }
        public decimal GoodPct { get; set; }
        public decimal SlowPct { get; set; }
        public decimal ArrearsPct { get; set; }
        public decimal NonPayingPct { get; set; }
        public decimal ArrearsTotal { get; set; }
    }
}
