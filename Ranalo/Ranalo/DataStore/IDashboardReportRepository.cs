using Ranalo.Models;

namespace Ranalo.DataStore
{
    public interface IDashboardReportRepository
    {
        Task<DashboardSnapshotRow?> GetSnapshotAsync(DashboardScope scope);

        // Real business name for the Dealer Dashboard header/greeting
        // ("Welcome back, {DealerName}") -- not part of DashboardSnapshot,
        // so it's not subject to the nightly rollup at all.
        Task<string?> GetDealerNameAsync(int dealerId);

        Task<List<DashboardMonthlyTrendPoint>> GetMonthlyTrendAsync(DashboardScope scope, int months = 8);

        Task<List<DashboardWatchlistEntryRow>> GetWatchlistAsync(DashboardScope scope, string watchlistType, int take = 20);

        Task<List<DashboardPerformanceEntryRow>> GetPerformanceAsync(DashboardScope scope, string entryType, int take = 20);

        Task<List<DashboardDeviceStockRow>> GetDeviceStockAsync(DashboardScope scope, int take = 20);

        Task<List<DashboardCompletedContractRow>> GetCompletedContractsAsync(DashboardScope scope, int take = 20);

        // Refresh (read) side, called only by ScheduledDashboardRollup. Single
        // set-based pass (GROUPING SETS) over Contract_Info/KosePayments/
        // Devices/Dealers -- one row per dealer plus one company-wide row
        // (DealerId == null), not a per-dealer loop over the ledger.
        Task<List<DashboardKpiRollupRow>> ComputeKpiRollupAsync();

        // Live (not rollup-backed) per-dealer revenue this month, keyed by
        // Dealers.CompanyName -- same source/spelling as
        // DashboardAccountDetailRow.DealerName, for the Approver Dashboard's
        // Dealer Performance table (GetApproverDashboardAsync), which only
        // has DealerName (not DealerId) to join against.
        Task<List<DashboardDealerRevenueRow>> GetRevenueThisMonthByDealerAsync();

        // Same single-pass GROUPING SETS pattern as ComputeKpiRollupAsync, for
        // the arrears/portfolio classification (see DashboardPortfolioRollupRow
        // for the exact tier definitions).
        Task<List<DashboardPortfolioRollupRow>> ComputePortfolioClassificationRollupAsync();

        // Refresh (write) side, called only by ScheduledDashboardRollup. Updates
        // only the KPI columns it's given, leaving every other column on the
        // row (if any) untouched -- see the nullable-fallback note on
        // DashboardSnapshotRow.
        Task UpsertSnapshotKpiAsync(
            DashboardScope scope,
            decimal? revenueThisMonth,
            decimal? revenueGrowthPct,
            int? newThisMonth,
            int? totalAccounts);

        // Same "update only these columns" contract as UpsertSnapshotKpiAsync.
        // inDefault/defaultRatePct/activePct are derived from the Arrears
        // tier -- see DashboardPortfolioRollupRow. arrearsChangePct compares
        // against DashboardPortfolioRollupRow.ArrearsTotalLastMonth (a real
        // historical recompute, not a stored snapshot diff).
        Task UpsertSnapshotPortfolioAsync(
            DashboardScope scope,
            decimal? portfolioGoodPct,
            decimal? portfolioSlowPct,
            decimal? portfolioArrearsPct,
            decimal? portfolioNonPayingPct,
            decimal? arrearsTotal,
            decimal? arrearsChangePct,
            int? inDefault,
            decimal? defaultRatePct,
            decimal? activePct);

        // Unlike the snapshot upserts above, this is a full replace, not a
        // per-scope upsert: DashboardCompletedContract is a ranked top-N
        // *list* (per dealer + company-wide), not one row per scope, so
        // there's no natural per-row key to merge against -- every refresh
        // recomputes and replaces the whole table in one statement. Returns
        // the number of rows inserted.
        Task<int> RefreshCompletedContractsAsync(int topNPerScope = 20);

        // Same full-replace pattern as RefreshCompletedContractsAsync, grouped
        // by device model (Make + Model) per dealer instead of per account.
        // Dealer-only -- Admin's ProductPerformance is a different shape with
        // no rollup table (see DashboardReportService's class comment).
        Task<int> RefreshDeviceStockAsync(int topNPerScope = 20);

        // Per-dealer commission scalars (CommissionReceived/CommissionPaidToAgents/
        // CommissionOutstanding/DealerCommissionOutstanding) -- see
        // DashboardCommissionRollupRow for the exact formula. Read/compute
        // side; paired with UpsertSnapshotCommissionAsync.
        Task<List<DashboardCommissionRollupRow>> ComputeCommissionSnapshotRollupAsync();

        Task<(decimal CommissionOutstanding, int CommissionAccountCount, decimal CommissionWithheldForArrears)> GetAgentCommissionSummaryAsync(int dealerId, int agentUserId);

        Task<(decimal CommissionPaidLifetime, int BonusEarnedAccountCount, int BonusAtRiskAccountCount, int BonusUpcomingAccountCount)> GetAgentBonusTrackerAsync(int dealerId, int agentUserId);

        Task UpsertSnapshotCommissionAsync(
            DashboardScope scope,
            decimal? commissionReceived,
            decimal? commissionPaidToAgents,
            decimal? commissionOutstanding);

        // Separate upsert so a not-yet-applied
        // 004_add_dealer_commission_outstanding.sql migration only affects
        // this one field -- see the implementation's comment.
        Task UpsertDealerCommissionOutstandingAsync(DashboardScope scope, decimal? dealerCommissionOutstanding);

        // Same decoupled-upsert pattern, for the two newest commission
        // columns (005_add_commission_account_stats.sql) -- see
        // DashboardCommissionRollupRow.CommissionAccountCount/CommissionWithheldForArrears.
        Task UpsertCommissionAccountStatsAsync(DashboardScope scope, int? commissionAccountCount, decimal? commissionWithheldForArrears);

        // Same decoupled-upsert pattern again, for
        // 006_add_dealer_commission_account_count.sql -- see
        // DashboardCommissionRollupRow.DealerCommissionAccountCount.
        Task UpsertDealerCommissionAccountCountAsync(DashboardScope scope, int? dealerCommissionAccountCount);

        // Same decoupled-upsert pattern again, for
        // 007_add_dealer_commission_cost_flags.sql -- see
        // DashboardCommissionRollupRow.DealerCommissionMissingCostCount/DealerCommissionWithheldForArrears.
        Task UpsertDealerCommissionCostFlagsAsync(DashboardScope scope, int? dealerCommissionMissingCostCount, decimal? dealerCommissionWithheldForArrears);

        // Dealer Commissions card: live sum of DealerCommissionPayments.PaidDate
        // within an arbitrary period window, backing the same top-of-page
        // filter as Revenue/New Accounts/Agent Commissions. Joined via
        // ContractId, not DealerCommissionPayments.DealerId -- same caution as
        // elsewhere in this codebase (that column's semantics aren't confirmed).
        Task<decimal> GetDealerCommissionPaidForPeriodAsync(int dealerId, DateTime periodStart, DateTime periodEndExclusive);

        // Same full-replace pattern as RefreshCompletedContractsAsync/
        // RefreshDeviceStockAsync, writing DashboardPerformanceEntry rows with
        // EntryType = AgentCommission (deliberately not "Agent" -- see that
        // constant's doc comment). Dealer-only.
        Task<int> RefreshAgentCommissionListAsync(int topNPerScope = 20);

        // Live (not rollup-backed) revenue query for the Dealer Dashboard's
        // date-range filter -- the nightly rollup only knows "this month" /
        // "last month", so an arbitrary period window has to hit
        // KosePayments/Devices/Dealers directly. periodEndExclusive and
        // priorPeriodEndExclusive are exclusive upper bounds.
        Task<DashboardRevenuePeriodRow> GetDealerRevenueForPeriodAsync(
            int? dealerId,
            DateTime periodStart,
            DateTime periodEndExclusive,
            DateTime priorPeriodStart,
            DateTime priorPeriodEndExclusive,
            int? agentUserId = null);

        // Paying vs Non-Paying card: classifies each of the dealer's accounts
        // as good or "true arrears" (more than 7 days past
        // Devices.NextLockDateIsoFormat) rather than the accrual-based
        // "days overdue" formula ComputePortfolioClassificationRollupAsync
        // uses elsewhere -- NextLockDate stays correct through a
        // restructuring, the accrual formula doesn't. See
        // DashboardLockClassificationRow.
        Task<DashboardLockClassificationRow> GetDealerLockClassificationAsync(int? dealerId, int? agentUserId = null);

        // Device Performance table: same NextLockDate-based classification as
        // GetDealerLockClassificationAsync, grouped by device (Make + Model)
        // instead of aggregated dealer-wide -- replaces RefreshDeviceStockAsync's
        // accrual-based per-device GoodPct/ArrearsPct. Keyed by the same
        // DeviceName string RefreshDeviceStockAsync/GetDeviceStockAsync use, so
        // callers can look up by DashboardDeviceStockRow.DeviceName.
        Task<Dictionary<string, DashboardLockClassificationRow>> GetDealerDeviceLockClassificationAsync(int dealerId);

        // Total Arrears card: splits dollar arrears into "true" (locked,
        // genuinely overdue) vs "restructured" (in arrears on paper, future
        // lock date, being managed) -- see DashboardArrearsClassificationRow.
        Task<DashboardArrearsClassificationRow> GetDealerArrearsClassificationAsync(int? dealerId, int? agentUserId = null);

        // Agent Commissions card: live sum of AgentCommissionPayments.AmountPaid
        // for this dealer within an arbitrary period window, backing the same
        // top-of-page filter as the Revenue/New Accounts cards. periodEndExclusive
        // is an exclusive upper bound, same convention as GetDealerRevenueForPeriodAsync.
        Task<decimal> GetDealerAgentCommissionPaidForPeriodAsync(int dealerId, DateTime periodStart, DateTime periodEndExclusive, int? agentUserId = null);

        // Single live per-account source for Non-Payers/Slow-Payers/Good-Payers,
        // Agent Performance, My Contracts, and Contracts Ending Soon -- every
        // one of those sections is a filter/projection of this same row set in
        // DashboardReportService, so they classify accounts identically to the
        // top KPI cards (GetDealerLockClassificationAsync/
        // GetDealerArrearsClassificationAsync) instead of each re-deriving its
        // own rule. No TOP cap -- a dealer's account count is bounded, and
        // capping would make section counts wrong.
        Task<List<DashboardAccountDetailRow>> GetDealerAccountDetailsAsync(int? dealerId, int? agentUserId = null);

        Task<(int Count, int OldestPendingDays)> GetOrdersAwaitingApprovalSummaryAsync();
    }
}
