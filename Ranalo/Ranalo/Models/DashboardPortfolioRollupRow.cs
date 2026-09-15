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
    //
    // "In default" (the Dealer/Admin dashboards' Total Accounts card) is
    // defined as the Arrears tier -- ArrearsCount / TotalAccounts is what
    // ScheduledDashboardRollup writes as InDefault / DefaultRatePct.
    // TotalAccounts here is the *unfiltered* Contract_Info count (same
    // population as DashboardKpiRollupRow.TotalAccounts), not just the
    // subset with a payment plan that GoodPct/SlowPct/ArrearsPct are
    // percentages of -- so DefaultRatePct is a rate against the same "Total
    // Accounts" figure the KPI card shows.
    public class DashboardPortfolioRollupRow
    {
        public int? DealerId { get; set; }
        public decimal GoodPct { get; set; }
        public decimal SlowPct { get; set; }
        public decimal ArrearsPct { get; set; }
        public decimal NonPayingPct { get; set; }
        public decimal ArrearsTotal { get; set; }
        public int ArrearsCount { get; set; }
        public int TotalAccounts { get; set; }

        // Same ArrearsTotal formula, computed as of one calendar month ago
        // (payments received by then, accrual measured up to then, accounts
        // that already existed by then) -- lets ScheduledDashboardRollup
        // derive a real ArrearsChangePct instead of leaving it unpopulated.
        public decimal ArrearsTotalLastMonth { get; set; }
    }
}
