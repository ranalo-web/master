using Ranalo.DataStore;
using Ranalo.Models;

namespace Ranolo.Web.Tests
{
    // Fake repository standing in for the DB-backed DashboardReportRepository so
    // these tests exercise DashboardReportService's mapping/fallback logic
    // without needing SQL Server (the rollup tables aren't applied anywhere
    // reachable from this test run yet -- see Database/Dashboard/001_create_dashboard_tables.sql).
    public class FakeDashboardReportRepository : IDashboardReportRepository
    {
        public DashboardSnapshotRow? SnapshotToReturn { get; set; }
        public List<DashboardMonthlyTrendPoint> TrendToReturn { get; set; } = new();
        public Dictionary<string, List<DashboardWatchlistEntryRow>> WatchlistsToReturn { get; set; } = new();
        public Dictionary<string, List<DashboardPerformanceEntryRow>> PerformanceToReturn { get; set; } = new();
        public List<DashboardDeviceStockRow> DeviceStockToReturn { get; set; } = new();
        public List<DashboardCompletedContractRow> CompletedContractsToReturn { get; set; } = new();
        public List<DashboardKpiRollupRow> KpiRollupToReturn { get; set; } = new();
        public List<DashboardPortfolioRollupRow> PortfolioRollupToReturn { get; set; } = new();
        public List<DashboardCommissionRollupRow> CommissionRollupToReturn { get; set; } = new();
        public string? DealerNameToReturn { get; set; }
        public DashboardRevenuePeriodRow RevenuePeriodToReturn { get; set; } = new();
        public List<(DashboardScope Scope, decimal? RevenueThisMonth, decimal? RevenueGrowthPct, int? NewThisMonth, int? TotalAccounts)> UpsertedSnapshots { get; } = new();
        public List<(DashboardScope Scope, decimal? GoodPct, decimal? SlowPct, decimal? ArrearsPct, decimal? NonPayingPct, decimal? ArrearsTotal, decimal? ArrearsChangePct, int? InDefault, decimal? DefaultRatePct, decimal? ActivePct)> UpsertedPortfolios { get; } = new();
        public List<(DashboardScope Scope, decimal? CommissionReceived, decimal? CommissionPaidToAgents, decimal? CommissionOutstanding)> UpsertedCommissions { get; } = new();

        public Task<DashboardSnapshotRow?> GetSnapshotAsync(DashboardScope scope) => Task.FromResult(SnapshotToReturn);

        public Task<string?> GetDealerNameAsync(int dealerId) => Task.FromResult(DealerNameToReturn);

        public Task<DashboardRevenuePeriodRow> GetDealerRevenueForPeriodAsync(
            int dealerId, DateTime periodStart, DateTime periodEndExclusive, DateTime priorPeriodStart, DateTime priorPeriodEndExclusive) =>
            Task.FromResult(RevenuePeriodToReturn);

        public Task<List<DashboardMonthlyTrendPoint>> GetMonthlyTrendAsync(DashboardScope scope, int months = 8) =>
            Task.FromResult(TrendToReturn);

        public Task<List<DashboardWatchlistEntryRow>> GetWatchlistAsync(DashboardScope scope, string watchlistType, int take = 20) =>
            Task.FromResult(WatchlistsToReturn.TryGetValue(watchlistType, out var rows) ? rows : new List<DashboardWatchlistEntryRow>());

        public Task<List<DashboardPerformanceEntryRow>> GetPerformanceAsync(DashboardScope scope, string entryType, int take = 20) =>
            Task.FromResult(PerformanceToReturn.TryGetValue(entryType, out var rows) ? rows : new List<DashboardPerformanceEntryRow>());

        public Task<List<DashboardDeviceStockRow>> GetDeviceStockAsync(DashboardScope scope, int take = 20) =>
            Task.FromResult(DeviceStockToReturn);

        public Task<List<DashboardCompletedContractRow>> GetCompletedContractsAsync(DashboardScope scope, int take = 20) =>
            Task.FromResult(CompletedContractsToReturn);

        public Task<List<DashboardKpiRollupRow>> ComputeKpiRollupAsync() => Task.FromResult(KpiRollupToReturn);

        public Task UpsertSnapshotKpiAsync(DashboardScope scope, decimal? revenueThisMonth, decimal? revenueGrowthPct, int? newThisMonth, int? totalAccounts)
        {
            UpsertedSnapshots.Add((scope, revenueThisMonth, revenueGrowthPct, newThisMonth, totalAccounts));
            return Task.CompletedTask;
        }

        public Task<List<DashboardPortfolioRollupRow>> ComputePortfolioClassificationRollupAsync() => Task.FromResult(PortfolioRollupToReturn);

        public Task UpsertSnapshotPortfolioAsync(
            DashboardScope scope,
            decimal? portfolioGoodPct,
            decimal? portfolioSlowPct,
            decimal? portfolioArrearsPct,
            decimal? portfolioNonPayingPct,
            decimal? arrearsTotal,
            decimal? arrearsChangePct,
            int? inDefault,
            decimal? defaultRatePct,
            decimal? activePct)
        {
            UpsertedPortfolios.Add((scope, portfolioGoodPct, portfolioSlowPct, portfolioArrearsPct, portfolioNonPayingPct, arrearsTotal, arrearsChangePct, inDefault, defaultRatePct, activePct));
            return Task.CompletedTask;
        }

        public int RefreshCompletedContractsCallCount { get; private set; }

        public Task<int> RefreshCompletedContractsAsync(int topNPerScope = 20)
        {
            RefreshCompletedContractsCallCount++;
            return Task.FromResult(CompletedContractsToReturn.Count);
        }

        public int RefreshDeviceStockCallCount { get; private set; }

        public Task<int> RefreshDeviceStockAsync(int topNPerScope = 20)
        {
            RefreshDeviceStockCallCount++;
            return Task.FromResult(DeviceStockToReturn.Count);
        }

        public Task<List<DashboardCommissionRollupRow>> ComputeCommissionSnapshotRollupAsync() => Task.FromResult(CommissionRollupToReturn);

        public Task UpsertSnapshotCommissionAsync(DashboardScope scope, decimal? commissionReceived, decimal? commissionPaidToAgents, decimal? commissionOutstanding)
        {
            UpsertedCommissions.Add((scope, commissionReceived, commissionPaidToAgents, commissionOutstanding));
            return Task.CompletedTask;
        }

        public List<(DashboardScope Scope, decimal? DealerCommissionOutstanding)> UpsertedDealerCommissionOutstanding { get; } = new();

        public Task UpsertDealerCommissionOutstandingAsync(DashboardScope scope, decimal? dealerCommissionOutstanding)
        {
            UpsertedDealerCommissionOutstanding.Add((scope, dealerCommissionOutstanding));
            return Task.CompletedTask;
        }

        public int RefreshAgentCommissionListCallCount { get; private set; }

        public Task<int> RefreshAgentCommissionListAsync(int topNPerScope = 20)
        {
            RefreshAgentCommissionListCallCount++;
            return Task.FromResult(PerformanceToReturn.TryGetValue(DashboardPerformanceEntryType.AgentCommission, out var rows) ? rows.Count : 0);
        }
    }

    public class DashboardReportServiceTests
    {
        [Test]
        public async Task GetDealerDashboardAsync_WithNoRollupRow_FallsBackToZeroNotSampleData()
        {
            // A dealer with no rollup data yet should see 0s/empty lists, not
            // a fabricated business making KES 412,300/month -- see
            // GetDealerDashboardAsync's comment on why the base model
            // changed from DealerDashboardSampleData.Build() to a bare
            // DealerDashboardViewModel().
            var fakeRepo = new FakeDashboardReportRepository(); // no snapshot, no trend
            var service = new Ranalo.Services.DashboardReportService(fakeRepo);

            var result = await service.GetDealerDashboardAsync(dealerId: 42);

            Assert.That(result.RevenueThisMonth, Is.EqualTo(0m));
            Assert.That(result.TotalAccounts, Is.EqualTo(0));
            Assert.That(result.GrowthMonths, Is.Empty);
            Assert.That(result.RevenueByMonth, Is.Empty);
            Assert.That(result.NonPayers, Is.Empty);
            Assert.That(result.CommissionReceived, Is.EqualTo(0m));
        }

        [Test]
        public async Task GetDealerDashboardAsync_WithRollupRow_OverlaysKpiAndTrendOnly()
        {
            var fakeRepo = new FakeDashboardReportRepository
            {
                SnapshotToReturn = new DashboardSnapshotRow
                {
                    DealerId = 42,
                    RevenueThisMonth = 999_000m,
                    TotalAccounts = 321,
                    ActivePct = 88.5m,
                    ArrearsTotal = 5_000m,
                },
                TrendToReturn = new List<DashboardMonthlyTrendPoint>
                {
                    new() { YearMonth = "2026-07", Revenue = 100m, AccountsCount = 10 },
                    new() { YearMonth = "2026-08", Revenue = 200m, AccountsCount = 20 },
                },
            };
            var service = new Ranalo.Services.DashboardReportService(fakeRepo);

            var result = await service.GetDealerDashboardAsync(dealerId: 42);

            Assert.That(result.RevenueThisMonth, Is.EqualTo(999_000m));
            Assert.That(result.TotalAccounts, Is.EqualTo(321));
            Assert.That(result.ActivePct, Is.EqualTo(88.5m));
            Assert.That(result.ArrearsTotal, Is.EqualTo(5_000m));

            // AvgPerAccount isn't sourced from snapshot.AvgPerAccount (that
            // rollup column is never populated) -- it's derived from the
            // now-updated RevenueThisMonth/TotalAccounts above.
            Assert.That(result.AvgPerAccount, Is.EqualTo(999_000m / 321));
            // Fields the snapshot row leaves unset (null) fall back to 0, not
            // sample data -- this is what makes partial refresh job coverage
            // safe.
            Assert.That(result.BadDebtThisMonth, Is.EqualTo(0m));
            Assert.That(result.GrowthMonths, Is.EqualTo(new List<string> { "Jul", "Aug" }));
            Assert.That(result.RevenueByMonth, Is.EqualTo(new List<decimal> { 100m, 200m }));
            Assert.That(result.AccountsByMonth, Is.EqualTo(new List<int> { 10, 20 }));

            // Sections not yet wired should be empty, not sample data.
            Assert.That(result.NonPayers, Is.Empty);
            Assert.That(result.DeviceStock, Is.Empty);
        }

        [Test]
        public async Task GetDealerDashboardAsync_WithWatchlistAndPerformanceRows_OverlaysThemOnly()
        {
            var fakeRepo = new FakeDashboardReportRepository
            {
                WatchlistsToReturn = new()
                {
                    [DashboardWatchlistType.NonPayer] = new()
                    {
                        new() { Rank = 1, CustomerName = "Test Customer", AgentName = "Test Agent", Detail = "10 days" },
                    },
                },
                PerformanceToReturn = new()
                {
                    [DashboardPerformanceEntryType.Agent] = new()
                    {
                        new() { Rank = 1, SubjectId = 7, SubjectName = "Test Agent", Accounts = 12, ActivePct = 90, PctOfTarget = 105 },
                    },
                },
            };
            var service = new Ranalo.Services.DashboardReportService(fakeRepo);

            var result = await service.GetDealerDashboardAsync(dealerId: 42);

            Assert.That(result.NonPayers, Has.Count.EqualTo(1));
            Assert.That(result.NonPayers[0].CustomerName, Is.EqualTo("Test Customer"));
            Assert.That(result.AgentPerformance, Has.Count.EqualTo(1));
            Assert.That(result.AgentPerformance[0].AgentName, Is.EqualTo("Test Agent"));

            // Watchlist types with no rows returned stay empty, not sample data.
            Assert.That(result.SlowPayers, Is.Empty);
            Assert.That(result.GoodPayers, Is.Empty);
        }

        [Test]
        public async Task GetAdminDashboardAsync_WithNoRollupRow_FallsBackToSampleData()
        {
            var fakeRepo = new FakeDashboardReportRepository();
            var service = new Ranalo.Services.DashboardReportService(fakeRepo);
            var sample = Ranalo.Controllers.AdminDashboardSampleData.Build();

            var result = await service.GetAdminDashboardAsync();

            Assert.That(result.RevenueThisMonth, Is.EqualTo(sample.RevenueThisMonth));
            Assert.That(result.GoodAccounts, Is.EqualTo(sample.GoodAccounts));
            Assert.That(result.DealerPerformance.Count, Is.EqualTo(sample.DealerPerformance.Count));
        }

        [Test]
        public async Task GetAdminDashboardAsync_WithRollupRow_OverlaysAdminOnlyFieldsCorrectly()
        {
            var fakeRepo = new FakeDashboardReportRepository
            {
                SnapshotToReturn = new DashboardSnapshotRow
                {
                    RevenueThisMonth = 1_000_000m,
                    TotalAccounts = 5000,
                    GoodAccounts = 4200,
                    BadAccounts = 800,
                    NonPayingChange = -25,
                    RevenueTargetThisMonth = 1_200_000m,
                },
            };
            var service = new Ranalo.Services.DashboardReportService(fakeRepo);

            var result = await service.GetAdminDashboardAsync();

            Assert.That(result.RevenueThisMonth, Is.EqualTo(1_000_000m));
            Assert.That(result.TotalAccounts, Is.EqualTo(5000));
            Assert.That(result.GoodAccounts, Is.EqualTo(4200));
            Assert.That(result.BadAccounts, Is.EqualTo(800));
            Assert.That(result.NonPayingAccountsChange, Is.EqualTo(-25));
            Assert.That(result.RevenueTargetThisMonth, Is.EqualTo(1_200_000m));
        }

        [Test]
        public async Task GetAdminDashboardAsync_WithPerformanceRows_OverlaysDealerAndAgentPerformance()
        {
            var fakeRepo = new FakeDashboardReportRepository
            {
                PerformanceToReturn = new()
                {
                    [DashboardPerformanceEntryType.Dealer] = new()
                    {
                        new() { Rank = 1, SubjectId = 5, SubjectName = "Test Dealer", Accounts = 100, ActivePct = 92, Revenue = 50000, CommissionPaid = 1000, CommissionDue = 200, PctOfTarget = 110 },
                    },
                    [DashboardPerformanceEntryType.Agent] = new()
                    {
                        new() { Rank = 1, SubjectId = 7, SubjectName = "Test Agent", ParentName = "Test Dealer", Accounts = 30, ActivePct = 88, PctOfTarget = 101 },
                    },
                },
            };
            var service = new Ranalo.Services.DashboardReportService(fakeRepo);

            var result = await service.GetAdminDashboardAsync();

            Assert.That(result.DealerPerformance, Has.Count.EqualTo(1));
            Assert.That(result.DealerPerformance[0].DealerName, Is.EqualTo("Test Dealer"));
            Assert.That(result.DealerPerformance[0].Revenue, Is.EqualTo(50000m));
            Assert.That(result.AgentPerformance, Has.Count.EqualTo(1));
            Assert.That(result.AgentPerformance[0].DealerName, Is.EqualTo("Test Dealer"));
        }

        [Test]
        public async Task GetDealerDashboardAsync_WithDeviceStockAndCompletedContractRows_OverlaysThemOnly()
        {
            var fakeRepo = new FakeDashboardReportRepository
            {
                DeviceStockToReturn = new()
                {
                    new() { DeviceName = "Test Phone", Units = 5, AvgValue = 20000, GoodPct = 80, ArrearsPct = 20 },
                },
                CompletedContractsToReturn = new()
                {
                    new() { CustomerName = "Test Customer", ProductName = "Test Phone", CompletedDate = new DateTime(2026, 8, 9), TotalPaid = 15000, DurationMonths = 6 },
                },
            };
            var service = new Ranalo.Services.DashboardReportService(fakeRepo);

            var result = await service.GetDealerDashboardAsync(dealerId: 42);

            Assert.That(result.DeviceStock, Has.Count.EqualTo(1));
            Assert.That(result.DeviceStock[0].Device, Is.EqualTo("Test Phone"));
            Assert.That(result.CompletedContracts, Has.Count.EqualTo(1));
            Assert.That(result.CompletedContracts[0].CompletedDate, Is.EqualTo("Aug 9"));
        }

        [Test]
        public async Task GetAdminDashboardAsync_WithCompletedContractRows_OverlaysThemWithDealerName()
        {
            var fakeRepo = new FakeDashboardReportRepository
            {
                CompletedContractsToReturn = new()
                {
                    new() { CustomerName = "Test Customer", DealerName = "Test Dealer", ProductName = "Test Phone", CompletedDate = new DateTime(2026, 8, 28), TotalPaid = 19000, DurationMonths = 8 },
                },
            };
            var service = new Ranalo.Services.DashboardReportService(fakeRepo);

            var result = await service.GetAdminDashboardAsync();

            Assert.That(result.CompletedContracts, Has.Count.EqualTo(1));
            Assert.That(result.CompletedContracts[0].DealerName, Is.EqualTo("Test Dealer"));
            Assert.That(result.CompletedContracts[0].CompletedDate, Is.EqualTo("Aug 28"));
        }

        [Test]
        public async Task GetDealerDashboardAsync_WithUpsellTargetRow_MapsStatusAndNullCompletedDate()
        {
            var fakeRepo = new FakeDashboardReportRepository
            {
                CompletedContractsToReturn = new()
                {
                    new()
                    {
                        CustomerName = "Test Customer",
                        ProductName = "Test Phone",
                        CompletedDate = null,
                        TotalPaid = 16000,
                        DurationMonths = 6,
                        Status = DashboardCompletedContractStatus.UpsellTarget,
                        PctComplete = 85.5m,
                    },
                },
            };
            var service = new Ranalo.Services.DashboardReportService(fakeRepo);

            var result = await service.GetDealerDashboardAsync(dealerId: 42);

            Assert.That(result.CompletedContracts, Has.Count.EqualTo(1));
            Assert.That(result.CompletedContracts[0].Status, Is.EqualTo(DashboardCompletedContractStatus.UpsellTarget));
            Assert.That(result.CompletedContracts[0].PctComplete, Is.EqualTo(85.5m));
            Assert.That(result.CompletedContracts[0].CompletedDate, Is.EqualTo("In progress"));
        }

        [Test]
        public async Task GetDealerDashboardAsync_WithCommissionSnapshotFields_OverlaysThemOnly()
        {
            var fakeRepo = new FakeDashboardReportRepository
            {
                SnapshotToReturn = new DashboardSnapshotRow
                {
                    DealerId = 42,
                    CommissionReceived = 55_000m,
                    CommissionPaidToAgents = 30_000m,
                    CommissionOutstanding = 4_500m,
                },
            };
            var service = new Ranalo.Services.DashboardReportService(fakeRepo);

            var result = await service.GetDealerDashboardAsync(dealerId: 42);

            Assert.That(result.CommissionReceived, Is.EqualTo(55_000m));
            Assert.That(result.CommissionPaidToAgents, Is.EqualTo(30_000m));
            Assert.That(result.CommissionOutstanding, Is.EqualTo(4_500m));
            // Not populated by this snapshot row -- falls back to 0, not sample data.
            Assert.That(result.CommissionsChangePct, Is.EqualTo(0m));
        }

        [Test]
        public async Task GetDealerDashboardAsync_WithAgentCommissionRows_MapsToCommissionsPaidWithDerivedStatus()
        {
            var fakeRepo = new FakeDashboardReportRepository
            {
                PerformanceToReturn = new()
                {
                    [DashboardPerformanceEntryType.AgentCommission] = new()
                    {
                        new() { Rank = 1, SubjectId = 3, SubjectName = "Fully Settled Agent", Accounts = 10, CommissionDue = 5000m, CommissionPaid = 5000m },
                        new() { Rank = 2, SubjectId = 4, SubjectName = "Owed Agent", Accounts = 6, CommissionDue = 3000m, CommissionPaid = 1000m },
                    },
                },
            };
            var service = new Ranalo.Services.DashboardReportService(fakeRepo);

            var result = await service.GetDealerDashboardAsync(dealerId: 42);

            Assert.That(result.CommissionsPaid, Has.Count.EqualTo(2));

            var settled = result.CommissionsPaid.Single(c => c.AgentName == "Fully Settled Agent");
            Assert.That(settled.Outstanding, Is.EqualTo(0m));
            Assert.That(settled.Status, Is.EqualTo("Settled"));

            var owed = result.CommissionsPaid.Single(c => c.AgentName == "Owed Agent");
            Assert.That(owed.Outstanding, Is.EqualTo(2000m));
            Assert.That(owed.Status, Is.EqualTo("Outstanding"));
        }

        [Test]
        public async Task GetDealerDashboardAsync_WithNoAgentCommissionRows_StaysEmpty()
        {
            var fakeRepo = new FakeDashboardReportRepository();
            var service = new Ranalo.Services.DashboardReportService(fakeRepo);

            var result = await service.GetDealerDashboardAsync(dealerId: 42);

            Assert.That(result.CommissionsPaid, Is.Empty);
        }
    }
}
