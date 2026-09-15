using Ranalo.Models;

namespace Ranalo.DataStore
{
    public interface IDashboardReportRepository
    {
        Task<DashboardSnapshotRow?> GetSnapshotAsync(DashboardScope scope);

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
        Task UpsertSnapshotPortfolioAsync(
            DashboardScope scope,
            decimal? portfolioGoodPct,
            decimal? portfolioSlowPct,
            decimal? portfolioArrearsPct,
            decimal? portfolioNonPayingPct,
            decimal? arrearsTotal);

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
        // CommissionOutstanding) -- see DashboardCommissionRollupRow for the
        // exact formula. Read/compute side; paired with UpsertSnapshotCommissionAsync.
        Task<List<DashboardCommissionRollupRow>> ComputeCommissionSnapshotRollupAsync();

        Task UpsertSnapshotCommissionAsync(
            DashboardScope scope,
            decimal? commissionReceived,
            decimal? commissionPaidToAgents,
            decimal? commissionOutstanding);

        // Same full-replace pattern as RefreshCompletedContractsAsync/
        // RefreshDeviceStockAsync, writing DashboardPerformanceEntry rows with
        // EntryType = AgentCommission (deliberately not "Agent" -- see that
        // constant's doc comment). Dealer-only.
        Task<int> RefreshAgentCommissionListAsync(int topNPerScope = 20);
    }
}
