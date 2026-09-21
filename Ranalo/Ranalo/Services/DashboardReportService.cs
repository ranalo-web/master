using Ranalo.Controllers;
using Ranalo.DataStore;
using Ranalo.Models;
using Ranalo.ScheduledServices;

namespace Ranalo.Services
{
    // Assembles dashboard view models for Admin/Dealer (and, later, Agent)
    // scopes. Wired to the rollup tables so far
    // (Database/Dashboard/001_create_dashboard_tables.sql, populated by
    // ScheduledDashboardRollup once that job exists): KPI snapshot (Revenue/
    // TotalAccounts/ArrearsTotal/Portfolio %), monthly trend, and the
    // completed-contracts list. Note the rollup job itself is not currently
    // registered as a hosted service (see Program.cs) -- these figures are
    // real but only as fresh as the last manual run, not nightly-refreshed.
    // Operating Expenses/Tax Rate/Dividends Paid have no data source
    // anywhere in this app (not WooCommerce/CRM data -- corporate
    // bookkeeping entries) and Admin's ProductPerformance has no rollup
    // table yet; both still come from the sample data builder.
    //
    // Non-Payers/Slow-Payers/Good-Payers, Dealer/Agent Performance, and Cost
    // of Devices are NOT rollup-backed -- the nightly job never populates
    // DashboardWatchlistEntry or DashboardPerformanceEntry(EntryType=Dealer/
    // Agent) (see ScheduledDashboardRollup's class comment), so these are
    // built live from GetDealerAccountDetailsAsync instead (see the Build*
    // methods below) -- the same live-recompute source and Good/Slow/Arrears
    // rule the Dealer scope and the Approver Dashboard already use.
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

            // Live, system-wide (all dealers) recompute -- same source and
            // Good/Slow/Arrears rule as the Approver Dashboard
            // (GetApproverDashboardAsync), reused here instead of the old
            // rollup path (DashboardWatchlistEntry/DashboardPerformanceEntry
            // for EntryType Dealer/Agent), which is never populated -- see
            // ScheduledDashboardRollup's "NOT refreshed by this job yet"
            // note -- and was silently falling back to sample data.
            var accountDetails = await _repository.GetDealerAccountDetailsAsync(null);
            var lockClassification = await _repository.GetDealerLockClassificationAsync(null);

            // Fixes a real bug this replaces: GoodAccounts/BadAccounts/
            // PayingAccounts/NonPayingAccounts were never written by the
            // rollup either, so they stayed on sample-data counts
            // (1,583/259/1,691/151) while TotalAccounts above came from the
            // (also real) snapshot -- a real denominator against a
            // stale-mock numerator produced >100% figures ("313% good
            // standing", "334% of accounts paying").
            model.GoodAccounts = lockClassification.GoodCount;
            model.BadAccounts = lockClassification.ArrearsCount;
            model.PayingAccounts = lockClassification.GoodCount;
            model.NonPayingAccounts = lockClassification.ArrearsCount;

            // Bad Debt / Write-offs card: was never written by the rollup at
            // all (not just stale), always a flat mock constant (28,500).
            // Same live accrual-based classification and >90-day-past-lock
            // threshold as the Approver Dashboard's Bad Debt card.
            var arrearsClassification = await _repository.GetDealerArrearsClassificationAsync(null);
            model.BadDebtThisMonth = arrearsClassification.BadDebtTotal;

            model.NonPayers = BuildNonPayers(accountDetails).Select(ToAdminWatchlistEntry).ToList();
            model.SlowPayers = BuildSlowPayers(accountDetails).Select(ToAdminWatchlistEntry).ToList();
            model.GoodPayers = BuildGoodPayers(accountDetails).Select(ToAdminWatchlistEntry).ToList();

            var revenueByDealer = (await _repository.GetRevenueThisMonthByDealerAsync())
                .ToDictionary(r => r.DealerName, r => r.RevenueThisMonth);
            var commissionByDealer = (await _repository.GetDealerCommissionPaidThisMonthByDealerAsync())
                .ToDictionary(r => r.DealerName, r => r.CommissionPaidThisMonth);

            model.DealerPerformance = BuildAdminDealerPerformance(accountDetails, revenueByDealer, commissionByDealer);
            model.AgentPerformance = BuildAdminAgentPerformance(accountDetails);

            // Cost of Devices This Month: Contract_Info.BuyingPrice
            // (manually entered per-contract device cost -- see
            // DashboardAccountDetailRow's doc comment; NOT synced from
            // WooCommerce) summed for accounts that started this calendar
            // month, same period definition as NewThisMonth/RevenueThisMonth
            // above. Was a flat mock constant (38,000); accounts with no
            // BuyingPrice recorded contribute 0, same population
            // DealerCommissionMissingCostCount already tracks.
            var thisMonth = DateTime.Now;
            model.CostOfDevicesThisMonth = accountDetails
                .Where(r => r.StartDate.Year == thisMonth.Year && r.StartDate.Month == thisMonth.Month)
                .Sum(r => r.BuyingPrice ?? 0);

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

        public async Task<DealerDashboardViewModel> GetDealerDashboardAsync(int dealerId, int? agentUserId = null)
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

            // Agent Commissions card -- ApplyDealerSnapshot above set
            // CommissionOutstanding/CommissionAccountCount/
            // CommissionWithheldForArrears from the dealer-wide nightly
            // rollup (every agent's accounts pooled together), since no
            // per-agent rollup row exists (DashboardScope.ForAgent is
            // unused/unpopulated). For an Agent viewing their own dashboard,
            // override with a live recompute scoped to just their accounts --
            // same formula, see GetAgentCommissionSummaryAsync. Dealer's own
            // view (agentUserId null) keeps the rollup figures as-is.
            if (agentUserId.HasValue)
            {
                var agentCommission = await _repository.GetAgentCommissionSummaryAsync(dealerId, agentUserId.Value);
                model.CommissionOutstanding = agentCommission.CommissionOutstanding;
                model.CommissionAccountCount = agentCommission.CommissionAccountCount;
                model.CommissionWithheldForArrears = agentCommission.CommissionWithheldForArrears;

                // Performance Bonus Tracker card -- Agent Dashboard only.
                var bonusTracker = await _repository.GetAgentBonusTrackerAsync(dealerId, agentUserId.Value);
                model.CommissionPaidLifetime = bonusTracker.CommissionPaidLifetime;
                model.BonusEarnedAccountCount = bonusTracker.BonusEarnedAccountCount;
                model.BonusAtRiskAccountCount = bonusTracker.BonusAtRiskAccountCount;
                model.BonusUpcomingAccountCount = bonusTracker.BonusUpcomingAccountCount;
            }

            // Paying vs Non-Paying and My Portfolio cards -- see
            // GetDealerLockClassificationAsync for why these don't stay on
            // ApplyDealerSnapshot's PortfolioGoodPct/SlowPct/ArrearsPct/
            // NonPayingPct/InDefault (accrual-based, doesn't know about a
            // restructuring). Deliberately overrides whatever the snapshot
            // just set above -- this is a live recompute, not a rollup read,
            // and only for the Dealer scope (Admin's portfolio composition
            // still reads the accrual-based rollup, out of scope here).
            var lockClassification = await _repository.GetDealerLockClassificationAsync(dealerId, agentUserId);
            model.LockGoodCount = lockClassification.GoodCount;
            model.LockNonPayingCount = lockClassification.ArrearsCount;

            // My Portfolio's doughnut needs four mutually-exclusive slices
            // that sum to 100% -- NonPayingCount is a SUBSET of ArrearsCount
            // (>90 days is also >7 days), not a fifth bucket, so the "Arrears"
            // slice here is ArrearsCount with the Non-Paying accounts pulled
            // back out (8-90 days only). This is purely the lock-date split
            // (no accrual/restructured concept involved) -- a restructured
            // account's NextLockDate moves to the future, which already
            // places it in Good, not Arrears.
            var lockTotal = lockClassification.GoodCount + lockClassification.SlowCount + lockClassification.ArrearsCount;
            if (lockTotal > 0)
            {
                var exclusiveArrearsCount = lockClassification.ArrearsCount - lockClassification.NonPayingCount;
                model.PortfolioGoodPct = Math.Round(100m * lockClassification.GoodCount / lockTotal, 2);
                model.PortfolioSlowPct = Math.Round(100m * lockClassification.SlowCount / lockTotal, 2);
                model.PortfolioArrearsPct = Math.Round(100m * exclusiveArrearsCount / lockTotal, 2);
                model.PortfolioNonPayingPct = Math.Round(100m * lockClassification.NonPayingCount / lockTotal, 2);
            }

            // Total Arrears card -- see GetDealerArrearsClassificationAsync.
            // Overrides ArrearsTotal from ApplyDealerSnapshot above (that one
            // sums every account's accrual shortfall regardless of lock
            // status; this one only counts accounts genuinely locked).
            var arrearsClassification = await _repository.GetDealerArrearsClassificationAsync(dealerId, agentUserId);
            model.ArrearsTotal = arrearsClassification.TrueArrearsTotal;
            model.ArrearsTrueCount = arrearsClassification.TrueArrearsCount;
            model.ArrearsAvgDaysLocked = arrearsClassification.TrueArrearsAvgDaysLocked;
            model.RestructuredArrearsTotal = arrearsClassification.RestructuredArrearsTotal;
            model.RestructuredArrearsCount = arrearsClassification.RestructuredArrearsCount;

            // Bad Debt card -- subset of the same classification above.
            model.BadDebtThisMonth = arrearsClassification.BadDebtTotal;
            model.BadDebtCount = arrearsClassification.BadDebtCount;
            model.BadDebtAvgDaysLocked = arrearsClassification.BadDebtAvgDaysLocked;

            // Write-offs & Collections card -- "collections" here means
            // recoveries on already-written-off debt specifically (not the
            // dealer's overall monthly revenue, which would just duplicate
            // the Revenue card) -- see WriteOffRecoveredThisMonth's doc comment.
            model.WriteOffTotal = arrearsClassification.WriteOffTotal;
            model.WriteOffCount = arrearsClassification.WriteOffCount;
            model.WriteOffRecoveredThisMonth = arrearsClassification.WriteOffRecoveredThisMonth;
            model.WriteOffRecoveryRatePct = model.WriteOffTotal > 0
                ? Math.Round(model.WriteOffRecoveredThisMonth / model.WriteOffTotal * 100, 1)
                : 0;

            // Non-Payers/Slow-Payers/Good-Payers, Agent Performance, My
            // Contracts, and Contracts Ending Soon all come from one live
            // per-account query and are built from that same row set below --
            // see IDashboardReportRepository.GetDealerAccountDetailsAsync.
            // Replaces the old GetWatchlistAsync/GetPerformanceAsync(Agent)
            // rollup reads, which the nightly job never populates (see
            // ScheduledDashboardRollup's class comment). Fetched here (before
            // the "month" window block below) so Collection Rate/PAR30 can
            // reuse the same rows instead of a second query.
            var accountDetails = await _repository.GetDealerAccountDetailsAsync(dealerId, agentUserId);

            // ApplyDealerSnapshot's TotalAccounts comes from the (dealer-wide,
            // rollup-backed) snapshot above and isn't agent-scopable the same
            // way -- for an agent's own view, override it with the live count
            // of their own book instead, so My Portfolio's Good/Slow/Arrears
            // counts (TotalAccounts * PortfolioXPct) don't show the whole
            // dealer's account count next to a correctly agent-scoped percent.
            if (agentUserId.HasValue)
            {
                model.TotalAccounts = accountDetails.Count;
            }

            // Target isn't part of the nightly rollup (see
            // DashboardRevenuePeriodRow.TargetRevenue) -- computed live here,
            // same as the date-range filter, just always for "this month"
            // since that's what the page loads with by default.
            if (TryResolvePeriodWindow("month", out var monthWindow))
            {
                var monthRow = await _repository.GetDealerRevenueForPeriodAsync(
                    dealerId, monthWindow.PeriodStart, monthWindow.PeriodEndExclusive, monthWindow.PriorPeriodStart, monthWindow.PriorPeriodEndExclusive, agentUserId);
                model.RevenueTarget = monthRow.TargetRevenue;

                // Collection Rate / PAR30 (My Portfolio card): count-based, not
                // value-based, per the agreed definition -- of the accounts
                // that started within the selected period (the top-of-page
                // filter), what % are currently in good standing (Collection
                // Rate) vs 30+ days past their lock date (PAR30). Replaces the
                // old value-based "amount due this month, collected" formula
                // and the always-zero PortfolioAtRiskPct (never written by the
                // nightly rollup -- see UpsertSnapshotPortfolioAsync, which has
                // no portfolioAtRiskPct parameter at all).
                var (collectionRatePct, portfolioAtRiskPct) = ComputeCohortRates(
                    accountDetails, monthWindow.PeriodStart, monthWindow.PeriodEndExclusive);
                model.CollectionRatePct = collectionRatePct;
                model.PortfolioAtRiskPct = portfolioAtRiskPct;

                // Agent/Dealer Commissions cards' default (page loads with
                // "Month" selected in the top-of-page filter) -- live-updated
                // by the same AJAX call as Revenue/New Accounts when the
                // filter changes.
                model.CommissionPaidThisPeriod = await _repository.GetDealerAgentCommissionPaidForPeriodAsync(
                    dealerId, monthWindow.PeriodStart, monthWindow.PeriodEndExclusive, agentUserId);
                model.DealerCommissionPaidThisPeriod = await _repository.GetDealerCommissionPaidForPeriodAsync(
                    dealerId, monthWindow.PeriodStart, monthWindow.PeriodEndExclusive);
            }

            var trend = await _repository.GetMonthlyTrendAsync(scope);
            if (trend.Count > 0)
            {
                model.GrowthMonths = trend.Select(t => MonthLabel(t.YearMonth)).ToList();
                model.RevenueByMonth = trend.Select(t => t.Revenue).ToList();
                model.AccountsByMonth = trend.Select(t => t.AccountsCount).ToList();
            }

            model.NonPayers = BuildNonPayers(accountDetails);
            model.SlowPayers = BuildSlowPayers(accountDetails);
            model.GoodPayers = BuildGoodPayers(accountDetails);
            model.AgentPerformance = BuildAgentPerformance(accountDetails);
            model.Contracts = BuildContracts(accountDetails);
            model.ContractsEndingSoon = BuildContractsEndingSoon(accountDetails);

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

            model.DeviceStock = await BuildDeviceStockAsync(dealerId, scope);
            model.CompletedContracts = BuildCompletedContracts(await _repository.GetCompletedContractsAsync(scope, DealerScopeTakeAll));

            return model;
        }

        // Approver Dashboard: same card set and live-recompute building
        // blocks as GetDealerDashboardAsync above, just with dealerId = null
        // (every one of the widened GetDealer*Async methods treats null as
        // "no dealer filter" -- system-wide, across every dealer) instead of
        // scoped to one. A separate method rather than widening
        // GetDealerDashboardAsync's own dealerId to nullable so Dealer's and
        // Agent's paths through it are completely unchanged.
        //
        // Deliberately skips: the nightly-rollup snapshot (ApplyDealerSnapshot)
        // and its Revenue/New-Accounts change-vs-last-period figures --
        // DashboardScope only rolls up per dealer (or company-wide for Admin,
        // a different card set/purpose), not "all dealers combined" the way
        // an Approver needs; Agent/Dealer Commission cards -- not part of an
        // Approver's job; and Device Stock/Completed Contracts -- both are
        // rollup-only with no live-recompute path anywhere in this file (see
        // BuildDeviceStockAsync), so there's nothing to reuse without a much
        // larger rollup change. Adds Orders Awaiting Approval and Customers
        // to Contact, which only make sense for this scope.
        public async Task<DealerDashboardViewModel> GetApproverDashboardAsync()
        {
            var model = new DealerDashboardViewModel { DealerName = "All Dealers" };

            var lockClassification = await _repository.GetDealerLockClassificationAsync(null);
            model.LockGoodCount = lockClassification.GoodCount;
            model.LockNonPayingCount = lockClassification.ArrearsCount;

            var lockTotal = lockClassification.GoodCount + lockClassification.SlowCount + lockClassification.ArrearsCount;
            if (lockTotal > 0)
            {
                var exclusiveArrearsCount = lockClassification.ArrearsCount - lockClassification.NonPayingCount;
                model.PortfolioGoodPct = Math.Round(100m * lockClassification.GoodCount / lockTotal, 2);
                model.PortfolioSlowPct = Math.Round(100m * lockClassification.SlowCount / lockTotal, 2);
                model.PortfolioArrearsPct = Math.Round(100m * exclusiveArrearsCount / lockTotal, 2);
                model.PortfolioNonPayingPct = Math.Round(100m * lockClassification.NonPayingCount / lockTotal, 2);
            }

            var arrearsClassification = await _repository.GetDealerArrearsClassificationAsync(null);
            model.ArrearsTotal = arrearsClassification.TrueArrearsTotal;
            model.ArrearsTrueCount = arrearsClassification.TrueArrearsCount;
            model.ArrearsAvgDaysLocked = arrearsClassification.TrueArrearsAvgDaysLocked;
            model.RestructuredArrearsTotal = arrearsClassification.RestructuredArrearsTotal;
            model.RestructuredArrearsCount = arrearsClassification.RestructuredArrearsCount;

            model.BadDebtThisMonth = arrearsClassification.BadDebtTotal;
            model.BadDebtCount = arrearsClassification.BadDebtCount;
            model.BadDebtAvgDaysLocked = arrearsClassification.BadDebtAvgDaysLocked;

            model.WriteOffTotal = arrearsClassification.WriteOffTotal;
            model.WriteOffCount = arrearsClassification.WriteOffCount;
            model.WriteOffRecoveredThisMonth = arrearsClassification.WriteOffRecoveredThisMonth;
            model.WriteOffRecoveryRatePct = model.WriteOffTotal > 0
                ? Math.Round(model.WriteOffRecoveredThisMonth / model.WriteOffTotal * 100, 1)
                : 0;

            var accountDetails = await _repository.GetDealerAccountDetailsAsync(null);
            model.TotalAccounts = accountDetails.Count;

            if (TryResolvePeriodWindow("month", out var monthWindow))
            {
                var monthRow = await _repository.GetDealerRevenueForPeriodAsync(
                    null, monthWindow.PeriodStart, monthWindow.PeriodEndExclusive, monthWindow.PriorPeriodStart, monthWindow.PriorPeriodEndExclusive);
                model.RevenueThisMonth = monthRow.RevenueThisPeriod;
                model.RevenueTarget = monthRow.TargetRevenue;
                model.NewThisMonth = monthRow.NewAccountsInPeriod;

                var (collectionRatePct, portfolioAtRiskPct) = ComputeCohortRates(
                    accountDetails, monthWindow.PeriodStart, monthWindow.PeriodEndExclusive);
                model.CollectionRatePct = collectionRatePct;
                model.PortfolioAtRiskPct = portfolioAtRiskPct;
            }

            model.NonPayers = BuildNonPayers(accountDetails);
            model.SlowPayers = BuildSlowPayers(accountDetails);
            model.GoodPayers = BuildGoodPayers(accountDetails);
            model.AgentPerformance = BuildAgentPerformance(accountDetails);
            model.ContractsEndingSoon = BuildContractsEndingSoon(accountDetails);

            var revenueByDealer = (await _repository.GetRevenueThisMonthByDealerAsync())
                .ToDictionary(r => r.DealerName, r => r.RevenueThisMonth);
            model.DealerPerformance = BuildDealerPerformance(accountDetails, revenueByDealer);

            var ordersAwaitingApproval = await _repository.GetOrdersAwaitingApprovalSummaryAsync();
            model.OrdersAwaitingApprovalCount = ordersAwaitingApproval.Count;
            model.OldestPendingOrderDays = ordersAwaitingApproval.OldestPendingDays;

            // Customers to Contact: genuinely overdue accounts (same
            // LockDays > 0 population as NonPayers+SlowPayers combined),
            // worst shortfall first, so a call list can be worked top-down.
            model.CustomersToContact = accountDetails
                .Where(r => LockDays(r) > 0)
                .OrderBy(r => r.ArrearsAmount)
                .Take(30)
                .Select(r => new DealerWatchlistEntry
                {
                    CustomerName = r.CustomerName,
                    AgentName = r.AgentName ?? "",
                    Phone = r.CustomerPhone,
                    DealerName = r.DealerName ?? "",
                    Detail = $"KES {Math.Max(0, -r.ArrearsAmount):N0} overdue, {Math.Round(LockDays(r))} days",
                }).ToList();

            return model;
        }

        // A dealer's distinct device models / completed contracts are bounded
        // in practice, unlike Admin's company-wide equivalents -- fetch
        // effectively everything so the dashboard's summary-card counts and
        // the dedicated report pages (GetDealerDeviceStockReportAsync/
        // GetDealerCompletedContractsReportAsync) both see the full list
        // instead of silently truncating at the shared method's default of 20.
        private const int DealerScopeTakeAll = 1000;

        private async Task<List<DealerDeviceStock>> BuildDeviceStockAsync(int dealerId, DashboardScope scope)
        {
            var deviceStock = await _repository.GetDeviceStockAsync(scope, DealerScopeTakeAll);
            if (deviceStock.Count == 0)
            {
                return new List<DealerDeviceStock>();
            }

            // Good%/Arrears% per device come from the live NextLockDate
            // classification below, not d.GoodPct/d.ArrearsPct (accrual-based,
            // same restructuring issue as PortfolioGoodPct/InDefault) --
            // Units/AvgValue still come from the rollup row, unaffected.
            var deviceLock = await _repository.GetDealerDeviceLockClassificationAsync(dealerId);

            return deviceStock.Select(d =>
            {
                var goodPct = d.GoodPct;
                var arrearsPct = d.ArrearsPct;
                if (deviceLock.TryGetValue(d.DeviceName, out var lockBucket))
                {
                    var deviceTotal = lockBucket.GoodCount + lockBucket.ArrearsCount;
                    if (deviceTotal > 0)
                    {
                        goodPct = Math.Round(100m * lockBucket.GoodCount / deviceTotal, 1);
                        arrearsPct = Math.Round(100m * lockBucket.ArrearsCount / deviceTotal, 1);
                    }
                }

                return new DealerDeviceStock
                {
                    Device = d.DeviceName,
                    Units = d.Units,
                    AvgValue = d.AvgValue,
                    GoodPct = goodPct,
                    ArrearsPct = arrearsPct,
                };
            }).ToList();
        }

        private static List<DealerCompletedContract> BuildCompletedContracts(List<DashboardCompletedContractRow> rows) =>
            rows.Select(c => new DealerCompletedContract
            {
                CustomerName = c.CustomerName,
                ProductName = c.ProductName,
                CompletedDate = CompletedDateLabel(c.CompletedDate),
                TotalPaid = c.TotalPaid,
                DurationMonths = c.DurationMonths,
                Status = c.Status,
                PctComplete = c.PctComplete,
            }).ToList();

        // Non-Payers/Slow-Payers/Good-Payers: same >7/1-7/<=0 days-past-lock
        // split as DashboardReportRepository's ClassifyLock (the exact rule
        // behind the Paying-vs-Non-Paying top card), just per account instead
        // of aggregated into counts.
        private static List<DealerWatchlistEntry> BuildNonPayers(List<DashboardAccountDetailRow> rows) => rows
            .Where(r => LockDays(r) > 7)
            .Select(r => new DealerWatchlistEntry
            {
                CustomerName = r.CustomerName,
                AgentName = r.AgentName ?? "",
                DealerName = r.DealerName,
                Phone = r.CustomerPhone,
                Detail = $"{Math.Round(LockDays(r))} days overdue",
            }).ToList();

        private static List<DealerWatchlistEntry> BuildSlowPayers(List<DashboardAccountDetailRow> rows) => rows
            .Where(r => LockDays(r) is > 0 and <= 7)
            .Select(r => new DealerWatchlistEntry
            {
                CustomerName = r.CustomerName,
                AgentName = r.AgentName ?? "",
                DealerName = r.DealerName,
                Phone = r.CustomerPhone,
                Detail = $"KES {Math.Max(0, -r.ArrearsAmount):N0} due",
            }).ToList();

        private static List<DealerWatchlistEntry> BuildGoodPayers(List<DashboardAccountDetailRow> rows) => rows
            .Where(r => LockDays(r) <= 0)
            .Select(r => new DealerWatchlistEntry
            {
                CustomerName = r.CustomerName,
                AgentName = r.AgentName ?? "",
                DealerName = r.DealerName,
                Phone = r.CustomerPhone,
                Detail = $"{PaymentsAhead(r)} payments ahead",
            }).ToList();

        // Surplus (paid ahead of the accrual schedule) expressed as a whole
        // number of months, using MonthlyPayment as the unit shown elsewhere
        // on this page rather than the raw daily-blended rate.
        private static int PaymentsAhead(DashboardAccountDetailRow r) =>
            r.MonthlyPayment > 0 ? Math.Max(0, (int)Math.Round(r.ArrearsAmount / r.MonthlyPayment)) : 0;

        private static double LockDays(DashboardAccountDetailRow r) =>
            DashboardReportRepository.DaysPastLock(r.NextLockDateRaw);

        // My Portfolio's Collection Rate / PAR30, count-based and scoped to
        // whichever period the top-of-page filter has selected: of the
        // accounts that STARTED within that window (same cohort concept as
        // DashboardRevenuePeriodRow.NewAccountsInPeriod), what % are currently
        // in good standing vs 30+ days past their lock date. Deliberately a
        // cohort of new enrollments, not "who was locked when" -- NextLockDate
        // has no history (see GetDealerLockClassificationAsync's doc comment),
        // so StartDate is the only period-safe dimension available live.
        private static (decimal CollectionRatePct, decimal PortfolioAtRiskPct) ComputeCohortRates(
            List<DashboardAccountDetailRow> accountDetails, DateTime periodStart, DateTime periodEndExclusive)
        {
            var cohort = accountDetails
                .Where(a => a.StartDate >= periodStart && a.StartDate < periodEndExclusive)
                .ToList();

            if (cohort.Count == 0)
            {
                return (0, 0);
            }

            var good = cohort.Count(a => LockDays(a) <= 0);
            var atRisk = cohort.Count(a => LockDays(a) >= 30);

            return (
                Math.Round(100m * good / cohort.Count, 1),
                Math.Round(100m * atRisk / cohort.Count, 1)
            );
        }

        // Agent Performance: grouped by AssignedAgentId, skipping unassigned
        // accounts. ActivePct mirrors the dealer-wide Paying-vs-Non-Paying
        // ratio (Good / (Good + Arrears), Slow excluded -- see
        // Index.cshtml's lockGoodPct). PctOfTarget is the whole book instead
        // (Slow included in the denominator): "if 100%, none of this agent's
        // accounts are in default" -- counted by accounts, not value, per
        // the agreed definition.
        private static List<DealerAgentPerformance> BuildAgentPerformance(List<DashboardAccountDetailRow> rows) => rows
            .Where(r => r.AssignedAgentId.HasValue)
            .GroupBy(r => r.AssignedAgentId!.Value)
            .Select(g =>
            {
                var total = g.Count();
                var good = g.Count(r => LockDays(r) <= 0);
                var arrears = g.Count(r => LockDays(r) > 7);
                var activeDenominator = good + arrears;
                return new DealerAgentPerformance
                {
                    AgentName = g.First().AgentName ?? "",
                    Accounts = total,
                    ActivePct = activeDenominator > 0 ? Math.Round(100m * good / activeDenominator, 1) : 0,
                    PctOfTarget = total > 0 ? Math.Round(100m * (total - arrears) / total, 1) : 0,
                };
            })
            .OrderByDescending(a => a.Accounts)
            .Select((a, i) => { a.Rank = i + 1; return a; })
            .ToList();

        // Dealer Performance (Approver Dashboard only): same Active% shape as
        // Agent Performance, grouped by DealerName instead of AssignedAgentId
        // -- a system-wide "My Contracts" listing has no natural owner for an
        // Approver overseeing every dealer, so this ranking replaces it there.
        // ArrearsTotal is each dealer's accrual shortfall summed across its
        // accounts (same ArrearsAmount formula as everywhere else on this
        // page); RevenueThisMonth comes from GetRevenueThisMonthByDealerAsync
        // since accountDetails only carries account-level data, not payments.
        private static List<DealerPerformance> BuildDealerPerformance(
            List<DashboardAccountDetailRow> rows, Dictionary<string, decimal> revenueByDealer) => rows
            .Where(r => !string.IsNullOrWhiteSpace(r.DealerName))
            .GroupBy(r => r.DealerName!)
            .Select(g =>
            {
                var total = g.Count();
                var good = g.Count(r => LockDays(r) <= 0);
                var arrears = g.Count(r => LockDays(r) > 7);
                var activeDenominator = good + arrears;
                return new DealerPerformance
                {
                    DealerName = g.Key,
                    Accounts = total,
                    ActivePct = activeDenominator > 0 ? Math.Round(100m * good / activeDenominator, 1) : 0,
                    ArrearsTotal = g.Sum(r => Math.Max(0, -r.ArrearsAmount)),
                    RevenueThisMonth = revenueByDealer.TryGetValue(g.Key, out var rev) ? rev : 0,
                };
            })
            .OrderByDescending(d => d.Accounts)
            .Select((d, i) => { d.Rank = i + 1; return d; })
            .ToList();

        // Admin Dashboard's Agent Performance: same grouping/formulas as
        // BuildAgentPerformance above, plus DealerName -- Admin's table
        // spans every dealer at once (unlike the Dealer scope's own Agent
        // Performance, already scoped to one dealer), so the dealer column
        // is needed to tell agents apart.
        private static List<AdminAgentPerformance> BuildAdminAgentPerformance(List<DashboardAccountDetailRow> rows) => rows
            .Where(r => r.AssignedAgentId.HasValue)
            .GroupBy(r => r.AssignedAgentId!.Value)
            .Select(g =>
            {
                var total = g.Count();
                var good = g.Count(r => LockDays(r) <= 0);
                var arrears = g.Count(r => LockDays(r) > 7);
                var activeDenominator = good + arrears;
                return new AdminAgentPerformance
                {
                    AgentName = g.First().AgentName ?? "",
                    DealerName = g.First().DealerName ?? "",
                    Accounts = total,
                    ActivePct = activeDenominator > 0 ? Math.Round(100m * good / activeDenominator, 1) : 0,
                    PctOfTarget = total > 0 ? Math.Round(100m * (total - arrears) / total, 1) : 0,
                };
            })
            .OrderByDescending(a => a.Accounts)
            .Select((a, i) => { a.Rank = i + 1; return a; })
            .ToList();

        // Admin Dashboard's Dealer Performance: same grouping/formulas as
        // BuildDealerPerformance above, plus commissions -- CommissionPaid
        // is this month's real DealerCommissionPayments total (see
        // GetDealerCommissionPaidThisMonthByDealerAsync); CommissionDue has
        // no live system-wide "outstanding" source yet (would need the full
        // per-account dealer-commission formula -- 30% of lifetime
        // TotalPaid-BuyingPrice-AgentGrossCommission -- replicated across
        // every dealer, a separate feature), so it's approximated as fully
        // settled (== CommissionPaid) rather than show a fabricated balance.
        private static List<AdminDealerPerformance> BuildAdminDealerPerformance(
            List<DashboardAccountDetailRow> rows,
            Dictionary<string, decimal> revenueByDealer,
            Dictionary<string, decimal> commissionByDealer) => rows
            .Where(r => !string.IsNullOrWhiteSpace(r.DealerName))
            .GroupBy(r => r.DealerName!)
            .Select(g =>
            {
                var total = g.Count();
                var good = g.Count(r => LockDays(r) <= 0);
                var arrears = g.Count(r => LockDays(r) > 7);
                var activeDenominator = good + arrears;
                var commissionPaid = commissionByDealer.TryGetValue(g.Key, out var cp) ? cp : 0;
                return new AdminDealerPerformance
                {
                    DealerName = g.Key,
                    Accounts = total,
                    ActivePct = activeDenominator > 0 ? Math.Round(100m * good / activeDenominator, 1) : 0,
                    Revenue = revenueByDealer.TryGetValue(g.Key, out var rev) ? rev : 0,
                    CommissionPaid = commissionPaid,
                    CommissionDue = commissionPaid,
                    PctOfTarget = total > 0 ? Math.Round(100m * (total - arrears) / total, 1) : 0,
                };
            })
            .OrderByDescending(d => d.Accounts)
            .Select((d, i) => { d.Rank = i + 1; return d; })
            .ToList();

        // My Contracts: every active account, current status/next-due from
        // the same lock classification as everywhere else on this page.
        private static List<DealerContract> BuildContracts(List<DashboardAccountDetailRow> rows) => rows
            .Select(r => new DealerContract
            {
                CustomerName = r.CustomerName,
                AgentName = r.AgentName ?? "",
                Device = r.DeviceName,
                MonthlyPayment = r.MonthlyPayment,
                Status = LockDays(r) > 7 ? "Late" : "On Track",
                NextDue = FormatNextLockDate(r.NextLockDateRaw),
                DaysLeft = "",
            }).ToList();

        // Contracts Ending Soon (redefined by the dealer): not in arrears
        // (Good or Slow) AND 80%+ of the contract's full value paid off --
        // same >=80% threshold RefreshCompletedContractsAsync uses for its
        // "UpsellTarget" status, just also requiring good standing.
        private static List<DealerContract> BuildContractsEndingSoon(List<DashboardAccountDetailRow> rows) => rows
            .Where(r => r.FullContractValue > 0 && LockDays(r) <= 7)
            .Select(r => new { r, pctComplete = Math.Min(100m, 100m * r.TotalPaid / r.FullContractValue) })
            .Where(x => x.pctComplete >= 80)
            .Select(x => new DealerContract
            {
                CustomerName = x.r.CustomerName,
                AgentName = x.r.AgentName ?? "",
                Device = x.r.DeviceName,
                MonthlyPayment = x.r.MonthlyPayment,
                Status = "Near Completion",
                NextDue = FormatNextLockDate(x.r.NextLockDateRaw),
                DaysLeft = "",
                PctComplete = Math.Round(x.pctComplete, 1),
            }).ToList();

        private static string FormatNextLockDate(string? nextLockDateRaw)
        {
            var parsed = DashboardReportRepository.ParseNextLockDate(nextLockDateRaw);
            return parsed.HasValue ? parsed.Value.ToString("MMM d") : "-";
        }

        public async Task<DealerRevenuePeriodResult?> GetDealerRevenueForPeriodAsync(int? dealerId, string period, int? agentUserId = null)
        {
            if (!TryResolvePeriodWindow(period, out var window))
            {
                return null;
            }

            var row = await _repository.GetDealerRevenueForPeriodAsync(
                dealerId, window.PeriodStart, window.PeriodEndExclusive, window.PriorPeriodStart, window.PriorPeriodEndExclusive, agentUserId);

            // Commission cards and Completed Contracts are hidden on the
            // Approver Dashboard (see GetApproverDashboardAsync's doc
            // comment) -- dealerId is only null for that scope, so skip the
            // dealer-only queries backing them entirely rather than widening
            // GetDealerAgentCommissionPaidForPeriodAsync/
            // GetDealerCommissionPaidForPeriodAsync/GetCompletedContractsAsync
            // for a result nothing would render.
            var commissionPaidThisPeriod = dealerId.HasValue
                ? await _repository.GetDealerAgentCommissionPaidForPeriodAsync(dealerId.Value, window.PeriodStart, window.PeriodEndExclusive, agentUserId)
                : 0m;
            var dealerCommissionPaidThisPeriod = dealerId.HasValue
                ? await _repository.GetDealerCommissionPaidForPeriodAsync(dealerId.Value, window.PeriodStart, window.PeriodEndExclusive)
                : 0m;

            // Reuses the same rows the Completed Contracts report page shows
            // (GetDealerCompletedContractsReportAsync) -- no separate query,
            // just a date-range filter on Status == Completed. Stays
            // dealer-wide (see GetDealerDeviceStockReportAsync's doc note).
            var completedContractsInPeriod = 0;
            if (dealerId.HasValue)
            {
                var completedContracts = await _repository.GetCompletedContractsAsync(DashboardScope.ForDealer(dealerId.Value), DealerScopeTakeAll);
                completedContractsInPeriod = completedContracts.Count(c =>
                    c.Status == DashboardCompletedContractStatus.Completed
                    && c.CompletedDate.HasValue
                    && c.CompletedDate.Value >= window.PeriodStart
                    && c.CompletedDate.Value < window.PeriodEndExclusive);
            }

            // Collection Rate / PAR30 for the selected period's cohort -- see
            // ComputeCohortRates.
            var accountDetails = await _repository.GetDealerAccountDetailsAsync(dealerId, agentUserId);
            var (collectionRatePct, portfolioAtRiskPct) = ComputeCohortRates(
                accountDetails, window.PeriodStart, window.PeriodEndExclusive);

            return new DealerRevenuePeriodResult
            {
                Revenue = row.RevenueThisPeriod,
                GrowthPct = ScheduledDashboardRollup.CalculateGrowthPct(row.RevenueThisPeriod, row.RevenueLastPeriod),
                AvgPerAccount = row.TotalAccounts > 0 ? row.RevenueThisPeriod / row.TotalAccounts : 0,
                TargetRevenue = row.TargetRevenue,
                Label = window.Label,
                TotalAccounts = row.TotalAccountsAsOfPeriod,
                NewInPeriod = row.NewAccountsInPeriod,
                NewInPeriodChangePct = ScheduledDashboardRollup.CalculateGrowthPct(row.NewAccountsInPeriod, row.NewAccountsPriorPeriod),
                CommissionPaidThisPeriod = commissionPaidThisPeriod,
                DealerCommissionPaidThisPeriod = dealerCommissionPaidThisPeriod,
                CompletedContractsInPeriod = completedContractsInPeriod,
                CollectionRatePct = collectionRatePct,
                PortfolioAtRiskPct = portfolioAtRiskPct,
            };
        }

        // Backs the dedicated report pages linked from the Dealer Dashboard's
        // summary cards -- each fetches only what its own page needs (either
        // the shared account-detail query, or the existing device-stock/
        // completed-contract sources), rather than the whole dashboard.
        public async Task<List<DealerWatchlistEntry>> GetDealerNonPayersAsync(int dealerId, int? agentUserId = null) =>
            BuildNonPayers(await _repository.GetDealerAccountDetailsAsync(dealerId, agentUserId));

        public async Task<List<DealerWatchlistEntry>> GetDealerSlowPayersAsync(int dealerId, int? agentUserId = null) =>
            BuildSlowPayers(await _repository.GetDealerAccountDetailsAsync(dealerId, agentUserId));

        public async Task<List<DealerWatchlistEntry>> GetDealerGoodPayersAsync(int dealerId, int? agentUserId = null) =>
            BuildGoodPayers(await _repository.GetDealerAccountDetailsAsync(dealerId, agentUserId));

        public async Task<List<DealerAgentPerformance>> GetDealerAgentPerformanceAsync(int dealerId, int? agentUserId = null) =>
            BuildAgentPerformance(await _repository.GetDealerAccountDetailsAsync(dealerId, agentUserId));

        public async Task<List<DealerContract>> GetDealerContractsAsync(int dealerId, int? agentUserId = null) =>
            BuildContracts(await _repository.GetDealerAccountDetailsAsync(dealerId, agentUserId));

        public async Task<List<DealerContract>> GetDealerContractsEndingSoonAsync(int dealerId, int? agentUserId = null) =>
            BuildContractsEndingSoon(await _repository.GetDealerAccountDetailsAsync(dealerId, agentUserId));

        public Task<List<DealerDeviceStock>> GetDealerDeviceStockReportAsync(int dealerId) =>
            BuildDeviceStockAsync(dealerId, DashboardScope.ForDealer(dealerId));

        public async Task<List<DealerCompletedContract>> GetDealerCompletedContractsReportAsync(int dealerId) =>
            BuildCompletedContracts(await _repository.GetCompletedContractsAsync(DashboardScope.ForDealer(dealerId), DealerScopeTakeAll));

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
                    window = new PeriodWindow(yearWindowStart, tomorrow, yearWindowStart.AddYears(-1), yearWindowStart, "last 12 months");
                    return true;

                default:
                    window = default;
                    return false;
            }
        }

        private static AdminWatchlistEntry ToAdminWatchlistEntry(DealerWatchlistEntry entry) => new()
        {
            CustomerName = entry.CustomerName,
            DealerName = entry.DealerName ?? "",
            Phone = entry.Phone ?? "",
            Detail = entry.Detail,
        };

        private static void ApplyAdminSnapshot(AdminDashboardViewModel model, DashboardSnapshotRow snapshot)
        {
            model.RevenueThisMonth = snapshot.RevenueThisMonth ?? model.RevenueThisMonth;
            model.RevenueGrowthPct = snapshot.RevenueGrowthPct ?? model.RevenueGrowthPct;
            model.RevenueTargetThisMonth = snapshot.RevenueTargetThisMonth ?? model.RevenueTargetThisMonth;

            model.TotalAccounts = snapshot.TotalAccounts ?? model.TotalAccounts;
            // GoodAccounts/BadAccounts/PayingAccounts/NonPayingAccounts are
            // set later in GetAdminDashboardAsync from the live lock
            // classification, not from this snapshot (the rollup never
            // wrote them, so they'd otherwise stay on stale sample counts
            // against a real TotalAccounts -- see that method's comment).
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
            model.CommissionAccountCount = snapshot.CommissionAccountCount ?? model.CommissionAccountCount;
            model.CommissionWithheldForArrears = snapshot.CommissionWithheldForArrears ?? model.CommissionWithheldForArrears;
            model.DealerCommissionAccountCount = snapshot.DealerCommissionAccountCount ?? model.DealerCommissionAccountCount;
            model.DealerCommissionMissingCostCount = snapshot.DealerCommissionMissingCostCount ?? model.DealerCommissionMissingCostCount;
            model.DealerCommissionWithheldForArrears = snapshot.DealerCommissionWithheldForArrears ?? model.DealerCommissionWithheldForArrears;
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
