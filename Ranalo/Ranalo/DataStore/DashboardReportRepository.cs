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
                        kp.AmountValue
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
                        DealerId,
                        Arrears,
                        CASE WHEN DailyBlendedRate = 0 THEN NULL ELSE -(Arrears / DailyBlendedRate) END AS DaysOverdue
                    FROM AccountClassification
                    WHERE DailyBlendedRate <> 0 -- accounts with no payment plan can't be classified
                )
                SELECT
                    DealerId,
                    100.0 * SUM(CASE WHEN DaysOverdue <= 0 THEN 1 ELSE 0 END) / COUNT(*) AS GoodPct,
                    100.0 * SUM(CASE WHEN DaysOverdue > 0 AND DaysOverdue <= 7 THEN 1 ELSE 0 END) / COUNT(*) AS SlowPct,
                    100.0 * SUM(CASE WHEN DaysOverdue > 7 THEN 1 ELSE 0 END) / COUNT(*) AS ArrearsPct,
                    100.0 * SUM(CASE WHEN DaysOverdue > 90 THEN 1 ELSE 0 END) / COUNT(*) AS NonPayingPct,
                    SUM(CASE WHEN Arrears < 0 THEN -Arrears ELSE 0 END) AS ArrearsTotal
                FROM Tiered
                GROUP BY GROUPING SETS ((DealerId), ())";

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
            decimal? arrearsTotal)
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
                        RefreshedAtUtc = SYSUTCDATETIME()
                WHEN NOT MATCHED THEN
                    INSERT (DealerId, AgentId, PortfolioGoodPct, PortfolioSlowPct, PortfolioArrearsPct, PortfolioNonPayingPct, ArrearsTotal, RefreshedAtUtc)
                    VALUES (source.DealerId, source.AgentId, @PortfolioGoodPct, @PortfolioSlowPct, @PortfolioArrearsPct, @PortfolioNonPayingPct, @ArrearsTotal, SYSUTCDATETIME());";

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
                        d.Make + ' ' + d.Model AS ProductName,
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
                        d.Make + ' ' + d.Model AS DeviceName,
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
                    SELECT DealerId, SUM(Paid) AS CommissionPaidToAgents, SUM(NetCommission - Paid) AS CommissionOutstanding
                    FROM PerAgent
                    GROUP BY DealerId
                ),
                PerDealerOwnCommission AS (
                    SELECT DealerId, SUM(DealerCommissionEarned) AS CommissionReceived
                    FROM WithDealerCommission
                    GROUP BY DealerId
                )
                SELECT
                    o.DealerId,
                    o.CommissionReceived,
                    ISNULL(a.CommissionPaidToAgents, 0) AS CommissionPaidToAgents,
                    ISNULL(a.CommissionOutstanding, 0) AS CommissionOutstanding
                FROM PerDealerOwnCommission o
                LEFT JOIN PerDealerFromAgents a ON a.DealerId = o.DealerId";

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

        // SQL Server error 208 = "Invalid object name" (table/view does not exist).
        private static bool IsMissingTable(SqlException ex) => ex.Number == 208;
    }
}
