using Ranalo.Controllers;
using Ranalo.DataStore;
using Ranalo.Models;
using Ranalo.ScheduledServices;

namespace Ranalo.Services
{
    // Assembles dashboard view models for Admin/Dealer (and, later, Agent)
    // scopes. Wired to the rollup tables so far
    // (Database/Dashboard/001_create_dashboard_tables.sql, populated by
    // ScheduledDashboardRollup once that job exists): KPI snapshot, monthly
    // trend, watchlists (non/slow/good payers), dealer/agent performance
    // leaderboards, device stock (Dealer only), and the completed-contracts
    // list. Commissions and Admin's ProductPerformance (a different shape,
    // no rollup table) still come from the sample data builders.
    //
    // If a scope has no rollup rows yet (schema not applied, or the nightly
    // job hasn't run for this dealer/agent yet), the sample-data values for
    // that slice are left in place instead of showing empty lists.
    public class DashboardReportService : IDashboardReportService
    {
        private readonly IDashboardReportRepository _repository;

        public DashboardReportService(IDashboardReportRepository repository)
        {
            _repository = repository;
        }

        public async Task<AdminDashboardViewModel> GetAdminDashboardAsync()
        {
            var model = AdminDashboardSampleData.Build();
            var scope = DashboardScope.Admin;

            var snapshot = await _repository.GetSnapshotAsync(scope);
            if (snapshot != null)
            {
                ApplyAdminSnapshot(model, snapshot);
            }

            var trend = await _repository.GetMonthlyTrendAsync(scope);
            if (trend.Count > 0)
            {
                model.GrowthMonths = trend.Select(t => MonthLabel(t.YearMonth)).ToList();
                model.RevenueByMonth = trend.Select(t => t.Revenue).ToList();
                model.AccountsByMonth = trend.Select(t => t.AccountsCount).ToList();
            }

            var nonPayers = await _repository.GetWatchlistAsync(scope, DashboardWatchlistType.NonPayer);
            if (nonPayers.Count > 0) model.NonPayers = nonPayers.Select(ToAdminWatchlistEntry).ToList();

            var slowPayers = await _repository.GetWatchlistAsync(scope, DashboardWatchlistType.SlowPayer);
            if (slowPayers.Count > 0) model.SlowPayers = slowPayers.Select(ToAdminWatchlistEntry).ToList();

            var goodPayers = await _repository.GetWatchlistAsync(scope, DashboardWatchlistType.GoodPayer);
            if (goodPayers.Count > 0) model.GoodPayers = goodPayers.Select(ToAdminWatchlistEntry).ToList();

            var dealerPerformance = await _repository.GetPerformanceAsync(scope, DashboardPerformanceEntryType.Dealer);
            if (dealerPerformance.Count > 0)
            {
                model.DealerPerformance = dealerPerformance.Select(p => new AdminDealerPerformance
                {
                    Rank = p.Rank,
                    DealerName = p.SubjectName,
                    Accounts = p.Accounts,
                    ActivePct = p.ActivePct,
                    Revenue = p.Revenue ?? 0,
                    CommissionPaid = p.CommissionPaid ?? 0,
                    CommissionDue = p.CommissionDue ?? 0,
                    PctOfTarget = p.PctOfTarget,
                }).ToList();
            }

            var agentPerformance = await _repository.GetPerformanceAsync(scope, DashboardPerformanceEntryType.Agent);
            if (agentPerformance.Count > 0)
            {
                model.AgentPerformance = agentPerformance.Select(p => new AdminAgentPerformance
                {
                    Rank = p.Rank,
                    AgentName = p.SubjectName,
                    DealerName = p.ParentName ?? "",
                    Accounts = p.Accounts,
                    ActivePct = p.ActivePct,
                    PctOfTarget = p.PctOfTarget,
                }).ToList();
            }

            var completedContracts = await _repository.GetCompletedContractsAsync(scope);
            if (completedContracts.Count > 0)
            {
                model.CompletedContracts = completedContracts.Select(c => new AdminCompletedContract
                {
                    CustomerName = c.CustomerName,
                    DealerName = c.DealerName ?? "",
                    ProductName = c.ProductName,
                    CompletedDate = CompletedDateLabel(c.CompletedDate),
                    TotalPaid = c.TotalPaid,
                    DurationMonths = c.DurationMonths,
                    Status = c.Status,
                    PctComplete = c.PctComplete,
                }).ToList();
            }

            // Admin's ProductPerformance is a different shape (Rank/Revenue/DefaultRatePct,
            // no Units/GoodPct/ArrearsPct) with no rollup table yet -- stays on sample data.

            return model;
        }

        public async Task<DealerDashboardViewModel> GetDealerDashboardAsync(int dealerId)
        {
            // Zero/empty defaults, not DealerDashboardSampleData.Build() -- a
            // dealer with no rollup data yet should see 0s, not a fabricated
            // "Nairobi Mobile Hub" business making KES 412,300/month. Every
            // block below already only overwrites a field when it found real
            // rows, so this only changes what's shown when nothing was found.
            var model = new DealerDashboardViewModel();
            var scope = DashboardScope.ForDealer(dealerId);

            var dealerName = await _repository.GetDealerNameAsync(dealerId);
            if (!string.IsNullOrWhiteSpace(dealerName))
            {
                model.DealerName = dealerName;
            }

            var snapshot = await _repository.GetSnapshotAsync(scope);
            if (snapshot != null)
            {
                ApplyDealerSnapshot(model, snapshot);
            }

            // Target isn't part of the nightly rollup (see
            // DashboardRevenuePeriodRow.TargetRevenue) -- computed live here,
            // same as the date-range filter, just always for "this month"
            // since that's what the page loads with by default.
            if (TryResolvePeriodWindow("month", out var monthWindow))
            {
                var monthRow = await _repository.GetDealerRevenueForPeriodAsync(
                    dealerId, monthWindow.PeriodStart, monthWindow.PeriodEndExclusive, monthWindow.PriorPeriodStart, monthWindow.PriorPeriodEndExclusive);
                model.RevenueTarget = monthRow.TargetRevenue;
            }

            var trend = await _repository.GetMonthlyTrendAsync(scope);
            if (trend.Count > 0)
            {
                model.GrowthMonths = trend.Select(t => MonthLabel(t.YearMonth)).ToList();
                model.RevenueByMonth = trend.Select(t => t.Revenue).ToList();
                model.AccountsByMonth = trend.Select(t => t.AccountsCount).ToList();
            }

            var nonPayers = await _repository.GetWatchlistAsync(scope, DashboardWatchlistType.NonPayer);
            if (nonPayers.Count > 0) model.NonPayers = nonPayers.Select(ToDealerWatchlistEntry).ToList();

            var slowPayers = await _repository.GetWatchlistAsync(scope, DashboardWatchlistType.SlowPayer);
            if (slowPayers.Count > 0) model.SlowPayers = slowPayers.Select(ToDealerWatchlistEntry).ToList();

            var goodPayers = await _repository.GetWatchlistAsync(scope, DashboardWatchlistType.GoodPayer);
            if (goodPayers.Count > 0) model.GoodPayers = goodPayers.Select(ToDealerWatchlistEntry).ToList();

            var agentPerformance = await _repository.GetPerformanceAsync(scope, DashboardPerformanceEntryType.Agent);
            if (agentPerformance.Count > 0)
            {
                model.AgentPerformance = agentPerformance.Select(p => new DealerAgentPerformance
                {
                    Rank = p.Rank,
                    AgentName = p.SubjectName,
                    Accounts = p.Accounts,
                    ActivePct = p.ActivePct,
                    PctOfTarget = p.PctOfTarget,
                }).ToList();
            }

            var agentCommissions = await _repository.GetPerformanceAsync(scope, DashboardPerformanceEntryType.AgentCommission);
            if (agentCommissions.Count > 0)
            {
                model.CommissionsPaid = agentCommissions.Select(c =>
                {
                    var due = c.CommissionDue ?? 0;
                    var paid = c.CommissionPaid ?? 0;
                    var outstanding = due - paid;
                    return new DealerCommissionPaid
                    {
                        AgentName = c.SubjectName,
                        Accounts = c.Accounts,
                        Due = due,
                        Paid = paid,
                        Outstanding = outstanding,
                        Status = outstanding <= 0 ? "Settled" : "Outstanding",
                    };
                }).ToList();
            }

            var deviceStock = await _repository.GetDeviceStockAsync(scope);
            if (deviceStock.Count > 0)
            {
                model.DeviceStock = deviceStock.Select(d => new DealerDeviceStock
                {
                    Device = d.DeviceName,
                    Units = d.Units,
                    AvgValue = d.AvgValue,
                    GoodPct = d.GoodPct,
                    ArrearsPct = d.ArrearsPct,
                }).ToList();
            }

            var completedContracts = await _repository.GetCompletedContractsAsync(scope);
            if (completedContracts.Count > 0)
            {
                model.CompletedContracts = completedContracts.Select(c => new DealerCompletedContract
                {
                    CustomerName = c.CustomerName,
                    ProductName = c.ProductName,
                    CompletedDate = CompletedDateLabel(c.CompletedDate),
                    TotalPaid = c.TotalPaid,
                    DurationMonths = c.DurationMonths,
                    Status = c.Status,
                    PctComplete = c.PctComplete,
                }).ToList();
            }

            return model;
        }

        public async Task<DealerRevenuePeriodResult?> GetDealerRevenueForPeriodAsync(int dealerId, string period)
        {
            if (!TryResolvePeriodWindow(period, out var window))
            {
                return null;
            }

            var row = await _repository.GetDealerRevenueForPeriodAsync(
                dealerId, window.PeriodStart, window.PeriodEndExclusive, window.PriorPeriodStart, window.PriorPeriodEndExclusive);

            return new DealerRevenuePeriodResult
            {
                Revenue = row.RevenueThisPeriod,
                GrowthPct = ScheduledDashboardRollup.CalculateGrowthPct(row.RevenueThisPeriod, row.RevenueLastPeriod),
                AvgPerAccount = row.TotalAccounts > 0 ? row.RevenueThisPeriod / row.TotalAccounts : 0,
                TargetRevenue = row.TargetRevenue,
                Label = window.Label,
            };
        }

        private readonly record struct PeriodWindow(
            DateTime PeriodStart, DateTime PeriodEndExclusive,
            DateTime PriorPeriodStart, DateTime PriorPeriodEndExclusive, string Label);

        // "week"/"month" are rolling/calendar windows compared against the
        // immediately preceding window of the same length; "ytd"/"year" are
        // compared against the same window one year earlier, since a
        // week-ago comparison isn't meaningful for either.
        private static bool TryResolvePeriodWindow(string period, out PeriodWindow window)
        {
            var today = DateTime.Now.Date;
            var tomorrow = today.AddDays(1);

            switch (period?.ToLowerInvariant())
            {
                case "week":
                    var weekStart = today.AddDays(-6);
                    window = new PeriodWindow(weekStart, tomorrow, weekStart.AddDays(-7), weekStart, "this week");
                    return true;

                case "month":
                    var monthStart = new DateTime(today.Year, today.Month, 1);
                    window = new PeriodWindow(monthStart, monthStart.AddMonths(1), monthStart.AddMonths(-1), monthStart, "this month");
                    return true;

                case "ytd":
                    var yearStart = new DateTime(today.Year, 1, 1);
                    window = new PeriodWindow(yearStart, tomorrow, yearStart.AddYears(-1), tomorrow.AddYears(-1), "year to date");
                    return true;

                case "year":
                    var yearWindowStart = tomorrow.AddYears(-1);
                    window = new PeriodWindow(yearWindowStart, tomorrow, yearWindowStart.AddYears(-1), yearWindowStart, "trailing year");
                    return true;

                default:
                    window = default;
                    return false;
            }
        }

        private static AdminWatchlistEntry ToAdminWatchlistEntry(DashboardWatchlistEntryRow row) => new()
        {
            CustomerName = row.CustomerName,
            DealerName = row.DealerName ?? "",
            Phone = row.Phone ?? "",
            Detail = row.Detail,
        };

        private static DealerWatchlistEntry ToDealerWatchlistEntry(DashboardWatchlistEntryRow row) => new()
        {
            CustomerName = row.CustomerName,
            AgentName = row.AgentName ?? "",
            Detail = row.Detail,
        };

        private static void ApplyAdminSnapshot(AdminDashboardViewModel model, DashboardSnapshotRow snapshot)
        {
            model.RevenueThisMonth = snapshot.RevenueThisMonth ?? model.RevenueThisMonth;
            model.RevenueGrowthPct = snapshot.RevenueGrowthPct ?? model.RevenueGrowthPct;
            model.RevenueTargetThisMonth = snapshot.RevenueTargetThisMonth ?? model.RevenueTargetThisMonth;

            model.TotalAccounts = snapshot.TotalAccounts ?? model.TotalAccounts;
            model.GoodAccounts = snapshot.GoodAccounts ?? model.GoodAccounts;
            model.BadAccounts = snapshot.BadAccounts ?? model.BadAccounts;
            model.PayingAccounts = snapshot.PayingAccounts ?? model.PayingAccounts;
            model.NonPayingAccounts = snapshot.NonPayingAccounts ?? model.NonPayingAccounts;
            model.NonPayingAccountsChange = snapshot.NonPayingChange ?? model.NonPayingAccountsChange;

            model.ArrearsTotal = snapshot.ArrearsTotal ?? model.ArrearsTotal;
            model.ArrearsChangePct = snapshot.ArrearsChangePct ?? model.ArrearsChangePct;

            model.PortfolioGoodPct = snapshot.PortfolioGoodPct ?? model.PortfolioGoodPct;
            model.PortfolioSlowPct = snapshot.PortfolioSlowPct ?? model.PortfolioSlowPct;
            model.PortfolioArrearsPct = snapshot.PortfolioArrearsPct ?? model.PortfolioArrearsPct;
            model.PortfolioNonPayingPct = snapshot.PortfolioNonPayingPct ?? model.PortfolioNonPayingPct;
            model.PortfolioGoodPctChange = snapshot.PortfolioGoodPctChange ?? model.PortfolioGoodPctChange;

            model.CollectionRatePct = snapshot.CollectionRatePct ?? model.CollectionRatePct;
            model.CollectionRateChangePct = snapshot.CollectionRateChangePct ?? model.CollectionRateChangePct;
            model.PortfolioAtRiskPct = snapshot.PortfolioAtRiskPct ?? model.PortfolioAtRiskPct;
            model.PortfolioAtRiskChangePct = snapshot.PortfolioAtRiskChangePct ?? model.PortfolioAtRiskChangePct;

            model.CostOfDevicesThisMonth = snapshot.CostOfDevicesThisMonth ?? model.CostOfDevicesThisMonth;
            model.BadDebtThisMonth = snapshot.BadDebtThisMonth ?? model.BadDebtThisMonth;
            model.BadDebtChangePct = snapshot.BadDebtChangePct ?? model.BadDebtChangePct;
            model.NetProfitChangePct = snapshot.NetProfitChangePct ?? model.NetProfitChangePct;
            model.ProfitMarginChangePct = snapshot.ProfitMarginChangePct ?? model.ProfitMarginChangePct;
            model.ProfitMarginTargetPct = snapshot.ProfitMarginTargetPct ?? model.ProfitMarginTargetPct;
            // CommissionsChangePct intentionally left on sample data -- commissions are deferred.

            model.OperatingExpensesThisMonth = snapshot.OperatingExpensesThisMonth ?? model.OperatingExpensesThisMonth;
            model.TaxRatePct = snapshot.TaxRatePct ?? model.TaxRatePct;
            model.DividendsPaidThisMonth = snapshot.DividendsPaidThisMonth ?? model.DividendsPaidThisMonth;

            model.TotalCustomers = snapshot.TotalCustomers ?? model.TotalCustomers;
            model.NewCustomersThisMonth = snapshot.NewCustomersThisMonth ?? model.NewCustomersThisMonth;
            model.RepeatCustomerRatePct = snapshot.RepeatCustomerRatePct ?? model.RepeatCustomerRatePct;
            model.AvgCustomerLifetimeValue = snapshot.AvgCustomerLifetimeValue ?? model.AvgCustomerLifetimeValue;
            model.ChurnRatePct = snapshot.ChurnRatePct ?? model.ChurnRatePct;

            model.CompletedContractsThisMonth = snapshot.CompletedContractsThisMonth ?? model.CompletedContractsThisMonth;
            model.CompletedContractsChangePct = snapshot.CompletedContractsChangePct ?? model.CompletedContractsChangePct;
            model.ContractCompletionRatePct = snapshot.ContractCompletionRatePct ?? model.ContractCompletionRatePct;
            model.ContractCompletionRateChangePct = snapshot.ContractCompletionRateChangePct ?? model.ContractCompletionRateChangePct;
            model.AvgTimeToCompletionMonths = snapshot.AvgTimeToCompletionMonths ?? model.AvgTimeToCompletionMonths;
            model.TotalValueCompletedThisMonth = snapshot.TotalValueCompletedThisMonth ?? model.TotalValueCompletedThisMonth;
        }

        private static void ApplyDealerSnapshot(DealerDashboardViewModel model, DashboardSnapshotRow snapshot)
        {
            model.RevenueThisMonth = snapshot.RevenueThisMonth ?? model.RevenueThisMonth;
            model.RevenueGrowthPct = snapshot.RevenueGrowthPct ?? model.RevenueGrowthPct;

            model.TotalAccounts = snapshot.TotalAccounts ?? model.TotalAccounts;

            // Not sourced from snapshot.AvgPerAccount -- ScheduledDashboardRollup
            // never computes that column, so it would be permanently null and
            // this would silently stay on sample data. Derived instead from the
            // (now-updated) revenue/account figures above, the same way the
            // date-range filter computes it in GetDealerRevenueForPeriodAsync.
            model.AvgPerAccount = model.TotalAccounts > 0 ? model.RevenueThisMonth / model.TotalAccounts : model.AvgPerAccount;

            model.ActivePct = snapshot.ActivePct ?? model.ActivePct;
            model.NewThisMonth = snapshot.NewThisMonth ?? model.NewThisMonth;
            model.InDefault = snapshot.InDefault ?? model.InDefault;
            model.DefaultRatePct = snapshot.DefaultRatePct ?? model.DefaultRatePct;
            model.NonPayingChange = snapshot.NonPayingChange ?? model.NonPayingChange;

            model.ArrearsTotal = snapshot.ArrearsTotal ?? model.ArrearsTotal;
            model.ArrearsChangePct = snapshot.ArrearsChangePct ?? model.ArrearsChangePct;

            model.CommissionReceived = snapshot.CommissionReceived ?? model.CommissionReceived;
            model.CommissionPaidToAgents = snapshot.CommissionPaidToAgents ?? model.CommissionPaidToAgents;
            model.CommissionOutstanding = snapshot.CommissionOutstanding ?? model.CommissionOutstanding;
            model.DealerCommissionOutstanding = snapshot.DealerCommissionOutstanding ?? model.DealerCommissionOutstanding;
            // CommissionsChangePct and the CommissionsReceived transaction-level
            // list intentionally left on sample data -- deferred (see
            // ScheduledDashboardRollup's class comment).

            model.BadDebtThisMonth = snapshot.BadDebtThisMonth ?? model.BadDebtThisMonth;
            model.BadDebtChangePct = snapshot.BadDebtChangePct ?? model.BadDebtChangePct;

            model.ActiveRateVsTargetPct = snapshot.ActiveRateVsTargetPct ?? model.ActiveRateVsTargetPct;

            model.PortfolioGoodPct = snapshot.PortfolioGoodPct ?? model.PortfolioGoodPct;
            model.PortfolioSlowPct = snapshot.PortfolioSlowPct ?? model.PortfolioSlowPct;
            model.PortfolioArrearsPct = snapshot.PortfolioArrearsPct ?? model.PortfolioArrearsPct;
            model.PortfolioNonPayingPct = snapshot.PortfolioNonPayingPct ?? model.PortfolioNonPayingPct;
            model.PortfolioGoodPctChange = snapshot.PortfolioGoodPctChange ?? model.PortfolioGoodPctChange;

            model.CollectionRatePct = snapshot.CollectionRatePct ?? model.CollectionRatePct;
            model.CollectionRateChangePct = snapshot.CollectionRateChangePct ?? model.CollectionRateChangePct;
            model.PortfolioAtRiskPct = snapshot.PortfolioAtRiskPct ?? model.PortfolioAtRiskPct;
            model.PortfolioAtRiskChangePct = snapshot.PortfolioAtRiskChangePct ?? model.PortfolioAtRiskChangePct;

            model.RepeatCustomerRatePct = snapshot.RepeatCustomerRatePct ?? model.RepeatCustomerRatePct;
            model.AvgCustomerLifetimeValue = snapshot.AvgCustomerLifetimeValue ?? model.AvgCustomerLifetimeValue;
            model.ChurnRatePct = snapshot.ChurnRatePct ?? model.ChurnRatePct;

            model.CompletedContractsThisMonth = snapshot.CompletedContractsThisMonth ?? model.CompletedContractsThisMonth;
            model.CompletedContractsChangePct = snapshot.CompletedContractsChangePct ?? model.CompletedContractsChangePct;
            model.ContractCompletionRatePct = snapshot.ContractCompletionRatePct ?? model.ContractCompletionRatePct;
            model.ContractCompletionRateChangePct = snapshot.ContractCompletionRateChangePct ?? model.ContractCompletionRateChangePct;
            model.AvgTimeToCompletionMonths = snapshot.AvgTimeToCompletionMonths ?? model.AvgTimeToCompletionMonths;
            model.TotalValueCompletedThisMonth = snapshot.TotalValueCompletedThisMonth ?? model.TotalValueCompletedThisMonth;
        }

        private static string MonthLabel(string yearMonth)
        {
            // yearMonth is 'YYYY-MM'; render as the existing view models expect ("Jan", "Feb", ...).
            if (DateTime.TryParseExact(yearMonth + "-01", "yyyy-MM-dd", null,
                    System.Globalization.DateTimeStyles.None, out var date))
            {
                return date.ToString("MMM");
            }

            return yearMonth;
        }

        // Null for UpsellTarget rows (not completed yet, so there's no completion date).
        private static string CompletedDateLabel(DateTime? completedDate) =>
            completedDate?.ToString("MMM d") ?? "In progress";
    }
}
