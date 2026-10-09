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
        private readonly IOperatingExpenseRepository _operatingExpenseRepository;

        public DashboardReportService(IDashboardReportRepository repository, IOperatingExpenseRepository operatingExpenseRepository)
        {
            _repository = repository;
            _operatingExpenseRepository = operatingExpenseRepository;
        }

        public async Task<AdminDashboardViewModel> GetAdminDashboardAsync()
        {
            // Empty defaults, not AdminDashboardSampleData.Build(): anything
            // without a real source shows 0 or is hidden, never a made-up
            // sample figure. The "vs last month" figures that have no history
            // yet stay null and the view leaves them out.
            var model = new AdminDashboardViewModel();
            var scope = DashboardScope.Admin;

            var snapshot = await _repository.GetSnapshotAsync(scope);
            if (snapshot != null)
            {
                ApplyAdminSnapshot(model, snapshot);
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

            // Portfolio Composition doughnut: same live lock-date split and
            // NonPaying-is-a-subset-of-Arrears convention as the Dealer
            // Dashboard's My Portfolio card (GetDealerDashboardAsync) --
            // was left on the stale accrual-based rollup (PortfolioGoodPct
            // etc. from ApplyAdminSnapshot) as an explicit out-of-scope call
            // when Dealer's copy was made live. Reuses lockClassification,
            // already fetched above -- no new query.
            var lockTotal = lockClassification.GoodCount + lockClassification.SlowCount + lockClassification.ArrearsCount;
            if (lockTotal > 0)
            {
                var exclusiveArrearsCount = lockClassification.ArrearsCount - lockClassification.NonPayingCount;
                model.PortfolioGoodPct = Math.Round(100m * lockClassification.GoodCount / lockTotal, 2);
                model.PortfolioSlowPct = Math.Round(100m * lockClassification.SlowCount / lockTotal, 2);
                model.PortfolioArrearsPct = Math.Round(100m * exclusiveArrearsCount / lockTotal, 2);
                model.PortfolioNonPayingPct = Math.Round(100m * lockClassification.NonPayingCount / lockTotal, 2);
            }

            // Collection Rate / PAR30 and Revenue Target/Growth: same live
            // "month" window the top-of-page period filter's AJAX endpoint
            // already computes on demand -- the initial page load never
            // called it, so these stayed on the stale rollup snapshot until
            // a user manually touched the filter. RevenueThisMonth itself
            // is left alone (already live via GetRevenueThisMonthByDealerAsync
            // above); this only fills in the two figures that had no live
            // source at all on load: the target and the month-over-month
            // growth %.
            if (TryResolvePeriodWindow("month", out var monthWindow))
            {
                var (collectionRatePct, portfolioAtRiskPct) = ComputeCohortRates(
                    accountDetails, monthWindow.PeriodStart, monthWindow.PeriodEndExclusive);
                model.CollectionRatePct = collectionRatePct;
                model.PortfolioAtRiskPct = portfolioAtRiskPct;

                var monthRevenueRow = await _repository.GetDealerRevenueForPeriodAsync(
                    null, monthWindow.PeriodStart, monthWindow.PeriodEndExclusive, monthWindow.PriorPeriodStart, monthWindow.PriorPeriodEndExclusive);
                model.RevenueTargetThisMonth = monthRevenueRow.TargetRevenue;

                // Revenue: live, with the Financials definition (every device
                // payment) -- the nightly snapshot lagged a day and left out
                // devices with no dealer mapping. Same figures as the period
                // filter's "Month".
                model.RevenueThisMonth = await _repository.GetRevenueForPeriodAsync(monthWindow.PeriodStart, monthWindow.PeriodEndExclusive);
                var revenueLastMonth = await _repository.GetRevenueForPeriodAsync(monthWindow.PriorPeriodStart, monthWindow.PriorPeriodEndExclusive);
                model.RevenueGrowthPct = ScheduledDashboardRollup.CalculateGrowthPct(model.RevenueThisMonth, revenueLastMonth) ?? 0;

                // Total Accounts card: headline and "new this month" delta
                // were both still on the stale rollup snapshot (never
                // overridden anywhere in this method). TotalAccounts now
                // matches lockTotal/accountDetails.Count (same population,
                // guaranteed consistent with the Good/Bad/Slow breakdown
                // shown on the same card). NewThisMonth reuses this same
                // monthRevenueRow so the initial page load shows the exact
                // same number as manually selecting "Month" in the period
                // filter does -- previously these were two disconnected
                // calculations that could disagree.
                model.TotalAccounts = lockTotal;
                model.NewThisMonth = monthRevenueRow.NewAccountsInPeriod;
                model.NewThisMonthChangePct = ScheduledDashboardRollup.CalculateGrowthPct(monthRevenueRow.NewAccountsInPeriod, monthRevenueRow.NewAccountsPriorPeriod);
            }

            // Total Arrears / Bad Debt cards: same live "true arrears" call
            // as the Dealer/Approver Dashboards (GetDealerArrearsClassificationAsync
            // -- only accounts genuinely locked past NextLockDate, not every
            // account's accrual shortfall). Previously only BadDebtThisMonth
            // was wired to this; ArrearsTotal was left on the stale rollup
            // snapshot's older, broader accrual-based definition, so the two
            // cards silently disagreed with each other and with the
            // Approver Dashboard's live figure for the same thing. Bad Debt
            // is a subset of this same classification (>90 days past lock),
            // so the two cards are now guaranteed consistent with each other.
            var arrearsClassification = await _repository.GetDealerArrearsClassificationAsync(null);
            model.ArrearsTotal = arrearsClassification.TrueArrearsTotal;
            model.ArrearsTrueCount = arrearsClassification.TrueArrearsCount;
            model.BadDebtThisMonth = arrearsClassification.BadDebtTotal;

            model.NonPayers = BuildNonPayers(accountDetails).Select(ToAdminWatchlistEntry).ToList();
            model.SlowPayers = BuildSlowPayers(accountDetails).Select(ToAdminWatchlistEntry).ToList();
            model.GoodPayers = BuildGoodPayers(accountDetails).Select(ToAdminWatchlistEntry).ToList();

            var revenueByDealer = (await _repository.GetRevenueThisMonthByDealerAsync())
                .ToDictionary(r => r.DealerName, r => r.RevenueThisMonth);
            var commissionByDealer = (await _repository.GetDealerCommissionPaidThisMonthByDealerAsync())
                .ToDictionary(r => r.DealerName, r => r.CommissionPaidThisMonth);

            // Commission figures, live and company-wide, through the same
            // CommissionPayees summaries as Pay Commissions.
            var commissionAccounts = await _repository.GetCommissionAccountsAsync(null);
            var dealerCommission = CommissionPayees.Dealers(commissionAccounts);
            var commissionDueByDealer = dealerCommission
                .GroupBy(d => d.Name)
                .ToDictionary(g => g.Key, g => g.Sum(d => d.Payable));
            model.DealersSuspended = dealerCommission.Count(d => d.IsSuspended);
            model.DealerCommissionPayable = dealerCommission.Sum(d => d.Payable);
            model.DealerCommissionHeld = dealerCommission.Where(d => d.IsSuspended).Sum(d => d.Pool.Owed);

            model.DealerPerformance = BuildAdminDealerPerformance(accountDetails, revenueByDealer, commissionByDealer, commissionDueByDealer);
            model.AgentPerformance = BuildAdminAgentPerformance(accountDetails);
            model.Collections = BuildCollections(accountDetails);
            (model.CommissionSummary, model.CommissionAccounts) = BuildCommissions(commissionAccounts, accountDetails);

            // Commissions Paid card: agent payouts this month join the
            // per-dealer dealer payouts summed in the view.
            var monthStart = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            model.AgentCommissionPaidThisMonth = await _repository.GetDealerAgentCommissionPaidForPeriodAsync(
                null, monthStart, monthStart.AddMonths(1));

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

            // Growth chart: the last 8 months, live. Revenue uses the
            // Financials definition (every device payment); accounts is how
            // many accounts had started by the end of each month.
            const int trendMonths = 8;
            var trendStart = new DateTime(thisMonth.Year, thisMonth.Month, 1).AddMonths(-(trendMonths - 1));
            var revenueByMonth = (await _repository.GetRevenueByMonthAsync(trendMonths))
                .ToDictionary(r => (r.Year, r.Month), r => r.Total);
            model.GrowthMonths = new List<string>();
            model.RevenueByMonth = new List<decimal>();
            model.AccountsByMonth = new List<int>();
            for (var i = 0; i < trendMonths; i++)
            {
                var month = trendStart.AddMonths(i);
                model.GrowthMonths.Add(month.ToString("MMM"));
                model.RevenueByMonth.Add(revenueByMonth.TryGetValue((month.Year, month.Month), out var rev) ? rev : 0);
                model.AccountsByMonth.Add(accountDetails.Count(r => r.StartDate < month.AddMonths(1)));
            }

            // Customer Performance: one customer per account (the app has no
            // customer identity shared across accounts), so repeat-customer
            // and churn rates have no source yet and stay null (hidden).
            model.TotalCustomers = accountDetails.Select(r => r.AccountId).Distinct().Count();
            model.NewCustomersThisMonth = model.NewThisMonth;
            model.AvgCustomerLifetimeValue = model.TotalCustomers > 0
                ? Math.Round(accountDetails.Sum(r => r.TotalPaid) / model.TotalCustomers, 0)
                : 0;

            // Product Performance: accounts grouped by device model -- units,
            // average contract value, what customers have paid, and the share
            // more than 7 days past their lock date.
            model.ProductPerformance = accountDetails
                .GroupBy(r => string.IsNullOrWhiteSpace(r.DeviceName) ? "Unknown device" : r.DeviceName.Trim())
                .Select(g => new AdminProductPerformance
                {
                    ProductName = g.Key,
                    UnitsFinanced = g.Count(),
                    AvgValue = Math.Round(g.Average(r => r.FullContractValue), 0),
                    Revenue = g.Sum(r => r.TotalPaid),
                    DefaultRatePct = Math.Round(100m * g.Count(r => LockDays(r) > CommissionPayoutRules.LockToleranceDays) / g.Count(), 1),
                })
                .OrderByDescending(p => p.UnitsFinanced)
                .ThenByDescending(p => p.Revenue)
                .Select((p, i) => { p.Rank = i + 1; return p; })
                .ToList();

            var completedContracts = await _repository.GetCompletedContractsAsync(scope, DealerScopeTakeAll);

            // Completed Contracts card: paid off this month vs last month,
            // value and average duration. The completion rate has no agreed
            // definition yet and stays null (hidden).
            var paidOff = completedContracts.Where(c => c.Status == DashboardCompletedContractStatus.Completed && c.CompletedDate.HasValue).ToList();
            var paidOffThisMonth = paidOff.Where(c => c.CompletedDate >= monthStart && c.CompletedDate < monthStart.AddMonths(1)).ToList();
            var paidOffLastMonth = paidOff.Count(c => c.CompletedDate >= monthStart.AddMonths(-1) && c.CompletedDate < monthStart);
            model.CompletedContractsThisMonth = paidOffThisMonth.Count;
            model.CompletedContractsChangePct = ScheduledDashboardRollup.CalculateGrowthPct(paidOffThisMonth.Count, paidOffLastMonth);
            model.TotalValueCompletedThisMonth = paidOffThisMonth.Sum(c => c.TotalPaid);
            model.AvgTimeToCompletionMonths = paidOff.Count > 0 ? Math.Round((decimal)paidOff.Average(c => c.DurationMonths), 1) : 0;

            if (completedContracts.Count > 0)
            {
                model.CompletedContracts = completedContracts.Take(20).Select(c => new AdminCompletedContract
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

            return model;
        }

        // Financials page -- moved off the Admin Dashboard so the P&L could
        // get room for a monthly comparison chart and a balance sheet. This
        // month's figures reuse the exact live sources GetAdminDashboardAsync
        // already wired up (Cost of Devices from BuyingPrice, Commissions
        // from DealerCommissionPayments, Bad Debt from the live arrears
        // classification) instead of the rollup snapshot, so every number on
        // this page is consistently live, not a mix of live and
        // nightly-stale.
        // The Income Statement covers [fromDate, toDateExclusive) -- null
        // bounds mean unbounded (All Time). The *ThisMonth properties keep
        // their names but hold totals for the requested period; the monthly
        // trend and balance sheet below are unaffected by the period.
        public async Task<FinancialsViewModel> GetFinancialsAsync(DateTime? fromDate, DateTime? toDateExclusive)
        {
            var model = new FinancialsViewModel();
            var now = DateTime.Now;

            var accountDetails = await _repository.GetDealerAccountDetailsAsync(null);

            model.RevenueThisMonth = await _repository.GetRevenueForPeriodAsync(fromDate, toDateExclusive);
            model.CommissionsPaidThisMonth = await _repository.GetCommissionsPaidForPeriodAsync(fromDate, toDateExclusive);

            model.CostOfDevicesThisMonth = accountDetails
                .Where(r => (fromDate == null || r.StartDate >= fromDate) && (toDateExclusive == null || r.StartDate < toDateExclusive))
                .Sum(r => r.BuyingPrice ?? 0);

            // Point-in-time classification -- there's no historical recompute,
            // so this is the current bad-debt figure whatever the period.
            var arrearsClassification = await _repository.GetDealerArrearsClassificationAsync(null);
            model.BadDebtThisMonth = arrearsClassification.BadDebtTotal;

            model.OperatingExpensesThisMonth = await _operatingExpenseRepository.GetTotalAsync(fromDate, toDateExclusive);

            var thisMonthStart = new DateTime(now.Year, now.Month, 1);

            // Kenya's standard resident corporate income tax rate (KRA) --
            // confirmed current as of 2026, not a placeholder. No dividends
            // have been paid to date (confirmed by the business).
            model.TaxRatePct = 30m;
            model.DividendsPaidThisMonth = 0m;

            // Monthly comparison chart (last 12 months). Cost of Devices is
            // filtered in-memory from the already-fetched accountDetails
            // (same pattern BuildDealerPerformance etc. already use) rather
            // than a second query. Bad Debt and Tax are deliberately not
            // part of this trend -- see FinancialsMonthRow's doc comment.
            const int trendMonths = 12;
            var revenueByMonth = (await _repository.GetRevenueByMonthAsync(trendMonths))
                .ToDictionary(r => (r.Year, r.Month), r => r.Total);
            var commissionsByMonth = (await _repository.GetCommissionsPaidByMonthAsync(trendMonths))
                .ToDictionary(r => (r.Year, r.Month), r => r.Total);
            var opexByMonth = await _operatingExpenseRepository.GetMonthlyTotalsAsync(trendMonths);
            var opexByMonthMap = opexByMonth.ToDictionary(r => (r.Year, r.Month), r => r.Total);

            var trendStart = thisMonthStart.AddMonths(-(trendMonths - 1));
            for (var i = 0; i < trendMonths; i++)
            {
                var monthStart = trendStart.AddMonths(i);
                var monthEnd = monthStart.AddMonths(1);
                var key = (monthStart.Year, monthStart.Month);

                var revenue = revenueByMonth.TryGetValue(key, out var rev) ? rev : 0;
                var commissions = commissionsByMonth.TryGetValue(key, out var comm) ? comm : 0;
                var costOfDevices = accountDetails
                    .Where(r => r.StartDate >= monthStart && r.StartDate < monthEnd)
                    .Sum(r => r.BuyingPrice ?? 0);
                var opex = opexByMonthMap.TryGetValue(key, out var opexTotal) ? opexTotal : 0;

                model.MonthlyTrend.Add(new FinancialsMonthRow
                {
                    MonthLabel = monthStart.ToString("MMM yyyy"),
                    Revenue = revenue,
                    CostOfDevices = costOfDevices,
                    CommissionsPaid = commissions,
                    OperatingExpenses = opex,
                    NetBeforeTax = revenue - costOfDevices - commissions - opex,
                    HasOperatingExpenses = opex > 0,
                });
            }

            // Balance sheet (best-effort -- see FinancialsViewModel's doc
            // comment on why this app has no Cash/Inventory figures to show).
            model.LoanReceivablesGross = accountDetails.Sum(r => Math.Max(0, r.FullContractValue - r.TotalPaid));

            // Commissions payable: live from the same figures as Pay
            // Commissions (the nightly snapshot lags a day). A liability, so
            // it includes commission held for suspended dealers/agents --
            // still owed, just not payable yet.
            var commissionAccounts = await _repository.GetCommissionAccountsAsync(null);
            model.CommissionsPayableToDealers = CommissionPayees.Dealers(commissionAccounts).Sum(d => d.Pool.Owed);
            model.CommissionsPayableToAgents = CommissionPayees.Agents(commissionAccounts).Sum(a => a.Pool.Owed);

            // Retained Earnings: an approximation, not a precise accounting
            // figure -- there's no real all-time ledger. All-time Revenue/
            // Commissions come from a simple unfiltered SUM; all-time Cost
            // of Devices sums BuyingPrice across every fetched account
            // regardless of StartDate; Bad Debt uses the current (point-in-
            // time) classification as the best available proxy for
            // cumulative write-offs, since there's no historical recompute
            // for it; Operating Expenses sums the trailing `trendMonths`
            // months, which in practice is the table's entire history since
            // it was only just created.
            var allTimeRevenue = await _repository.GetAllTimeRevenueAsync();
            var allTimeCommissions = await _repository.GetAllTimeCommissionsPaidAsync();
            var allTimeCostOfDevices = accountDetails.Sum(r => r.BuyingPrice ?? 0);
            var allTimeOperatingExpenses = opexByMonth.Sum(r => r.Total);

            // No bad-debt deduction: revenue is counted as cash received and
            // device cost when the contract starts, so an unpaid balance was
            // never income -- deducting it again would count the loss twice.
            // Write-offs only take the balance off the loan book (balance
            // sheet); their real loss is reported separately on Financials.
            var allTimeNetBeforeTax = allTimeRevenue - allTimeCostOfDevices - allTimeCommissions - allTimeOperatingExpenses;
            var allTimeTax = Math.Max(0, allTimeNetBeforeTax) * model.TaxRatePct / 100;
            model.RetainedEarningsAllTime = allTimeNetBeforeTax - allTimeTax;

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

            // Agent and Dealer Commissions cards (and the Performance Bonus
            // card on the Agent Dashboard) are set live further down by
            // ApplyLiveCommissionCards, overriding the nightly snapshot copies.
            // An agent sees only their own accounts.
            var commissionAccounts = (await _repository.GetCommissionAccountsAsync(dealerId, agentUserId))
                .Where(a => !agentUserId.HasValue || a.AgentId == agentUserId)
                .ToList();
            ApplyLiveCommissionCards(model, commissionAccounts, dealerId, agentUserId);

            if (agentUserId.HasValue)
            {
                // Performance Bonus Tracker card -- Agent Dashboard only.
                // "At risk" = 90+ days old but the bonus is held (past lock
                // date or no WooCommerce order); "upcoming" = 60-89 days in.
                var breakdowns = commissionAccounts.Select(a => a.Commission).ToList();
                model.CommissionPaidLifetime = breakdowns.Sum(b => b.AgentPaid);
                model.BonusEarnedAccountCount = breakdowns.Count(b => b.BonusEarned);
                model.BonusAtRiskAccountCount = breakdowns.Count(b => b.BonusAtRisk);
                var upcoming = commissionAccounts.Where(a =>
                    a.DaysSinceStart >= CommissionCalculator.BonusDays - 30 && a.DaysSinceStart < CommissionCalculator.BonusDays).ToList();
                model.BonusUpcomingAccountCount = upcoming.Count;
                model.BonusUpcomingValue = upcoming.Sum(a => a.Deposit * CommissionCalculator.AgentBonusRate);
                var held = commissionAccounts.Where(a => a.Commission.BonusAtRisk).ToList();
                model.BonusHeldAmount = held.Sum(a => a.Deposit * CommissionCalculator.AgentBonusRate);
                model.BonusHeldPastLockCount = held.Count(a => !a.Commission.BonusHeldNoWooOrder);
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
            // Live for the dealer's own view too (the snapshot is a day old).
            model.TotalAccounts = accountDetails.Count;

            // Target isn't part of the nightly rollup (see
            // DashboardRevenuePeriodRow.TargetRevenue) -- computed live here,
            // same as the date-range filter, just always for "this month"
            // since that's what the page loads with by default.
            if (TryResolvePeriodWindow("month", out var monthWindow))
            {
                var monthRow = await _repository.GetDealerRevenueForPeriodAsync(
                    dealerId, monthWindow.PeriodStart, monthWindow.PeriodEndExclusive, monthWindow.PriorPeriodStart, monthWindow.PriorPeriodEndExclusive, agentUserId);
                model.RevenueTarget = monthRow.TargetRevenue;

                // Revenue card: live and scoped like everything else on the
                // page (an agent sees their own accounts' payments, not the
                // dealer's) -- the nightly snapshot is dealer-wide and a day old.
                model.RevenueThisMonth = monthRow.RevenueThisPeriod;
                model.RevenueGrowthPct = ScheduledDashboardRollup.CalculateGrowthPct(monthRow.RevenueThisPeriod, monthRow.RevenueLastPeriod) ?? 0;
                model.AvgPerAccount = model.TotalAccounts > 0 ? model.RevenueThisMonth / model.TotalAccounts : 0;

                // In default: accounts more than 7 days past their lock date --
                // the same test as the Arrears tier and commission suspension,
                // live and agent-scoped (the snapshot's figure was dealer-wide
                // and used a different, accrual-based definition).
                model.InDefault = accountDetails.Count(r => LockDays(r) > CommissionPayoutRules.LockToleranceDays);
                model.DefaultRatePct = accountDetails.Count > 0 ? Math.Round(100m * model.InDefault / accountDetails.Count, 1) : 0;

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
            model.Collections = BuildCollections(accountDetails);
            model.Contracts = BuildContracts(accountDetails);
            model.ContractsEndingSoon = BuildContractsEndingSoon(accountDetails);

            (model.CommissionSummary, model.CommissionAccounts) = BuildCommissions(commissionAccounts, accountDetails);

            model.DeviceStock = BuildDeviceStock(accountDetails);
            // Completed contracts: live, and only the agent's own on the Agent
            // Dashboard (the rows are per dealer). The completion rate has no
            // agreed definition yet and the portfolio "vs last month" has no
            // history, so both stay null (hidden) rather than showing 0.0.
            var completedRows = await _repository.GetCompletedContractsAsync(scope, DealerScopeTakeAll);
            if (agentUserId.HasValue)
            {
                var agentAccountIds = commissionAccounts.Select(a => a.AccountId).ToHashSet();
                completedRows = completedRows.Where(c => c.AccountId.HasValue && agentAccountIds.Contains(c.AccountId.Value)).ToList();
            }
            model.CompletedContracts = BuildCompletedContracts(completedRows);

            var completedMonthStart = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            var paidOffRows = completedRows.Where(c => c.Status == DashboardCompletedContractStatus.Completed && c.CompletedDate.HasValue).ToList();
            var paidOffThisMonth = paidOffRows.Where(c => c.CompletedDate >= completedMonthStart).ToList();
            var paidOffLastMonth = paidOffRows.Count(c => c.CompletedDate >= completedMonthStart.AddMonths(-1) && c.CompletedDate < completedMonthStart);
            model.CompletedContractsThisMonth = paidOffThisMonth.Count;
            model.CompletedContractsChangePct = ScheduledDashboardRollup.CalculateGrowthPct(paidOffThisMonth.Count, paidOffLastMonth);
            model.TotalValueCompletedThisMonth = paidOffThisMonth.Sum(c => c.TotalPaid);
            model.AvgTimeToCompletionMonths = paidOffRows.Count > 0 ? Math.Round((decimal)paidOffRows.Average(c => c.DurationMonths), 1) : 0;
            model.ContractCompletionRatePct = null;
            model.ContractCompletionRateChangePct = null;
            model.PortfolioGoodPctChange = null;

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
                NextOfKinName = r.NextOfKinName,
                NextOfKinPhone = r.NextOfKinPhone,
                NextOfKinIdNumber = r.NextOfKinIdNumber,
                NextOfKin2Name = r.NextOfKin2Name,
                NextOfKin2Phone = r.NextOfKin2Phone,
                NextOfKin2IdNumber = r.NextOfKin2IdNumber,
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

        // Device Performance: live from the same account rows as the rest of
        // the page, so an agent sees only their own devices. "Collected" is
        // money received / money due to date (capped at 100%) -- a plain
        // good-vs-arrears split read 100% whenever slow payers were left out.
        // "Behind" counts accounts more than a week of instalments behind.
        private static List<DealerDeviceStock> BuildDeviceStock(List<DashboardAccountDetailRow> rows) => rows
            .GroupBy(r => string.IsNullOrWhiteSpace(r.DeviceName) ? "Unknown device" : r.DeviceName.Trim())
            .Select(g =>
            {
                var paid = g.Sum(r => r.TotalPaid);
                var due = g.Sum(r => Math.Max(0, r.TotalPaid - r.ArrearsAmount));
                var behind = g.Count(IsBehind);
                return new DealerDeviceStock
                {
                    Device = g.Key,
                    Units = g.Count(),
                    AvgValue = Math.Round(g.Average(r => r.FullContractValue), 0),
                    CollectedPct = due > 0 ? Math.Min(100m, Math.Round(100m * paid / due, 1)) : 100m,
                    BehindCount = behind,
                    GoodPct = Math.Round(100m * (g.Count() - behind) / g.Count(), 1),
                    ArrearsPct = Math.Round(100m * behind / g.Count(), 1),
                };
            })
            .OrderByDescending(d => d.Units)
            .ToList();

        // More than a week of instalments behind on the original schedule.
        private static bool IsBehind(DashboardAccountDetailRow r) =>
            r.ArrearsAmount < 0 && -r.ArrearsAmount > Math.Max(0, r.DailyBlendedRate) * 7;

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
                AccountId = r.AccountId,
                CustomerName = r.CustomerName,
                AgentName = r.AgentName ?? "",
                DealerName = r.DealerName,
                Phone = r.CustomerPhone,
                NextOfKinName = r.NextOfKinName,
                NextOfKinPhone = r.NextOfKinPhone,
                NextOfKinIdNumber = r.NextOfKinIdNumber,
                NextOfKin2Name = r.NextOfKin2Name,
                NextOfKin2Phone = r.NextOfKin2Phone,
                NextOfKin2IdNumber = r.NextOfKin2IdNumber,
                Detail = $"{Math.Round(LockDays(r))} days overdue",
            }).ToList();

        private static List<DealerWatchlistEntry> BuildSlowPayers(List<DashboardAccountDetailRow> rows) => rows
            .Where(r => LockDays(r) is > 0 and <= 7)
            .Select(r => new DealerWatchlistEntry
            {
                AccountId = r.AccountId,
                CustomerName = r.CustomerName,
                AgentName = r.AgentName ?? "",
                DealerName = r.DealerName,
                Phone = r.CustomerPhone,
                NextOfKinName = r.NextOfKinName,
                NextOfKinPhone = r.NextOfKinPhone,
                NextOfKinIdNumber = r.NextOfKinIdNumber,
                NextOfKin2Name = r.NextOfKin2Name,
                NextOfKin2Phone = r.NextOfKin2Phone,
                NextOfKin2IdNumber = r.NextOfKin2IdNumber,
                Detail = $"KES {Math.Max(0, -r.ArrearsAmount):N0} due",
            }).ToList();

        private static List<DealerWatchlistEntry> BuildGoodPayers(List<DashboardAccountDetailRow> rows) => rows
            .Where(r => LockDays(r) <= 0)
            .Select(r => new DealerWatchlistEntry
            {
                AccountId = r.AccountId,
                CustomerName = r.CustomerName,
                AgentName = r.AgentName ?? "",
                DealerName = r.DealerName,
                Phone = r.CustomerPhone,
                NextOfKinName = r.NextOfKinName,
                NextOfKinPhone = r.NextOfKinPhone,
                NextOfKinIdNumber = r.NextOfKinIdNumber,
                NextOfKin2Name = r.NextOfKin2Name,
                NextOfKin2Phone = r.NextOfKin2Phone,
                NextOfKin2IdNumber = r.NextOfKin2IdNumber,
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
            .Select(g => FillAgentMetrics(new DealerAgentPerformance(), g.ToList()))
            .OrderByDescending(a => a.Accounts)
            .Select((a, i) => { a.Rank = i + 1; return a; })
            .ToList();

        // Account Commissions page (sidebar). Every figure comes from
        // CommissionCalculator via the repository.
        public async Task<AccountCommissionsViewModel> GetAccountCommissionsPageAsync(int? dealerId, int? agentUserId, bool showDealer)
        {
            var accounts = await _repository.GetCommissionAccountsAsync(dealerId, agentUserId);

            var rows = accounts
                .Select(a => new AccountCommissionRow
                {
                    AccountId = a.AccountId,
                    CustomerName = a.CustomerName,
                    AgentName = a.AgentId.HasValue ? a.AgentName : null,
                    DealerName = a.DealerName,
                    DaysSinceStart = a.DaysSinceStart,
                    Deposit = a.Deposit,
                    TotalPaid = a.TotalPaid,
                    BuyingPrice = a.BuyingPrice,
                    Commission = a.Commission,
                    ContractId = a.ContractId,
                    ProductName = a.ProductName,
                    Imei = a.Imei,
                    StartDate = a.StartDate,
                    ContractValue = a.TotalCost,
                    DaysPastLock = (int)Math.Max(0, Math.Floor(a.DaysPastLock)),
                    AgentBonusPaid = a.AgentBonusPaid,
                })
                .OrderBy(r => r.AgentName == null)
                .ThenBy(r => r.AgentName)
                .ThenByDescending(r => r.AccountId)
                .ToList();

            // Agent totals pool per agent (like the Agent Commissions card), then add up.
            var agentPools = accounts
                .Where(a => a.AgentId.HasValue)
                .GroupBy(a => a.AgentId!.Value)
                .Select(g => CommissionCalculator.PoolAgent(g.Select(a => a.Commission)))
                .ToList();

            var dealerPools = accounts
                .GroupBy(a => a.DealerId)
                .Select(g => CommissionCalculator.PoolDealer(g.Select(a => a.Commission)))
                .ToList();

            return new AccountCommissionsViewModel
            {
                IsAgentView = agentUserId.HasValue,
                ShowDealer = showDealer,
                Rows = rows,
                Agent = SumPools(agentPools),
                Dealer = SumPools(dealerPools),
                MissingBuyingPriceCount = accounts.Count(a => !a.BuyingPrice.HasValue),
            };
        }

        private static CommissionPool SumPools(List<CommissionPool> pools) => new()
        {
            Earned = pools.Sum(p => p.Earned),
            Deducted = pools.Sum(p => p.Deducted),
            Withheld = pools.Sum(p => p.Withheld),
            Paid = pools.Sum(p => p.Paid),
            Owed = pools.Sum(p => p.Owed),
        };

        // Commissions section. The summary pools each agent's accounts the
        // same way the Agent Commissions card and Pay Commissions do
        // (CommissionPayees): arrears on one account reduce what's owed on
        // the others, and a suspended agent's owed commission is held, not
        // payable. The table shows each account's own figures.
        private static (DashboardCommissionSummary Summary, List<DashboardCommissionAccount> Accounts) BuildCommissions(
            List<CommissionAccount> commissionAccounts, List<DashboardAccountDetailRow> accountDetails)
        {
            var details = accountDetails
                .GroupBy(a => a.AccountId)
                .ToDictionary(g => g.Key, g => g.First());

            var agentAccounts = commissionAccounts.Where(a => a.AgentId.HasValue).ToList();
            var perAgent = CommissionPayees.Agents(agentAccounts);

            var summary = new DashboardCommissionSummary
            {
                Agents = perAgent.Count,
                Accounts = agentAccounts.Count,
                Earned = perAgent.Sum(a => a.Pool.Earned),
                Withheld = perAgent.Sum(a => a.Pool.Withheld),
                Paid = perAgent.Sum(a => a.Pool.Paid),
                Owed = perAgent.Sum(a => a.Pool.Owed),
                Payable = perAgent.Sum(a => a.Payable),
                HeldSuspended = perAgent.Where(a => a.IsSuspended).Sum(a => a.Pool.Owed),
                SuspendedAgents = perAgent.Count(a => a.IsSuspended),
                UpfrontDue = perAgent.Sum(a => a.UpfrontDue),
                BonusDue = perAgent.Sum(a => a.BonusDue),
            };

            var accounts = agentAccounts
                .Select(a =>
                {
                    details.TryGetValue(a.AccountId, out var d);
                    var c = a.Commission;
                    var net = c.AgentEarned - c.AgentArrearsDeducted - c.AgentPaid;
                    return new DashboardCommissionAccount
                    {
                        AccountId = a.AccountId,
                        CustomerName = d?.CustomerName ?? a.CustomerName,
                        AgentName = d?.AgentName ?? a.AgentName ?? $"Agent #{a.AgentId}",
                        DealerName = d?.DealerName ?? a.DealerName,
                        Earned = c.AgentEarned,
                        ArrearsDeducted = c.AgentArrearsDeducted,
                        Paid = c.AgentPaid,
                        Net = net,
                        Status = c.AgentArrearsDeducted > 0 ? "Withheld" : net > 0 ? "Owed" : "Paid",
                        Upfront = c.AgentUpfront,
                        UpfrontPaid = Math.Min(c.AgentUpfront, Math.Max(0, c.AgentPaid - a.AgentBonusPaid)),
                        Bonus = c.BonusEarned ? c.AgentBonus : a.Deposit * CommissionCalculator.AgentBonusRate,
                        BonusPaid = a.AgentBonusPaid,
                        BonusState = c.BonusEarned ? "earned"
                            : c.BonusHeldNoWooOrder ? "held-woo"
                            : c.BonusAtRisk ? "held-lock"
                            : "not-yet",
                        DaysToBonus = c.DaysToBonus,
                        CustomerBehindBy = c.TrueArrears,
                    };
                })
                .OrderByDescending(c => c.Net)
                .ToList();

            return (summary, accounts);
        }

        // Agent and Dealer Commissions cards, live from the same per-account
        // figures as the Commissions section and Pay Commissions (the nightly
        // snapshot's copies are out of date until the next run and know
        // nothing of suspension). Dealer view: every agent of this dealer,
        // pooled per agent, plus the dealer's own position. Agent view: this
        // agent only.
        private static void ApplyLiveCommissionCards(DealerDashboardViewModel model, List<CommissionAccount> accounts, int dealerId, int? agentUserId)
        {
            var agents = CommissionPayees.Agents(accounts);
            model.CommissionOutstanding = agents.Sum(a => a.Payable);
            model.CommissionAccountCount = agents.Sum(a => a.Accounts);
            model.CommissionWithheldForArrears = agents.Sum(a => a.Pool.Withheld);
            model.CommissionPaidToAgents = agents.Sum(a => a.Pool.Paid);
            model.AgentsSuspendedCount = agents.Count(a => a.IsSuspended);
            model.AgentCommissionHeld = agents.Where(a => a.IsSuspended).Sum(a => a.Pool.Owed);
            model.AgentUpfrontDue = agents.Sum(a => a.UpfrontDue);
            model.AgentBonusDue = agents.Sum(a => a.BonusDue);
            model.BonusHeldNoWooOrderCount = agents.Sum(a => a.BonusHeldNoWooOrderCount);

            if (agentUserId.HasValue)
            {
                var me = agents.FirstOrDefault(a => a.PayeeId == agentUserId.Value);
                model.AgentCommissionSuspended = me?.IsSuspended ?? false;
                model.AgentDefaultRatePct = me?.DefaultRatePct ?? 0;
                return;
            }

            if (accounts.Count > 0)
            {
                var dealer = CommissionPayees.Summarize(CommissionPayeeType.Dealer, dealerId, accounts);
                model.CommissionReceived = dealer.Pool.Earned;
                model.DealerCommissionOutstanding = dealer.Payable;
                model.DealerCommissionAccountCount = accounts.Count(a => a.BuyingPrice.HasValue);
                model.DealerCommissionMissingCostCount = dealer.MissingBuyingPriceCount;
                model.DealerCommissionWithheldForArrears = dealer.Pool.Withheld;
                model.DealerCommissionSuspended = dealer.IsSuspended;
                model.DealerDefaultRatePct = dealer.DefaultRatePct;
                model.DealerCommissionHeld = dealer.IsSuspended ? dealer.Pool.Owed : 0;
            }
            else
            {
                model.CommissionReceived = 0;
                model.DealerCommissionOutstanding = 0;
                model.DealerCommissionAccountCount = 0;
                model.DealerCommissionMissingCostCount = 0;
                model.DealerCommissionWithheldForArrears = 0;
            }
        }

        // Needs Collection threshold, shared with FillAgentMetrics so the
        // Collections table and the per-agent count always agree.
        private const int CollectionDaysPastLock = 30;

        // Anyone who paid within this many days is still paying, not a collection case.
        private const int RecentPaymentDays = 7;

        // Restructured = a manual restructure on record, or the dashboard's
        // existing "being managed" rule (shortfall but lock date not yet
        // passed -- see GetDealerArrearsClassificationAsync), which is where
        // auto-restructured payers land once their lock date is pushed out.
        private static bool IsRestructured(DashboardAccountDetailRow r) =>
            r.IsManuallyRestructured || (r.ArrearsAmount < 0 && LockDays(r) <= 0);

        private static bool PaidRecently(DashboardAccountDetailRow r) =>
            r.LastPaymentDate.HasValue && r.LastPaymentDate.Value >= DateTime.Now.AddDays(-RecentPaymentDays);

        private static bool NeedsCollection(DashboardAccountDetailRow r) =>
            LockDays(r) >= CollectionDaysPastLock && !IsRestructured(r) && !PaidRecently(r);

        private static List<DashboardCollectionEntry> BuildCollections(List<DashboardAccountDetailRow> rows) => rows
            .Where(NeedsCollection)
            .Select(r => (Row: r, Days: LockDays(r)))
            .OrderByDescending(x => x.Days)
            .ThenBy(x => x.Row.ArrearsAmount)
            .Select(x => new DashboardCollectionEntry
            {
                AccountId = x.Row.AccountId,
                CustomerName = x.Row.CustomerName,
                AgentName = x.Row.AgentName ?? "",
                DealerName = x.Row.DealerName,
                DeviceName = x.Row.DeviceName,
                DaysPastLock = (int)Math.Floor(x.Days),
                OverdueAmount = Math.Max(0, -x.Row.ArrearsAmount),
                LockDate = DashboardReportRepository.ParseNextLockDate(x.Row.NextLockDateRaw),
                LastPaymentDate = x.Row.LastPaymentDate,
                Phone = x.Row.CustomerPhone,
                NextOfKinName = x.Row.NextOfKinName,
                NextOfKinPhone = x.Row.NextOfKinPhone,
                NextOfKinIdNumber = x.Row.NextOfKinIdNumber,
                NextOfKin2Name = x.Row.NextOfKin2Name,
                NextOfKin2Phone = x.Row.NextOfKin2Phone,
                NextOfKin2IdNumber = x.Row.NextOfKin2IdNumber,
            })
            .ToList();

        // Shared by the Dealer and Admin Agent Performance tables so both
        // classify an agent's book identically.
        private static T FillAgentMetrics<T>(T target, List<DashboardAccountDetailRow> accounts)
            where T : DealerAgentPerformance
        {
            var total = accounts.Count;
            var restructured = accounts.Count(IsRestructured);
            // Lock buckets cover the rest of the book, so restructured
            // accounts are not also counted as Good/Slow/Arrears/Bad.
            var days = accounts.Where(r => !IsRestructured(r)).Select(LockDays).ToList();
            var good = days.Count(d => d <= 0);
            var slow = days.Count(d => d is > 0 and <= 7);
            var bad = days.Count(d => d > 90);
            var overdue = days.Count(d => d > 7); // Arrears + Bad
            var activeDenominator = good + overdue;

            var collected = accounts.Sum(r => r.TotalPaid);
            // ArrearsAmount is paid minus due, so due to date = paid - ArrearsAmount.
            var dueToDate = accounts.Sum(r => r.TotalPaid - r.ArrearsAmount);

            target.AgentName = accounts[0].AgentName ?? "";
            target.Accounts = total;
            target.ActivePct = activeDenominator > 0 ? Math.Round(100m * good / activeDenominator, 1) : 0;
            target.PctOfTarget = total > 0 ? Math.Round(100m * (total - overdue) / total, 1) : 0;
            target.GoodCount = good;
            target.RestructuredCount = restructured;
            target.SlowCount = slow;
            target.ArrearsCount = overdue - bad;
            target.BadCount = bad;
            target.NeedsCollectionCount = accounts.Count(NeedsCollection);
            target.RepaymentPct = dueToDate > 0 ? Math.Round(100m * collected / dueToDate, 1) : 0;
            return target;
        }

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
            .Select(g => FillAgentMetrics(
                new AdminAgentPerformance { DealerName = g.First().DealerName ?? "" }, g.ToList()))
            .OrderByDescending(a => a.Accounts)
            .Select((a, i) => { a.Rank = i + 1; return a; })
            .ToList();

        // Admin Dashboard's Dealer Performance: same grouping/formulas as
        // BuildDealerPerformance above, plus commissions -- CommissionPaid
        // is this month's real DealerCommissionPayments total (see
        // GetDealerCommissionPaidThisMonthByDealerAsync); CommissionDue is
        // the dealer's commission payable now (CommissionPayees: earned -
        // arrears - paid, 0 while suspended).
        private static List<AdminDealerPerformance> BuildAdminDealerPerformance(
            List<DashboardAccountDetailRow> rows,
            Dictionary<string, decimal> revenueByDealer,
            Dictionary<string, decimal> commissionByDealer,
            Dictionary<string, decimal> commissionDueByDealer) => rows
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
                    CommissionDue = commissionDueByDealer.TryGetValue(g.Key, out var due) ? due : 0,
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
                Status = ContractStatus(r),
                NextDue = FormatNextLockDate(r.NextLockDateRaw),
                DaysLeft = "",
            }).ToList();

        // My Contracts status. "On track" only when the account isn't behind
        // at all: a restructured account paying on its new plan is its own
        // status (not green), and a few days late is "Slightly behind".
        private static string ContractStatus(DashboardAccountDetailRow r)
        {
            var lockDays = LockDays(r);
            if (IsRestructured(r))
            {
                return lockDays > 0 ? ContractStatusRestructuredLate : ContractStatusRestructuredOnPlan;
            }
            if (lockDays > 7 || IsBehind(r))
            {
                return ContractStatusBehind;
            }
            return lockDays > 0 || r.ArrearsAmount < 0 ? ContractStatusSlightlyBehind : ContractStatusOnTrack;
        }

        public const string ContractStatusOnTrack = "On track";
        public const string ContractStatusSlightlyBehind = "Slightly behind";
        public const string ContractStatusBehind = "Behind";
        public const string ContractStatusRestructuredOnPlan = "Restructured · on plan";
        public const string ContractStatusRestructuredLate = "Restructured · late";

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

            // Agent commission paid in the period: one dealer's agents, or
            // company-wide for a null dealerId (the Admin Dashboard's
            // Commissions Paid card adds it to the dealer payouts).
            var commissionPaidThisPeriod = await _repository.GetDealerAgentCommissionPaidForPeriodAsync(
                dealerId, window.PeriodStart, window.PeriodEndExclusive, agentUserId);

            // Dealer Commissions: null dealerId now returns the company-wide
            // total (GetDealerCommissionPaidForPeriodAsync widened to
            // nullable) -- backs the Admin Dashboard's Commissions Paid card.
            // Still a harmless no-op for the Approver Dashboard, which
            // doesn't render this field.
            var dealerCommissionPaidThisPeriod = await _repository.GetDealerCommissionPaidForPeriodAsync(
                dealerId, window.PeriodStart, window.PeriodEndExclusive);

            // Completed Contracts summary card: reuses the same rows the
            // Completed Contracts report page shows
            // (GetDealerCompletedContractsReportAsync) -- no separate query,
            // just a date-range filter on Status == Completed. Neither the
            // Approver nor Admin Dashboard shows a period-filtered Completed
            // Contracts count, so this stays skipped for a null dealerId.
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

            // Company-wide revenue uses the Financials definition (every device
            // payment, including devices with no dealer mapping), so the Admin
            // Dashboard and Financials agree. Dealer/agent scopes stay as-is.
            var revenueThis = row.RevenueThisPeriod;
            var revenueLast = row.RevenueLastPeriod;
            if (!dealerId.HasValue && !agentUserId.HasValue)
            {
                revenueThis = await _repository.GetRevenueForPeriodAsync(window.PeriodStart, window.PeriodEndExclusive);
                revenueLast = await _repository.GetRevenueForPeriodAsync(window.PriorPeriodStart, window.PriorPeriodEndExclusive);
            }

            return new DealerRevenuePeriodResult
            {
                Revenue = revenueThis,
                GrowthPct = ScheduledDashboardRollup.CalculateGrowthPct(revenueThis, revenueLast),
                AvgPerAccount = row.TotalAccounts > 0 ? revenueThis / row.TotalAccounts : 0,
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

        public async Task<List<DealerDeviceStock>> GetDealerDeviceStockReportAsync(int dealerId, int? agentUserId = null) =>
            BuildDeviceStock(await _repository.GetDealerAccountDetailsAsync(dealerId, agentUserId));

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

            // TotalAccounts/GoodAccounts/BadAccounts/PayingAccounts/
            // NonPayingAccounts/NewThisMonth are all set later in
            // GetAdminDashboardAsync from live queries, not from this
            // snapshot (the rollup never wrote a consistent set of these,
            // so they'd otherwise stay on stale/mismatched sample counts --
            // see that method's comments).
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
