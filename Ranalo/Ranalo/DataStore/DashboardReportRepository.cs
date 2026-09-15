using System.Data;
using Dapper;
using System.Data.SqlClient;
using Ranalo.Models;

namespace Ranalo.DataStore
{
    public class DashboardReportRepository : IDashboardReportRepository
    {
        private readonly IDbConnection _db;
        private readonly ILogger<DashboardReportRepository> _logger;

        public DashboardReportRepository(IDbConnection db, ILogger<DashboardReportRepository> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<DashboardSnapshotRow?> GetSnapshotAsync(DashboardScope scope)
        {
            const string sql = @"
                SELECT TOP (1) *
                FROM DashboardSnapshot
                WHERE ((@DealerId IS NULL AND DealerId IS NULL) OR DealerId = @DealerId)
                  AND ((@AgentId IS NULL AND AgentId IS NULL) OR AgentId = @AgentId)";

            try
            {
                return await _db.QueryFirstOrDefaultAsync<DashboardSnapshotRow>(
                    sql, new { scope.DealerId, scope.AgentId });
            }
            catch (SqlException ex) when (IsMissingTable(ex))
            {
                // Rollup tables not applied to this database yet (see
                // Database/Dashboard/001_create_dashboard_tables.sql). Callers
                // fall back to sample data until the schema is in place and
                // the nightly job has run at least once.
                _logger.LogWarning(ex, "DashboardSnapshot table not found; returning no snapshot for scope {@Scope}", scope);
                return null;
            }
        }

        public async Task<string?> GetDealerNameAsync(int dealerId)
        {
            const string sql = "SELECT CompanyName FROM Dealers WHERE DealerId = @DealerId";

            try
            {
                return await _db.QueryFirstOrDefaultAsync<string>(sql, new { DealerId = dealerId });
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "Dealer name lookup failed for dealer {DealerId}", dealerId);
                return null;
            }
        }

        public async Task<List<DashboardMonthlyTrendPoint>> GetMonthlyTrendAsync(DashboardScope scope, int months = 8)
        {
            const string sql = @"
                SELECT TOP (@Months) YearMonth, Revenue, AccountsCount
                FROM DashboardMonthlyTrend
                WHERE ((@DealerId IS NULL AND DealerId IS NULL) OR DealerId = @DealerId)
                  AND ((@AgentId IS NULL AND AgentId IS NULL) OR AgentId = @AgentId)
                ORDER BY YearMonth DESC";

            try
            {
                var rows = await _db.QueryAsync<DashboardMonthlyTrendPoint>(
                    sql, new { scope.DealerId, scope.AgentId, Months = months });

                return rows.OrderBy(r => r.YearMonth).ToList();
            }
            catch (SqlException ex) when (IsMissingTable(ex))
            {
                _logger.LogWarning(ex, "DashboardMonthlyTrend table not found; returning no trend for scope {@Scope}", scope);
                return new List<DashboardMonthlyTrendPoint>();
            }
        }

        public async Task<List<DashboardWatchlistEntryRow>> GetWatchlistAsync(DashboardScope scope, string watchlistType, int take = 20)
        {
            const string sql = @"
                SELECT TOP (@Take) Rank, AccountId, CustomerName, AgentName, DealerName, Phone, Detail
                FROM DashboardWatchlistEntry
                WHERE WatchlistType = @WatchlistType
                  AND ScopeAgentId IS NULL
                  AND ((@DealerId IS NULL AND ScopeDealerId IS NULL) OR ScopeDealerId = @DealerId)
                ORDER BY Rank";

            try
            {
                var rows = await _db.QueryAsync<DashboardWatchlistEntryRow>(
                    sql, new { scope.DealerId, WatchlistType = watchlistType, Take = take });

                return rows.ToList();
            }
            catch (SqlException ex) when (IsMissingTable(ex))
            {
                _logger.LogWarning(ex, "DashboardWatchlistEntry table not found; returning no {WatchlistType} entries for scope {@Scope}", watchlistType, scope);
                return new List<DashboardWatchlistEntryRow>();
            }
        }

        public async Task<List<DashboardPerformanceEntryRow>> GetPerformanceAsync(DashboardScope scope, string entryType, int take = 20)
        {
            const string sql = @"
                SELECT TOP (@Take) Rank, SubjectId, SubjectName, ParentName, Accounts, ActivePct, Revenue, CommissionPaid, CommissionDue, PctOfTarget
                FROM DashboardPerformanceEntry
                WHERE EntryType = @EntryType
                  AND ScopeAgentId IS NULL
                  AND ((@DealerId IS NULL AND ScopeDealerId IS NULL) OR ScopeDealerId = @DealerId)
                ORDER BY Rank";

            try
            {
                var rows = await _db.QueryAsync<DashboardPerformanceEntryRow>(
                    sql, new { scope.DealerId, EntryType = entryType, Take = take });

                return rows.ToList();
            }
            catch (SqlException ex) when (IsMissingTable(ex))
            {
                _logger.LogWarning(ex, "DashboardPerformanceEntry table not found; returning no {EntryType} entries for scope {@Scope}", entryType, scope);
                return new List<DashboardPerformanceEntryRow>();
            }
        }

        public async Task<List<DashboardDeviceStockRow>> GetDeviceStockAsync(DashboardScope scope, int take = 20)
        {
            const string sql = @"
                SELECT TOP (@Take) DeviceName, Units, AvgValue, GoodPct, ArrearsPct
                FROM DashboardDeviceStock
                WHERE ScopeAgentId IS NULL
                  AND ((@DealerId IS NULL AND ScopeDealerId IS NULL) OR ScopeDealerId = @DealerId)
                ORDER BY Units DESC";

            try
            {
                var rows = await _db.QueryAsync<DashboardDeviceStockRow>(sql, new { scope.DealerId, Take = take });
                return rows.ToList();
            }
            catch (SqlException ex) when (IsMissingTable(ex))
            {
                _logger.LogWarning(ex, "DashboardDeviceStock table not found; returning no device stock for scope {@Scope}", scope);
                return new List<DashboardDeviceStockRow>();
            }
        }

        public async Task<List<DashboardCompletedContractRow>> GetCompletedContractsAsync(DashboardScope scope, int take = 20)
        {
            const string sql = @"
                SELECT TOP (@Take) AccountId, CustomerName, DealerName, ProductName, CompletedDate, TotalPaid, DurationMonths, Status, PctComplete
                FROM DashboardCompletedContract
                WHERE ScopeAgentId IS NULL
                  AND ((@DealerId IS NULL AND ScopeDealerId IS NULL) OR ScopeDealerId = @DealerId)
                ORDER BY CASE WHEN Status = 'Completed' THEN 0 ELSE 1 END, CompletedDate DESC, PctComplete DESC";

            try
            {
                var rows = await _db.QueryAsync<DashboardCompletedContractRow>(sql, new { scope.DealerId, Take = take });
                return rows.ToList();
            }
            catch (SqlException ex) when (IsMissingTable(ex))
            {
                _logger.LogWarning(ex, "DashboardCompletedContract table not found; returning no completed contracts for scope {@Scope}", scope);
                return new List<DashboardCompletedContractRow>();
            }
        }

        public async Task<List<DashboardKpiRollupRow>> ComputeKpiRollupAsync()
        {
            // Revenue = actual Mpesa payments received (KosePayments), not
            // WooCommerce order value -- Woo_Orders are applications, some of
            // which never convert into a paying contract. Dealer linkage goes
            // through Devices/Dealers (Contract_Info has no DealerId column
            // directly): KosePayments.AccountNoBigint = Devices.Id = Contract_Info.ID,
            // Devices.DeviceGroupId = Dealers.DealerReference. "Account" means a
            // contract that has actually received its first payment
            // (Contract_Info.StartDate IS NOT NULL), not just an approved order.
            const string sql = @"
                ;WITH RevenueByDealer AS (
                    SELECT
                        dl.DealerId,
                        SUM(CASE WHEN MONTH(kp.PaymentDateValue) = MONTH(GETDATE()) AND YEAR(kp.PaymentDateValue) = YEAR(GETDATE())
                                 THEN kp.AmountValue ELSE 0 END) AS RevenueThisMonth,
                        SUM(CASE WHEN MONTH(kp.PaymentDateValue) = MONTH(DATEADD(MONTH, -1, GETDATE())) AND YEAR(kp.PaymentDateValue) = YEAR(DATEADD(MONTH, -1, GETDATE()))
                                 THEN kp.AmountValue ELSE 0 END) AS RevenueLastMonth
                    FROM KosePayments kp
                    INNER JOIN Devices d ON d.Id = kp.AccountNoBigint
                    INNER JOIN Dealers dl ON dl.DealerReference = d.DeviceGroupId
                    GROUP BY GROUPING SETS ((dl.DealerId), ())
                ),
                AccountsByDealer AS (
                    SELECT
                        dl.DealerId,
                        COUNT(*) AS TotalAccounts,
                        SUM(CASE WHEN MONTH(ci.StartDate) = MONTH(GETDATE()) AND YEAR(ci.StartDate) = YEAR(GETDATE()) THEN 1 ELSE 0 END) AS NewThisMonth
                    FROM Contract_Info ci
                    INNER JOIN Devices d ON d.Id = ci.ID
                    INNER JOIN Dealers dl ON dl.DealerReference = d.DeviceGroupId
                    WHERE ci.StartDate IS NOT NULL
                    GROUP BY GROUPING SETS ((dl.DealerId), ())
                )
                SELECT
                    COALESCE(r.DealerId, a.DealerId) AS DealerId,
                    ISNULL(r.RevenueThisMonth, 0) AS RevenueThisMonth,
                    ISNULL(r.RevenueLastMonth, 0) AS RevenueLastMonth,
                    ISNULL(a.TotalAccounts, 0) AS TotalAccounts,
                    ISNULL(a.NewThisMonth, 0) AS NewThisMonth
                FROM RevenueByDealer r
                FULL OUTER JOIN AccountsByDealer a
                    ON a.DealerId = r.DealerId OR (a.DealerId IS NULL AND r.DealerId IS NULL)";

            try
            {
                var rows = await _db.QueryAsync<DashboardKpiRollupRow>(sql);
                return rows.ToList();
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "KPI rollup computation failed");
                return new List<DashboardKpiRollupRow>();
            }
        }

        public async Task<DashboardRevenuePeriodRow> GetDealerRevenueForPeriodAsync(
            int dealerId,
            DateTime periodStart,
            DateTime periodEndExclusive,
            DateTime priorPeriodStart,
            DateTime priorPeriodEndExclusive)
        {
            // Same Dealer linkage as ComputeKpiRollupAsync (KosePayments ->
            // Devices -> Dealers), but filtered to one dealer and an arbitrary
            // date window instead of the hardcoded "this/last calendar month"
            // the nightly rollup uses. TotalAccounts mirrors
            // ComputeKpiRollupAsync's AccountsByDealer definition (Contract_Info
            // rows with a StartDate) so AvgPerAccount stays comparable to the
            // page's default (rollup-backed) figure.
            //
            // TargetRevenue ("if everyone paid as expected"): each account's
            // daily-blended rate (Daily + Weekly/7 + Monthly/30 -- same
            // formula as ComputePortfolioClassificationRollupAsync's Arrears
            // calc) times however many days its accrual window (StartDate
            // through contract completion, capped like
            // ContractCalculatorService.CalculateTotalDue) overlaps the
            // requested period. Not what was actually paid -- what should
            // have been paid.
            const string sql = @"
                SELECT
                    ISNULL(SUM(CASE WHEN kp.PaymentDateValue >= @PeriodStart AND kp.PaymentDateValue < @PeriodEnd
                             THEN kp.AmountValue ELSE 0 END), 0) AS RevenueThisPeriod,
                    ISNULL(SUM(CASE WHEN kp.PaymentDateValue >= @PriorPeriodStart AND kp.PaymentDateValue < @PriorPeriodEnd
                             THEN kp.AmountValue ELSE 0 END), 0) AS RevenueLastPeriod,
                    (SELECT COUNT(*)
                     FROM Contract_Info ci
                     INNER JOIN Devices d2 ON d2.Id = ci.ID
                     INNER JOIN Dealers dl2 ON dl2.DealerReference = d2.DeviceGroupId
                     WHERE dl2.DealerId = @DealerId AND ci.StartDate IS NOT NULL) AS TotalAccounts,
                    (SELECT ISNULL(SUM(
                            ca.DailyBlendedRate *
                            CASE WHEN ca.OverlapEnd > ca.OverlapStart THEN DATEDIFF(DAY, ca.OverlapStart, ca.OverlapEnd) ELSE 0 END
                        ), 0)
                     FROM (
                         SELECT
                             (ci3.Daily + (ci3.Weekly / 7.0) + (ci3.Monthly / 30.0)) AS DailyBlendedRate,
                             CASE WHEN ci3.StartDate > @PeriodStart THEN ci3.StartDate ELSE @PeriodStart END AS OverlapStart,
                             CASE
                                 WHEN DATEADD(DAY, CAST(ci3.Term_in_Months * 30 AS INT), ci3.StartDate) < @PeriodEnd
                                 THEN DATEADD(DAY, CAST(ci3.Term_in_Months * 30 AS INT), ci3.StartDate)
                                 ELSE @PeriodEnd
                             END AS OverlapEnd
                         FROM Contract_Info ci3
                         INNER JOIN Devices d3 ON d3.Id = ci3.ID
                         INNER JOIN Dealers dl3 ON dl3.DealerReference = d3.DeviceGroupId
                         WHERE dl3.DealerId = @DealerId AND ci3.StartDate IS NOT NULL
                     ) ca) AS TargetRevenue
                FROM KosePayments kp
                INNER JOIN Devices d ON d.Id = kp.AccountNoBigint
                INNER JOIN Dealers dl ON dl.DealerReference = d.DeviceGroupId
                WHERE dl.DealerId = @DealerId";

            try
            {
                var row = await _db.QueryFirstOrDefaultAsync<DashboardRevenuePeriodRow>(sql, new
                {
                    DealerId = dealerId,
                    PeriodStart = periodStart,
                    PeriodEnd = periodEndExclusive,
                    PriorPeriodStart = priorPeriodStart,
                    PriorPeriodEnd = priorPeriodEndExclusive,
                });
                return row ?? new DashboardRevenuePeriodRow();
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "Dealer revenue-for-period query failed for dealer {DealerId}", dealerId);
                return new DashboardRevenuePeriodRow();
            }
        }

        public async Task<List<DashboardPortfolioRollupRow>> ComputePortfolioClassificationRollupAsync()
        {
            // TotalPaid per account mirrors the existing ValidPayments/OrphanedPayments
            // reconciliation pattern used elsewhere (e.g. GetPaymentSummaryAsync) --
            // payments are matched to an account via KosePayments, falling back to
            // OrphanedPayments' reconciled AccountNoBigint when set.
            //
            // TotalDue mirrors ContractCalculatorService.CalculateTotalDue: deposit +
            // daily/weekly/monthly accrual since StartDate (first payment date),
            // capped at the contract's full term so it never exceeds contract value.
            //
            // Classification is driven by "days overdue" against the self-calculated
            // lock date (see ScheduledLockPaying's AutoLockDate formula) rather than
            // Arrears sign or days-since-last-payment alone -- an account that has
            // paid ahead of schedule has positive Arrears and a lock date in the
            // future, so it is never misclassified as behind just because it hasn't
            // transacted recently. See DashboardPortfolioRollupRow for tier definitions.
            const string sql = @"
                ;WITH ValidPayments AS (
                    SELECT
                        COALESCE(op.AccountNoBigint, kp.AccountNoBigint) AS AccountNo,
                        kp.AmountValue,
                        kp.PaymentDateValue
                    FROM KosePayments kp
                    LEFT JOIN OrphanedPayments op ON op.MpesaCode = kp.MpesaCode
                ),
                PaymentTotals AS (
                    SELECT AccountNo, SUM(AmountValue) AS TotalPaid
                    FROM ValidPayments
                    GROUP BY AccountNo
                ),
                PaymentTotalsAsOfLastMonth AS (
                    SELECT AccountNo, SUM(AmountValue) AS TotalPaid
                    FROM ValidPayments
                    WHERE PaymentDateValue < DATEADD(MONTH, -1, GETDATE())
                    GROUP BY AccountNo
                ),
                AccountClassification AS (
                    SELECT
                        dl.DealerId,
                        ISNULL(pt.TotalPaid, 0)
                            - (
                                ci.Deposit
                                + ci.Daily * DaysAccrued.Days
                                + ci.Weekly * (DaysAccrued.Days / 7.0)
                                + ci.Monthly * (DaysAccrued.Days / 30.0)
                              ) AS Arrears,
                        (ci.Daily + (ci.Weekly / 7.0) + (ci.Monthly / 30.0)) AS DailyBlendedRate
                    FROM Contract_Info ci
                    INNER JOIN Devices d ON d.Id = ci.ID
                    INNER JOIN Dealers dl ON dl.DealerReference = d.DeviceGroupId
                    LEFT JOIN PaymentTotals pt ON pt.AccountNo = ci.ID
                    CROSS APPLY (
                        SELECT CASE
                            WHEN DATEDIFF(DAY, ci.StartDate, GETDATE()) < CAST(ci.Term_in_Months * 30 AS INT)
                                THEN DATEDIFF(DAY, ci.StartDate, GETDATE())
                            ELSE CAST(ci.Term_in_Months * 30 AS INT)
                        END AS Days
                    ) DaysAccrued
                    WHERE ci.StartDate IS NOT NULL
                ),
                -- Same Arrears formula as AccountClassification, but ""as of""
                -- one calendar month ago: only payments received by then
                -- (PaymentTotalsAsOfLastMonth), accrual days measured up to
                -- then (still capped at the contract's full term), and only
                -- accounts that already existed then. Lets ArrearsChangePct
                -- compare like-for-like instead of diffing today's balance
                -- against a number nobody actually computed a month ago.
                AccountClassificationLastMonth AS (
                    SELECT
                        dl.DealerId,
                        ISNULL(pt.TotalPaid, 0)
                            - (
                                ci.Deposit
                                + ci.Daily * DaysAccruedLM.Days
                                + ci.Weekly * (DaysAccruedLM.Days / 7.0)
                                + ci.Monthly * (DaysAccruedLM.Days / 30.0)
                              ) AS Arrears
                    FROM Contract_Info ci
                    INNER JOIN Devices d ON d.Id = ci.ID
                    INNER JOIN Dealers dl ON dl.DealerReference = d.DeviceGroupId
                    LEFT JOIN PaymentTotalsAsOfLastMonth pt ON pt.AccountNo = ci.ID
                    CROSS APPLY (
                        SELECT CASE
                            WHEN DATEDIFF(DAY, ci.StartDate, DATEADD(MONTH, -1, GETDATE())) < CAST(ci.Term_in_Months * 30 AS INT)
                                THEN DATEDIFF(DAY, ci.StartDate, DATEADD(MONTH, -1, GETDATE()))
                            ELSE CAST(ci.Term_in_Months * 30 AS INT)
                        END AS Days
                    ) DaysAccruedLM
                    WHERE ci.StartDate IS NOT NULL AND ci.StartDate < DATEADD(MONTH, -1, GETDATE())
                ),
                ArrearsLastMonth AS (
                    SELECT
                        DealerId,
                        SUM(CASE WHEN Arrears < 0 THEN -Arrears ELSE 0 END) AS ArrearsTotalLastMonth
                    FROM AccountClassificationLastMonth
                    GROUP BY GROUPING SETS ((DealerId), ())
                ),
                Tiered AS (
                    SELECT
                        DealerId,
                        Arrears,
                        CASE WHEN DailyBlendedRate = 0 THEN NULL ELSE -(Arrears / DailyBlendedRate) END AS DaysOverdue
                    FROM AccountClassification
                    WHERE DailyBlendedRate <> 0 -- accounts with no payment plan can't be classified
                ),
                -- Unfiltered count (same population as ComputeKpiRollupAsync's
                -- AccountsByDealer -- every Contract_Info row with a
                -- StartDate) so ArrearsCount / TotalAccounts gives a rate
                -- against the same ""Total Accounts"" figure the KPI card
                -- shows, not just the subset with a payment plan.
                TotalCounts AS (
                    SELECT DealerId, COUNT(*) AS TotalAccounts
                    FROM AccountClassification
                    GROUP BY GROUPING SETS ((DealerId), ())
                ),
                Classified AS (
                    SELECT
                        DealerId,
                        100.0 * SUM(CASE WHEN DaysOverdue <= 0 THEN 1 ELSE 0 END) / COUNT(*) AS GoodPct,
                        100.0 * SUM(CASE WHEN DaysOverdue > 0 AND DaysOverdue <= 7 THEN 1 ELSE 0 END) / COUNT(*) AS SlowPct,
                        100.0 * SUM(CASE WHEN DaysOverdue > 7 THEN 1 ELSE 0 END) / COUNT(*) AS ArrearsPct,
                        100.0 * SUM(CASE WHEN DaysOverdue > 90 THEN 1 ELSE 0 END) / COUNT(*) AS NonPayingPct,
                        SUM(CASE WHEN Arrears < 0 THEN -Arrears ELSE 0 END) AS ArrearsTotal,
                        -- ""In default"" = the Arrears tier (>7 days overdue),
                        -- not the narrower NonPaying subset -- see
                        -- DashboardPortfolioRollupRow for why.
                        SUM(CASE WHEN DaysOverdue > 7 THEN 1 ELSE 0 END) AS ArrearsCount
                    FROM Tiered
                    GROUP BY GROUPING SETS ((DealerId), ())
                )
                SELECT
                    COALESCE(c.DealerId, tc.DealerId) AS DealerId,
                    ISNULL(c.GoodPct, 0) AS GoodPct,
                    ISNULL(c.SlowPct, 0) AS SlowPct,
                    ISNULL(c.ArrearsPct, 0) AS ArrearsPct,
                    ISNULL(c.NonPayingPct, 0) AS NonPayingPct,
                    ISNULL(c.ArrearsTotal, 0) AS ArrearsTotal,
                    ISNULL(c.ArrearsCount, 0) AS ArrearsCount,
                    ISNULL(tc.TotalAccounts, 0) AS TotalAccounts,
                    ISNULL(alm.ArrearsTotalLastMonth, 0) AS ArrearsTotalLastMonth
                FROM Classified c
                FULL OUTER JOIN TotalCounts tc
                    ON tc.DealerId = c.DealerId OR (tc.DealerId IS NULL AND c.DealerId IS NULL)
                LEFT JOIN ArrearsLastMonth alm
                    ON alm.DealerId = COALESCE(c.DealerId, tc.DealerId) OR (alm.DealerId IS NULL AND COALESCE(c.DealerId, tc.DealerId) IS NULL)";

            try
            {
                var rows = await _db.QueryAsync<DashboardPortfolioRollupRow>(sql);
                return rows.ToList();
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "Portfolio classification rollup computation failed");
                return new List<DashboardPortfolioRollupRow>();
            }
        }

        public async Task UpsertSnapshotPortfolioAsync(
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
            const string sql = @"
                MERGE DashboardSnapshot AS target
                USING (SELECT @DealerId AS DealerId, @AgentId AS AgentId) AS source
                ON  (target.DealerId = source.DealerId OR (target.DealerId IS NULL AND source.DealerId IS NULL))
                AND (target.AgentId = source.AgentId OR (target.AgentId IS NULL AND source.AgentId IS NULL))
                WHEN MATCHED THEN
                    UPDATE SET
                        PortfolioGoodPct = @PortfolioGoodPct,
                        PortfolioSlowPct = @PortfolioSlowPct,
                        PortfolioArrearsPct = @PortfolioArrearsPct,
                        PortfolioNonPayingPct = @PortfolioNonPayingPct,
                        ArrearsTotal = @ArrearsTotal,
                        ArrearsChangePct = @ArrearsChangePct,
                        InDefault = @InDefault,
                        DefaultRatePct = @DefaultRatePct,
                        ActivePct = @ActivePct,
                        RefreshedAtUtc = SYSUTCDATETIME()
                WHEN NOT MATCHED THEN
                    INSERT (DealerId, AgentId, PortfolioGoodPct, PortfolioSlowPct, PortfolioArrearsPct, PortfolioNonPayingPct, ArrearsTotal, ArrearsChangePct, InDefault, DefaultRatePct, ActivePct, RefreshedAtUtc)
                    VALUES (source.DealerId, source.AgentId, @PortfolioGoodPct, @PortfolioSlowPct, @PortfolioArrearsPct, @PortfolioNonPayingPct, @ArrearsTotal, @ArrearsChangePct, @InDefault, @DefaultRatePct, @ActivePct, SYSUTCDATETIME());";

            try
            {
                await _db.ExecuteAsync(sql, new
                {
                    scope.DealerId,
                    scope.AgentId,
                    PortfolioGoodPct = portfolioGoodPct,
                    PortfolioSlowPct = portfolioSlowPct,
                    PortfolioArrearsPct = portfolioArrearsPct,
                    PortfolioNonPayingPct = portfolioNonPayingPct,
                    ArrearsTotal = arrearsTotal,
                    ArrearsChangePct = arrearsChangePct,
                    InDefault = inDefault,
                    DefaultRatePct = defaultRatePct,
                    ActivePct = activePct,
                });
            }
            catch (SqlException ex) when (IsMissingTable(ex))
            {
                _logger.LogWarning(ex, "DashboardSnapshot table not found; skipping portfolio refresh for scope {@Scope}. Apply Database/Dashboard/001_create_dashboard_tables.sql first.", scope);
            }
        }

        public async Task UpsertSnapshotKpiAsync(
            DashboardScope scope,
            decimal? revenueThisMonth,
            decimal? revenueGrowthPct,
            int? newThisMonth,
            int? totalAccounts)
        {
            const string sql = @"
                MERGE DashboardSnapshot AS target
                USING (SELECT @DealerId AS DealerId, @AgentId AS AgentId) AS source
                ON  (target.DealerId = source.DealerId OR (target.DealerId IS NULL AND source.DealerId IS NULL))
                AND (target.AgentId = source.AgentId OR (target.AgentId IS NULL AND source.AgentId IS NULL))
                WHEN MATCHED THEN
                    UPDATE SET
                        RevenueThisMonth = @RevenueThisMonth,
                        RevenueGrowthPct = @RevenueGrowthPct,
                        NewThisMonth = @NewThisMonth,
                        TotalAccounts = @TotalAccounts,
                        RefreshedAtUtc = SYSUTCDATETIME()
                WHEN NOT MATCHED THEN
                    INSERT (DealerId, AgentId, RevenueThisMonth, RevenueGrowthPct, NewThisMonth, TotalAccounts, RefreshedAtUtc)
                    VALUES (source.DealerId, source.AgentId, @RevenueThisMonth, @RevenueGrowthPct, @NewThisMonth, @TotalAccounts, SYSUTCDATETIME());";

            try
            {
                await _db.ExecuteAsync(sql, new
                {
                    scope.DealerId,
                    scope.AgentId,
                    RevenueThisMonth = revenueThisMonth,
                    RevenueGrowthPct = revenueGrowthPct,
                    NewThisMonth = newThisMonth,
                    TotalAccounts = totalAccounts,
                });
            }
            catch (SqlException ex) when (IsMissingTable(ex))
            {
                _logger.LogWarning(ex, "DashboardSnapshot table not found; skipping KPI refresh for scope {@Scope}. Apply Database/Dashboard/001_create_dashboard_tables.sql first.", scope);
            }
        }

        public async Task<int> RefreshCompletedContractsAsync(int topNPerScope = 20)
        {
            // FullContractValue mirrors ContractCalculatorService.CalculateOutstandingAmount's
            // "totalDue" -- the FULL contract term value (not day-elapsed-capped
            // like the arrears TotalDue in ComputePortfolioClassificationRollupAsync).
            // Status: Completed when the full value is paid off (LoanBalance <= 0);
            // UpsellTarget when 80%+ paid but not yet done. CompletedDate is
            // approximated as the account's last payment date -- there's no
            // stored "date it was paid off". Two ranked slices (per-dealer top N
            // and company-wide top N) are computed from the same classified set
            // and inserted together.
            const string sql = @"
                DELETE FROM DashboardCompletedContract;

                ;WITH ValidPayments AS (
                    SELECT COALESCE(op.AccountNoBigint, kp.AccountNoBigint) AS AccountNo, kp.AmountValue, kp.PaymentDateValue
                    FROM KosePayments kp
                    LEFT JOIN OrphanedPayments op ON op.MpesaCode = kp.MpesaCode
                ),
                PaymentTotals AS (
                    SELECT AccountNo, SUM(AmountValue) AS TotalPaid, MAX(PaymentDateValue) AS LastPaidDate
                    FROM ValidPayments
                    GROUP BY AccountNo
                ),
                Classified AS (
                    SELECT
                        dl.DealerId,
                        ci.ID AS AccountId,
                        ci.First_Name AS CustomerName,
                        dl.CompanyName AS DealerName,
                        ISNULL(NULLIF(LTRIM(RTRIM(ISNULL(d.Make, '') + ' ' + ISNULL(d.Model, ''))), ''), 'Unknown Device') AS ProductName,
                        CAST(ci.Term_in_Months AS INT) AS DurationMonths,
                        ISNULL(pt.TotalPaid, 0) AS TotalPaid,
                        pt.LastPaidDate,
                        (ci.Deposit + ci.Daily * 30 * ci.Term_in_Months + ci.Weekly * (30.0 / 7.0) * ci.Term_in_Months + ci.Monthly * ci.Term_in_Months) AS FullContractValue
                    FROM Contract_Info ci
                    INNER JOIN Devices d ON d.Id = ci.ID
                    INNER JOIN Dealers dl ON dl.DealerReference = d.DeviceGroupId
                    LEFT JOIN PaymentTotals pt ON pt.AccountNo = ci.ID
                    WHERE ci.StartDate IS NOT NULL
                ),
                Tiered AS (
                    SELECT
                        DealerId, AccountId, CustomerName, DealerName, ProductName, DurationMonths, TotalPaid, LastPaidDate,
                        (FullContractValue - TotalPaid) AS LoanBalance,
                        (TotalPaid / FullContractValue) * 100.0 AS RawPctComplete
                    FROM Classified
                    WHERE FullContractValue <> 0
                ),
                Qualifying AS (
                    SELECT
                        DealerId, AccountId, CustomerName, DealerName, ProductName, DurationMonths, TotalPaid, LastPaidDate,
                        CASE WHEN LoanBalance <= 0 THEN 'Completed' ELSE 'UpsellTarget' END AS Status,
                        CASE WHEN RawPctComplete > 100 THEN 100 ELSE RawPctComplete END AS PctComplete
                    FROM Tiered
                    WHERE LoanBalance <= 0 OR RawPctComplete >= 80
                ),
                RankedPerDealer AS (
                    SELECT *, ROW_NUMBER() OVER (
                        PARTITION BY DealerId
                        ORDER BY CASE WHEN Status = 'Completed' THEN 0 ELSE 1 END, LastPaidDate DESC, PctComplete DESC
                    ) AS Rn
                    FROM Qualifying
                ),
                RankedGlobal AS (
                    SELECT *, ROW_NUMBER() OVER (
                        ORDER BY CASE WHEN Status = 'Completed' THEN 0 ELSE 1 END, LastPaidDate DESC, PctComplete DESC
                    ) AS Rn
                    FROM Qualifying
                )
                INSERT INTO DashboardCompletedContract
                    (ScopeDealerId, ScopeAgentId, AccountId, CustomerName, DealerName, ProductName, CompletedDate, TotalPaid, DurationMonths, Status, PctComplete)
                SELECT DealerId, NULL, AccountId, CustomerName, DealerName, ProductName,
                       CASE WHEN Status = 'Completed' THEN CAST(LastPaidDate AS DATE) ELSE NULL END,
                       TotalPaid, DurationMonths, Status, PctComplete
                FROM RankedPerDealer WHERE Rn <= @TopN
                UNION ALL
                SELECT NULL, NULL, AccountId, CustomerName, DealerName, ProductName,
                       CASE WHEN Status = 'Completed' THEN CAST(LastPaidDate AS DATE) ELSE NULL END,
                       TotalPaid, DurationMonths, Status, PctComplete
                FROM RankedGlobal WHERE Rn <= @TopN;

                SELECT @@ROWCOUNT;"; // rows inserted (the last statement's count), not delete+insert combined

            try
            {
                return await _db.QuerySingleAsync<int>(sql, new { TopN = topNPerScope });
            }
            catch (SqlException ex) when (IsMissingTable(ex))
            {
                _logger.LogWarning(ex, "DashboardCompletedContract table not found; skipping completed-contracts refresh. Apply Database/Dashboard/001_create_dashboard_tables.sql and 002_extend_completed_contracts.sql first.");
                return 0;
            }
        }

        public async Task<int> RefreshDeviceStockAsync(int topNPerScope = 20)
        {
            // Same classification (DaysOverdue against the self-calculated
            // lock date) and FullContractValue formula as
            // ComputePortfolioClassificationRollupAsync / RefreshCompletedContractsAsync,
            // just grouped by device model (Devices.Make + Model) within each
            // dealer instead of aggregated dealer-wide. Dealer-only: Admin's
            // ProductPerformance has no rollup table (different shape).
            const string sql = @"
                DELETE FROM DashboardDeviceStock;

                ;WITH ValidPayments AS (
                    SELECT COALESCE(op.AccountNoBigint, kp.AccountNoBigint) AS AccountNo, kp.AmountValue
                    FROM KosePayments kp
                    LEFT JOIN OrphanedPayments op ON op.MpesaCode = kp.MpesaCode
                ),
                PaymentTotals AS (
                    SELECT AccountNo, SUM(AmountValue) AS TotalPaid
                    FROM ValidPayments
                    GROUP BY AccountNo
                ),
                AccountClassification AS (
                    SELECT
                        dl.DealerId,
                        ISNULL(NULLIF(LTRIM(RTRIM(ISNULL(d.Make, '') + ' ' + ISNULL(d.Model, ''))), ''), 'Unknown Device') AS DeviceName,
                        (ci.Deposit + ci.Daily * 30 * ci.Term_in_Months + ci.Weekly * (30.0 / 7.0) * ci.Term_in_Months + ci.Monthly * ci.Term_in_Months) AS FullContractValue,
                        ISNULL(pt.TotalPaid, 0)
                            - (
                                ci.Deposit
                                + ci.Daily * DaysAccrued.Days
                                + ci.Weekly * (DaysAccrued.Days / 7.0)
                                + ci.Monthly * (DaysAccrued.Days / 30.0)
                              ) AS Arrears,
                        (ci.Daily + (ci.Weekly / 7.0) + (ci.Monthly / 30.0)) AS DailyBlendedRate
                    FROM Contract_Info ci
                    INNER JOIN Devices d ON d.Id = ci.ID
                    INNER JOIN Dealers dl ON dl.DealerReference = d.DeviceGroupId
                    LEFT JOIN PaymentTotals pt ON pt.AccountNo = ci.ID
                    CROSS APPLY (
                        SELECT CASE
                            WHEN DATEDIFF(DAY, ci.StartDate, GETDATE()) < CAST(ci.Term_in_Months * 30 AS INT)
                                THEN DATEDIFF(DAY, ci.StartDate, GETDATE())
                            ELSE CAST(ci.Term_in_Months * 30 AS INT)
                        END AS Days
                    ) DaysAccrued
                    WHERE ci.StartDate IS NOT NULL
                ),
                Tiered AS (
                    SELECT
                        DealerId, DeviceName, FullContractValue,
                        CASE WHEN DailyBlendedRate = 0 THEN NULL ELSE -(Arrears / DailyBlendedRate) END AS DaysOverdue
                    FROM AccountClassification
                    WHERE DailyBlendedRate <> 0
                ),
                ByDevice AS (
                    SELECT
                        DealerId,
                        DeviceName,
                        COUNT(*) AS Units,
                        AVG(FullContractValue) AS AvgValue,
                        100.0 * SUM(CASE WHEN DaysOverdue <= 0 THEN 1 ELSE 0 END) / COUNT(*) AS GoodPct,
                        100.0 * SUM(CASE WHEN DaysOverdue > 7 THEN 1 ELSE 0 END) / COUNT(*) AS ArrearsPct
                    FROM Tiered
                    GROUP BY DealerId, DeviceName
                ),
                Ranked AS (
                    SELECT *, ROW_NUMBER() OVER (PARTITION BY DealerId ORDER BY Units DESC) AS Rn
                    FROM ByDevice
                )
                INSERT INTO DashboardDeviceStock (ScopeDealerId, ScopeAgentId, DeviceGroupId, DeviceName, Units, AvgValue, GoodPct, ArrearsPct)
                SELECT DealerId, NULL, NULL, DeviceName, Units, AvgValue, GoodPct, ArrearsPct
                FROM Ranked WHERE Rn <= @TopN;

                SELECT @@ROWCOUNT;";

            try
            {
                return await _db.QuerySingleAsync<int>(sql, new { TopN = topNPerScope });
            }
            catch (SqlException ex) when (IsMissingTable(ex))
            {
                _logger.LogWarning(ex, "DashboardDeviceStock table not found; skipping device-stock refresh. Apply Database/Dashboard/001_create_dashboard_tables.sql first.");
                return 0;
            }
        }

        public async Task<List<DashboardCommissionRollupRow>> ComputeCommissionSnapshotRollupAsync()
        {
            // AgentGrossCommission per account: 50% of Deposit vests immediately
            // (StartDate), plus 25% once 90+ days have passed -- both based on
            // CURRENT elapsed time, recomputed fresh each run (no historical
            // "was it in arrears exactly at day 90" snapshot is stored).
            // DealerCommissionEarned per account: 0.30 x (lifetime TotalPaid -
            // BuyingPrice - AgentGrossCommission), floored at 0.
            // Per-agent NetCommission pools (sums) AgentGrossCommission across
            // ALL of that agent's accounts, then subtracts the sum of CURRENT
            // arrears (day-elapsed-capped, same formula as
            // ComputePortfolioClassificationRollupAsync) across ALL their
            // accounts too, uncapped -- one account's arrears reduces the
            // whole pool, not just that account's own commission.
            // CommissionOutstanding (agent-facing, dealer's liability to pay
            // out) floors each agent's (NetCommission - Paid) at 0 before
            // summing across agents -- one agent's arrears wiping out their
            // own pool must not offset a genuinely-owed balance on another
            // agent. DealerCommissionOutstanding (Ranalo's liability to the
            // dealer) is CommissionReceived minus DealerCommissionPayments,
            // also floored at 0; distinct field, not to be confused with the
            // agent-facing one above.
            const string sql = @"
                ;WITH ValidPayments AS (
                    SELECT COALESCE(op.AccountNoBigint, kp.AccountNoBigint) AS AccountNo, kp.AmountValue
                    FROM KosePayments kp
                    LEFT JOIN OrphanedPayments op ON op.MpesaCode = kp.MpesaCode
                ),
                PaymentTotals AS (
                    SELECT AccountNo, SUM(AmountValue) AS TotalPaid
                    FROM ValidPayments
                    GROUP BY AccountNo
                ),
                AccountCommission AS (
                    SELECT
                        dl.DealerId,
                        ci.AssignedAgentId,
                        ci.ContractID,
                        ISNULL(pt.TotalPaid, 0) AS TotalPaid,
                        ISNULL(ci.BuyingPrice, 0) AS BuyingPrice,
                        (ci.Deposit * 0.50)
                            + (CASE WHEN DATEDIFF(DAY, ci.StartDate, GETDATE()) >= 90 THEN ci.Deposit * 0.25 ELSE 0 END)
                            AS AgentGrossCommission,
                        ISNULL(pt.TotalPaid, 0)
                            - (
                                ci.Deposit
                                + ci.Daily * DaysAccrued.Days
                                + ci.Weekly * (DaysAccrued.Days / 7.0)
                                + ci.Monthly * (DaysAccrued.Days / 30.0)
                              ) AS Arrears
                    FROM Contract_Info ci
                    INNER JOIN Devices d ON d.Id = ci.ID
                    INNER JOIN Dealers dl ON dl.DealerReference = d.DeviceGroupId
                    LEFT JOIN PaymentTotals pt ON pt.AccountNo = ci.ID
                    CROSS APPLY (
                        SELECT CASE
                            WHEN DATEDIFF(DAY, ci.StartDate, GETDATE()) < CAST(ci.Term_in_Months * 30 AS INT)
                                THEN DATEDIFF(DAY, ci.StartDate, GETDATE())
                            ELSE CAST(ci.Term_in_Months * 30 AS INT)
                        END AS Days
                    ) DaysAccrued
                    WHERE ci.StartDate IS NOT NULL AND ci.AssignedAgentId IS NOT NULL
                ),
                WithDealerCommission AS (
                    SELECT *,
                        CASE WHEN (TotalPaid - BuyingPrice - AgentGrossCommission) > 0
                             THEN (TotalPaid - BuyingPrice - AgentGrossCommission) * 0.30
                             ELSE 0 END AS DealerCommissionEarned
                    FROM AccountCommission
                ),
                AgentPaymentsAgg AS (
                    SELECT ContractId, SUM(ISNULL(AmountPaid, 0)) AS TotalAgentPaid
                    FROM AgentCommissionPayments
                    GROUP BY ContractId
                ),
                PerAgent AS (
                    SELECT
                        wdc.DealerId,
                        wdc.AssignedAgentId,
                        SUM(wdc.AgentGrossCommission) - SUM(CASE WHEN wdc.Arrears < 0 THEN -wdc.Arrears ELSE 0 END) AS NetCommission,
                        SUM(ISNULL(ap.TotalAgentPaid, 0)) AS Paid
                    FROM WithDealerCommission wdc
                    LEFT JOIN AgentPaymentsAgg ap ON ap.ContractId = wdc.ContractID
                    GROUP BY wdc.DealerId, wdc.AssignedAgentId
                ),
                PerDealerFromAgents AS (
                    SELECT
                        DealerId,
                        SUM(Paid) AS CommissionPaidToAgents,
                        -- Floored per agent before summing: one agent's
                        -- arrears wiping out their own pool shouldn't offset
                        -- what's genuinely still owed to a different agent.
                        SUM(CASE WHEN (NetCommission - Paid) > 0 THEN (NetCommission - Paid) ELSE 0 END) AS CommissionOutstanding
                    FROM PerAgent
                    GROUP BY DealerId
                ),
                PerDealerOwnCommission AS (
                    SELECT DealerId, SUM(DealerCommissionEarned) AS CommissionReceived
                    FROM WithDealerCommission
                    GROUP BY DealerId
                ),
                -- What Ranalo has actually paid the dealer, joined via
                -- ContractId (like AgentPaymentsAgg) rather than
                -- DealerCommissionPayments.DealerId -- existing usage
                -- elsewhere in this codebase (CommissionsRepository.cs)
                -- deliberately does the same, since that column's semantics
                -- aren't confirmed (only ContractId/AmountPaid are).
                DealerPaymentsAgg AS (
                    SELECT ContractId, SUM(ISNULL(AmountPaid, 0)) AS TotalDealerPaid
                    FROM DealerCommissionPayments
                    GROUP BY ContractId
                ),
                PerDealerPayments AS (
                    SELECT wdc.DealerId, SUM(ISNULL(dpa.TotalDealerPaid, 0)) AS TotalDealerPaid
                    FROM WithDealerCommission wdc
                    LEFT JOIN DealerPaymentsAgg dpa ON dpa.ContractId = wdc.ContractID
                    GROUP BY wdc.DealerId
                )
                SELECT
                    o.DealerId,
                    o.CommissionReceived,
                    ISNULL(a.CommissionPaidToAgents, 0) AS CommissionPaidToAgents,
                    ISNULL(a.CommissionOutstanding, 0) AS CommissionOutstanding,
                    CASE WHEN (o.CommissionReceived - ISNULL(dp.TotalDealerPaid, 0)) > 0
                         THEN o.CommissionReceived - ISNULL(dp.TotalDealerPaid, 0)
                         ELSE 0 END AS DealerCommissionOutstanding
                FROM PerDealerOwnCommission o
                LEFT JOIN PerDealerFromAgents a ON a.DealerId = o.DealerId
                LEFT JOIN PerDealerPayments dp ON dp.DealerId = o.DealerId";

            try
            {
                var rows = await _db.QueryAsync<DashboardCommissionRollupRow>(sql);
                return rows.ToList();
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "Commission snapshot rollup computation failed");
                return new List<DashboardCommissionRollupRow>();
            }
        }

        public async Task UpsertSnapshotCommissionAsync(
            DashboardScope scope,
            decimal? commissionReceived,
            decimal? commissionPaidToAgents,
            decimal? commissionOutstanding)
        {
            const string sql = @"
                MERGE DashboardSnapshot AS target
                USING (SELECT @DealerId AS DealerId, @AgentId AS AgentId) AS source
                ON  (target.DealerId = source.DealerId OR (target.DealerId IS NULL AND source.DealerId IS NULL))
                AND (target.AgentId = source.AgentId OR (target.AgentId IS NULL AND source.AgentId IS NULL))
                WHEN MATCHED THEN
                    UPDATE SET
                        CommissionReceived = @CommissionReceived,
                        CommissionPaidToAgents = @CommissionPaidToAgents,
                        CommissionOutstanding = @CommissionOutstanding,
                        RefreshedAtUtc = SYSUTCDATETIME()
                WHEN NOT MATCHED THEN
                    INSERT (DealerId, AgentId, CommissionReceived, CommissionPaidToAgents, CommissionOutstanding, RefreshedAtUtc)
                    VALUES (source.DealerId, source.AgentId, @CommissionReceived, @CommissionPaidToAgents, @CommissionOutstanding, SYSUTCDATETIME());";

            try
            {
                await _db.ExecuteAsync(sql, new
                {
                    scope.DealerId,
                    scope.AgentId,
                    CommissionReceived = commissionReceived,
                    CommissionPaidToAgents = commissionPaidToAgents,
                    CommissionOutstanding = commissionOutstanding,
                });
            }
            catch (SqlException ex) when (IsMissingTable(ex))
            {
                _logger.LogWarning(ex, "DashboardSnapshot table not found; skipping commission refresh for scope {@Scope}. Apply Database/Dashboard/003_add_commission_snapshot_fields.sql first.", scope);
            }
        }

        // Separate from UpsertSnapshotCommissionAsync above (rather than one
        // extra column on the same MERGE) so that a not-yet-applied
        // 004_add_dealer_commission_outstanding.sql migration only skips
        // this one field, not the three already-working commission columns
        // too -- a single MERGE statement fails all-or-nothing on a missing
        // column.
        public async Task UpsertDealerCommissionOutstandingAsync(DashboardScope scope, decimal? dealerCommissionOutstanding)
        {
            const string sql = @"
                MERGE DashboardSnapshot AS target
                USING (SELECT @DealerId AS DealerId, @AgentId AS AgentId) AS source
                ON  (target.DealerId = source.DealerId OR (target.DealerId IS NULL AND source.DealerId IS NULL))
                AND (target.AgentId = source.AgentId OR (target.AgentId IS NULL AND source.AgentId IS NULL))
                WHEN MATCHED THEN
                    UPDATE SET
                        DealerCommissionOutstanding = @DealerCommissionOutstanding,
                        RefreshedAtUtc = SYSUTCDATETIME()
                WHEN NOT MATCHED THEN
                    INSERT (DealerId, AgentId, DealerCommissionOutstanding, RefreshedAtUtc)
                    VALUES (source.DealerId, source.AgentId, @DealerCommissionOutstanding, SYSUTCDATETIME());";

            try
            {
                await _db.ExecuteAsync(sql, new
                {
                    scope.DealerId,
                    scope.AgentId,
                    DealerCommissionOutstanding = dealerCommissionOutstanding,
                });
            }
            catch (SqlException ex) when (IsMissingTable(ex))
            {
                _logger.LogWarning(ex, "DashboardSnapshot.DealerCommissionOutstanding column not found; skipping for scope {@Scope}. Apply Database/Dashboard/004_add_dealer_commission_outstanding.sql first.", scope);
            }
        }

        public async Task<int> RefreshAgentCommissionListAsync(int topNPerScope = 20)
        {
            const string sql = @"
                DELETE FROM DashboardPerformanceEntry WHERE EntryType = 'AgentCommission';

                ;WITH ValidPayments AS (
                    SELECT COALESCE(op.AccountNoBigint, kp.AccountNoBigint) AS AccountNo, kp.AmountValue
                    FROM KosePayments kp
                    LEFT JOIN OrphanedPayments op ON op.MpesaCode = kp.MpesaCode
                ),
                PaymentTotals AS (
                    SELECT AccountNo, SUM(AmountValue) AS TotalPaid
                    FROM ValidPayments
                    GROUP BY AccountNo
                ),
                AccountCommission AS (
                    SELECT
                        dl.DealerId,
                        ci.AssignedAgentId,
                        ci.ContractID,
                        (ci.Deposit * 0.50)
                            + (CASE WHEN DATEDIFF(DAY, ci.StartDate, GETDATE()) >= 90 THEN ci.Deposit * 0.25 ELSE 0 END)
                            AS AgentGrossCommission,
                        ISNULL(pt.TotalPaid, 0)
                            - (
                                ci.Deposit
                                + ci.Daily * DaysAccrued.Days
                                + ci.Weekly * (DaysAccrued.Days / 7.0)
                                + ci.Monthly * (DaysAccrued.Days / 30.0)
                              ) AS Arrears
                    FROM Contract_Info ci
                    INNER JOIN Devices d ON d.Id = ci.ID
                    INNER JOIN Dealers dl ON dl.DealerReference = d.DeviceGroupId
                    LEFT JOIN PaymentTotals pt ON pt.AccountNo = ci.ID
                    CROSS APPLY (
                        SELECT CASE
                            WHEN DATEDIFF(DAY, ci.StartDate, GETDATE()) < CAST(ci.Term_in_Months * 30 AS INT)
                                THEN DATEDIFF(DAY, ci.StartDate, GETDATE())
                            ELSE CAST(ci.Term_in_Months * 30 AS INT)
                        END AS Days
                    ) DaysAccrued
                    WHERE ci.StartDate IS NOT NULL AND ci.AssignedAgentId IS NOT NULL
                ),
                AgentPaymentsAgg AS (
                    SELECT ContractId, SUM(ISNULL(AmountPaid, 0)) AS TotalAgentPaid
                    FROM AgentCommissionPayments
                    GROUP BY ContractId
                ),
                PerAgent AS (
                    SELECT
                        ac.DealerId,
                        ac.AssignedAgentId AS AgentId,
                        u.[Name] + ' ' + u.[LastName] AS AgentName,
                        COUNT(*) AS Accounts,
                        SUM(ac.AgentGrossCommission) - SUM(CASE WHEN ac.Arrears < 0 THEN -ac.Arrears ELSE 0 END) AS NetCommission,
                        SUM(ISNULL(ap.TotalAgentPaid, 0)) AS Paid
                    FROM AccountCommission ac
                    INNER JOIN Users u ON u.UserId = ac.AssignedAgentId
                    LEFT JOIN AgentPaymentsAgg ap ON ap.ContractId = ac.ContractID
                    GROUP BY ac.DealerId, ac.AssignedAgentId, u.[Name], u.[LastName]
                ),
                Ranked AS (
                    SELECT *, ROW_NUMBER() OVER (PARTITION BY DealerId ORDER BY (NetCommission - Paid) DESC) AS Rn
                    FROM PerAgent
                )
                INSERT INTO DashboardPerformanceEntry
                    (ScopeDealerId, ScopeAgentId, EntryType, Rank, SubjectId, SubjectName, ParentName, Accounts, ActivePct, Revenue, CommissionPaid, CommissionDue, PctOfTarget)
                SELECT DealerId, NULL, 'AgentCommission', Rn, AgentId, AgentName, NULL, Accounts, 0, NULL, Paid, NetCommission, 0
                FROM Ranked WHERE Rn <= @TopN;

                SELECT @@ROWCOUNT;";

            try
            {
                return await _db.QuerySingleAsync<int>(sql, new { TopN = topNPerScope });
            }
            catch (SqlException ex) when (IsMissingTable(ex))
            {
                _logger.LogWarning(ex, "DashboardPerformanceEntry table not found; skipping agent commission list refresh. Apply Database/Dashboard/001_create_dashboard_tables.sql first.");
                return 0;
            }
        }

        // SQL Server error 208 = "Invalid object name" (table/view does not
        // exist); 207 = "Invalid column name" (table exists, a column added
        // by a later migration doesn't yet -- e.g. DealerCommissionOutstanding
        // before 004_add_dealer_commission_outstanding.sql runs). Both mean
        // "this migration hasn't been applied to this database yet" from the
        // caller's point of view, and should degrade the same way: this app's
        // DB credentials are DML-only (confirmed via a failed ALTER TABLE
        // attempt), so schema changes are applied out-of-band by whoever runs
        // the Database/Dashboard/*.sql scripts, not by this process.
        private static bool IsMissingTable(SqlException ex) => ex.Number is 208 or 207;
    }
}
