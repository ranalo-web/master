using Ranalo.DataStore;
using Ranalo.Models;

namespace Ranalo.ScheduledServices
{
    // Nightly job that populates the dashboard rollup tables
    // (Database/Dashboard/001_create_dashboard_tables.sql) so Admin/Dealer
    // dashboards read precomputed values instead of recalculating on every
    // page load (see DashboardReportService).
    //
    // Currently refreshes:
    //  - the KPI snapshot's revenue/accounts fields (RevenueThisMonth,
    //    RevenueGrowthPct, NewThisMonth, TotalAccounts) via
    //    IDashboardReportRepository.ComputeKpiRollupAsync.
    //  - the arrears/portfolio classification (PortfolioGoodPct/SlowPct/
    //    ArrearsPct/NonPayingPct, ArrearsTotal) via
    //    ComputePortfolioClassificationRollupAsync -- see
    //    DashboardPortfolioRollupRow for the exact tier definitions. Both are
    //    single set-based queries (GROUPING SETS), not a per-dealer loop.
    //  - the completed-contracts / upsell-target list via
    //    RefreshCompletedContractsAsync -- a full delete+replace of the ranked
    //    top-N list (per dealer + company-wide), not a per-scope upsert like
    //    the two above (see that method's doc comment for why).
    //  - the (Dealer-only) device stock breakdown via RefreshDeviceStockAsync
    //    -- same delete+replace pattern, same classification, grouped by
    //    device model per dealer instead of per account.
    //  - Commissions (Dealer-only): CommissionReceived/CommissionPaidToAgents/
    //    CommissionOutstanding scalars via ComputeCommissionSnapshotRollupAsync,
    //    and the CommissionsPaid per-agent list via
    //    RefreshAgentCommissionListAsync (EntryType = AgentCommission, NOT
    //    "Agent" -- see DashboardPerformanceEntryType.AgentCommission's doc
    //    comment for why they're kept separate). Agent commission = 50% of
    //    Deposit vesting immediately + 25% after 90 days, pooled across all of
    //    an agent's accounts and reduced (uncapped) by the sum of their
    //    accounts' current arrears. Dealer commission = 30% of (lifetime
    //    TotalPaid - BuyingPrice - AgentGrossCommission) per account, floored
    //    at 0. Deferred: the CommissionsReceived transaction-level list,
    //    CommissionsChangePct (no historical comparison point), and Admin's
    //    DealerPerformance leaderboard (a separate, still-deferred feature --
    //    see below).
    //
    // NOT refreshed by this job yet:
    //  - Watchlists -- the existing supporting logic (ApplicationReportService's
    //    CallQualifyingFunc/GetQualifyingPageAsync) iterates every account in
    //    memory to produce a count, which does not scale to a 1M-account
    //    ledger looped per dealer. Needs a set-based replacement designed the
    //    same way the classification above was, before it's safe to wire in.
    //  - Agent/dealer performance leaderboards (EntryType = Agent/Dealer, the
    //    ActivePct/PctOfTarget ranking feature) -- still deferred. Now that
    //    commissions exist, only ActivePct/PctOfTarget definitions are left
    //    to design before this can be revisited.
    public class ScheduledDashboardRollup : BackgroundService
    {
        private readonly ILogger<ScheduledDashboardRollup> _logger;
        private readonly IServiceScopeFactory _scopeFactory;

        // Run at 4 AM UTC daily -- after ScheduledDailyPaymentSummary (3 AM)
        // and other existing jobs, so this reads a settled day's data.
        private static readonly TimeSpan RunTimeUtc = new TimeSpan(4, 0, 0);

        public ScheduledDashboardRollup(ILogger<ScheduledDashboardRollup> logger, IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Dashboard rollup service started at {time} (UTC)", DateTime.UtcNow);

            while (!stoppingToken.IsCancellationRequested)
            {
                DateTime utcNow = DateTime.UtcNow;
                DateTime nextRunUtc = utcNow.Date.Add(RunTimeUtc);

                if (nextRunUtc <= utcNow)
                {
                    nextRunUtc = nextRunUtc.AddDays(1);
                }

                TimeSpan delay = nextRunUtc - utcNow;

                _logger.LogInformation("Next scheduled Dashboard rollup run: {next}", nextRunUtc);

                try
                {
                    await Task.Delay(delay, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    _logger.LogInformation("Service cancellation detected, stopping.");
                    break;
                }

                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var repository = scope.ServiceProvider.GetRequiredService<IDashboardReportRepository>();

                    var kpiRowCount = await RefreshKpiSnapshotsAsync(repository);
                    var portfolioRowCount = await RefreshPortfolioClassificationAsync(repository);
                    var completedContractsRowCount = await repository.RefreshCompletedContractsAsync();
                    var deviceStockRowCount = await repository.RefreshDeviceStockAsync();
                    var commissionRowCount = await RefreshCommissionSnapshotsAsync(repository);
                    var agentCommissionListRowCount = await repository.RefreshAgentCommissionListAsync();

                    _logger.LogInformation(
                        "Dashboard rollup completed: refreshed {kpiCount} KPI row(s), {portfolioCount} portfolio row(s), {completedCount} completed-contract row(s), {deviceStockCount} device-stock row(s), {commissionCount} commission snapshot row(s), {agentCommissionCount} agent commission list row(s)",
                        kpiRowCount, portfolioRowCount, completedContractsRowCount, deviceStockRowCount, commissionRowCount, agentCommissionListRowCount);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error running Dashboard rollup job.");
                }
            }

            _logger.LogInformation("Dashboard rollup service stopped at {time} (UTC)", DateTime.UtcNow);
        }

        // Public so it can be exercised directly in tests against a fake
        // repository, without waiting on the BackgroundService's own timer.
        public static async Task<int> RefreshKpiSnapshotsAsync(IDashboardReportRepository repository)
        {
            var rollupRows = await repository.ComputeKpiRollupAsync();

            foreach (var row in rollupRows)
            {
                var scope = row.DealerId.HasValue
                    ? DashboardScope.ForDealer(row.DealerId.Value)
                    : DashboardScope.Admin;

                var growthPct = CalculateGrowthPct(row.RevenueThisMonth, row.RevenueLastMonth);

                await repository.UpsertSnapshotKpiAsync(
                    scope,
                    revenueThisMonth: row.RevenueThisMonth,
                    revenueGrowthPct: growthPct,
                    newThisMonth: row.NewThisMonth,
                    totalAccounts: row.TotalAccounts);
            }

            return rollupRows.Count;
        }

        // Public so it can be exercised directly in tests against a fake
        // repository, without waiting on the BackgroundService's own timer.
        public static async Task<int> RefreshPortfolioClassificationAsync(IDashboardReportRepository repository)
        {
            var rollupRows = await repository.ComputePortfolioClassificationRollupAsync();

            foreach (var row in rollupRows)
            {
                var scope = row.DealerId.HasValue
                    ? DashboardScope.ForDealer(row.DealerId.Value)
                    : DashboardScope.Admin;

                await repository.UpsertSnapshotPortfolioAsync(
                    scope,
                    portfolioGoodPct: row.GoodPct,
                    portfolioSlowPct: row.SlowPct,
                    portfolioArrearsPct: row.ArrearsPct,
                    portfolioNonPayingPct: row.NonPayingPct,
                    arrearsTotal: row.ArrearsTotal);
            }

            return rollupRows.Count;
        }

        // Public so it can be exercised directly in tests against a fake
        // repository, without waiting on the BackgroundService's own timer.
        public static async Task<int> RefreshCommissionSnapshotsAsync(IDashboardReportRepository repository)
        {
            var rollupRows = await repository.ComputeCommissionSnapshotRollupAsync();

            foreach (var row in rollupRows)
            {
                await repository.UpsertSnapshotCommissionAsync(
                    DashboardScope.ForDealer(row.DealerId),
                    commissionReceived: row.CommissionReceived,
                    commissionPaidToAgents: row.CommissionPaidToAgents,
                    commissionOutstanding: row.CommissionOutstanding);
            }

            return rollupRows.Count;
        }

        // Null (not 0%) when last month had no revenue to compare against --
        // "infinite growth" isn't a meaningful number to show.
        public static decimal? CalculateGrowthPct(decimal revenueThisMonth, decimal revenueLastMonth)
        {
            if (revenueLastMonth == 0)
            {
                return null;
            }

            return Math.Round((revenueThisMonth - revenueLastMonth) / revenueLastMonth * 100m, 2);
        }
    }
}
