using Ranalo.Models;
using Ranalo.ScheduledServices;

namespace Ranolo.Web.Tests
{
    public class ScheduledDashboardRollupTests
    {
        [Test]
        public void CalculateGrowthPct_NormalGrowth_ReturnsRoundedPercentage()
        {
            var result = ScheduledDashboardRollup.CalculateGrowthPct(revenueThisMonth: 110_000m, revenueLastMonth: 100_000m);

            Assert.That(result, Is.EqualTo(10.00m));
        }

        [Test]
        public void CalculateGrowthPct_Decline_ReturnsNegativePercentage()
        {
            var result = ScheduledDashboardRollup.CalculateGrowthPct(revenueThisMonth: 80_000m, revenueLastMonth: 100_000m);

            Assert.That(result, Is.EqualTo(-20.00m));
        }

        [Test]
        public void CalculateGrowthPct_NoRevenueLastMonth_ReturnsNullNotInfinite()
        {
            var result = ScheduledDashboardRollup.CalculateGrowthPct(revenueThisMonth: 50_000m, revenueLastMonth: 0m);

            Assert.That(result, Is.Null);
        }

        [Test]
        public async Task RefreshKpiSnapshotsAsync_MapsDealerRowsAndGlobalRowToCorrectScopes()
        {
            var fakeRepo = new FakeDashboardReportRepository
            {
                KpiRollupToReturn = new List<DashboardKpiRollupRow>
                {
                    new() { DealerId = null, RevenueThisMonth = 500_000m, RevenueLastMonth = 400_000m, TotalAccounts = 900, NewThisMonth = 50 },
                    new() { DealerId = 7, RevenueThisMonth = 120_000m, RevenueLastMonth = 100_000m, TotalAccounts = 140, NewThisMonth = 12 },
                },
            };

            var count = await ScheduledDashboardRollup.RefreshKpiSnapshotsAsync(fakeRepo);

            Assert.That(count, Is.EqualTo(2));
            Assert.That(fakeRepo.UpsertedSnapshots, Has.Count.EqualTo(2));

            var globalRow = fakeRepo.UpsertedSnapshots.Single(u => u.Scope.DealerId == null);
            Assert.That(globalRow.RevenueThisMonth, Is.EqualTo(500_000m));
            Assert.That(globalRow.RevenueGrowthPct, Is.EqualTo(25.00m));
            Assert.That(globalRow.TotalAccounts, Is.EqualTo(900));

            var dealerRow = fakeRepo.UpsertedSnapshots.Single(u => u.Scope.DealerId == 7);
            Assert.That(dealerRow.RevenueGrowthPct, Is.EqualTo(20.00m));
            Assert.That(dealerRow.NewThisMonth, Is.EqualTo(12));
        }

        [Test]
        public async Task RefreshPortfolioClassificationAsync_MapsDealerRowsAndGlobalRowToCorrectScopes()
        {
            var fakeRepo = new FakeDashboardReportRepository
            {
                PortfolioRollupToReturn = new List<DashboardPortfolioRollupRow>
                {
                    new() { DealerId = null, GoodPct = 80m, SlowPct = 12m, ArrearsPct = 8m, NonPayingPct = 3m, ArrearsTotal = 250_000m },
                    new() { DealerId = 7, GoodPct = 91m, SlowPct = 5m, ArrearsPct = 4m, NonPayingPct = 1m, ArrearsTotal = 12_000m },
                },
            };

            var count = await ScheduledDashboardRollup.RefreshPortfolioClassificationAsync(fakeRepo);

            Assert.That(count, Is.EqualTo(2));
            Assert.That(fakeRepo.UpsertedPortfolios, Has.Count.EqualTo(2));

            var globalRow = fakeRepo.UpsertedPortfolios.Single(u => u.Scope.DealerId == null);
            Assert.That(globalRow.GoodPct, Is.EqualTo(80m));
            Assert.That(globalRow.ArrearsTotal, Is.EqualTo(250_000m));

            var dealerRow = fakeRepo.UpsertedPortfolios.Single(u => u.Scope.DealerId == 7);
            Assert.That(dealerRow.NonPayingPct, Is.EqualTo(1m));
            Assert.That(dealerRow.ArrearsTotal, Is.EqualTo(12_000m));
        }

        [Test]
        public async Task RefreshCommissionSnapshotsAsync_MapsDealerRowsToCorrectScope_NoGlobalRow()
        {
            var fakeRepo = new FakeDashboardReportRepository
            {
                CommissionRollupToReturn = new List<DashboardCommissionRollupRow>
                {
                    new() { DealerId = 7, CommissionReceived = 50_000m, CommissionPaidToAgents = 30_000m, CommissionOutstanding = 5_000m },
                },
            };

            var count = await ScheduledDashboardRollup.RefreshCommissionSnapshotsAsync(fakeRepo);

            Assert.That(count, Is.EqualTo(1));
            Assert.That(fakeRepo.UpsertedCommissions, Has.Count.EqualTo(1));

            var dealerRow = fakeRepo.UpsertedCommissions.Single();
            Assert.That(dealerRow.Scope.DealerId, Is.EqualTo(7));
            Assert.That(dealerRow.CommissionReceived, Is.EqualTo(50_000m));
            Assert.That(dealerRow.CommissionPaidToAgents, Is.EqualTo(30_000m));
            Assert.That(dealerRow.CommissionOutstanding, Is.EqualTo(5_000m));
        }
    }
}
