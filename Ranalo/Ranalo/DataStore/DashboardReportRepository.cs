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
            //
            // TotalAccountsAsOfPeriod/NewAccountsInPeriod/NewAccountsPriorPeriod
            // back the Total Accounts card's own period filter (separate concept
            // from TotalAccounts/AvgPerAccount above): how many accounts existed
            // by the end of the selected window, how many started within it, and
            // how many started in the prior window of equal length, so the view
            // can show a period-over-period "new accounts" comparison.
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
                    (SELECT COUNT(*)
                     FROM Contract_Info ci4
                     INNER JOIN Devices d4 ON d4.Id = ci4.ID
                     INNER JOIN Dealers dl4 ON dl4.DealerReference = d4.DeviceGroupId
                     WHERE dl4.DealerId = @DealerId AND ci4.StartDate IS NOT NULL
                       AND ci4.StartDate < @PeriodEnd) AS TotalAccountsAsOfPeriod,
                    (SELECT COUNT(*)
                     FROM Contract_Info ci4
                     INNER JOIN Devices d4 ON d4.Id = ci4.ID
                     INNER JOIN Dealers dl4 ON dl4.DealerReference = d4.DeviceGroupId
                     WHERE dl4.DealerId = @DealerId AND ci4.StartDate >= @PeriodStart
                       AND ci4.StartDate < @PeriodEnd) AS NewAccountsInPeriod,
                    (SELECT COUNT(*)
                     FROM Contract_Info ci4
                     INNER JOIN Devices d4 ON d4.Id = ci4.ID
                     INNER JOIN Dealers dl4 ON dl4.DealerReference = d4.DeviceGroupId
                     WHERE dl4.DealerId = @DealerId AND ci4.StartDate >= @PriorPeriodStart
                       AND ci4.StartDate < @PriorPeriodEnd) AS NewAccountsPriorPeriod,
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

        // Formats Devices.NextLockDateIsoFormat has actually been written in
        // (despite its name, it is NOT real ISO 8601) -- see
        // TimestampHelper.FormatRelockTimestamp for the primary one and
        // ScheduledLockPaying.DateTimeFormat for the same legacy-format
        // tolerance this mirrors. Classifying in C# rather than risking a
        // silently wrong SQL-side date conversion on a mixed-format column.
        private static readonly string[] NextLockDateFormats =
        {
            "dd/MM/yyyy'T'HH:mm:ss",
            "dd/MM/yyyy'T'HH:mm:ss.FFFFFFF",
            "dd/MM/yyyy HH:mm:ss",
            "yyyy-MM-dd HH:mm:ss",
            "d/M/yyyy h:mm:ss tt",
        };

        private static DateTime? ParseNextLockDate(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            if (DateTime.TryParseExact(raw, NextLockDateFormats, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var parsed))
            {
                return parsed;
            }

            return DateTime.TryParse(raw, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out parsed) ? parsed : null;
        }

        public async Task<DashboardLockClassificationRow> GetDealerLockClassificationAsync(int dealerId)
        {
            // Devices.NextLockDateIsoFormat, not the accrual-based "days
            // overdue" formula ComputePortfolioClassificationRollupAsync
            // uses -- that formula derives arrears purely from the account's
            // original Deposit/Daily/Weekly/Monthly rates and never learns
            // about a restructuring, so a restructured customer current on
            // their NEW plan would still show as overdue against their OLD
            // one. NextLockDateIsoFormat is recalculated through
            // restructuring elsewhere in this codebase (see
            // ApplicationReportService's restructured branch), so it is the
            // authoritative "when is this account next due to be locked"
            // date -- in the future (or unset) for an account paying on
            // schedule or ahead (including a restructured one), in the past
            // for one genuinely behind. Same four-tier split as
            // ComputePortfolioClassificationRollupAsync's DaysOverdue tiers
            // (<=0 Good, 1-7 Slow, >7 Arrears, >90 NonPaying -- the last is a
            // subset of Arrears, not exclusive), just measured off
            // NextLockDate instead of the accrual formula. No scheduled lock
            // date at all counts as good (never yet due, not evidence of
            // non-payment).
            const string sql = @"
                SELECT d.NextLockDateIsoFormat
                FROM Contract_Info ci
                INNER JOIN Devices d ON d.Id = ci.ID
                INNER JOIN Dealers dl ON dl.DealerReference = d.DeviceGroupId
                WHERE dl.DealerId = @DealerId AND ci.StartDate IS NOT NULL";

            try
            {
                var rawDates = await _db.QueryAsync<string?>(sql, new { DealerId = dealerId });
                var now = DateTime.Now;
                var result = new DashboardLockClassificationRow();

                foreach (var raw in rawDates)
                {
                    var nextLockDate = ParseNextLockDate(raw);
                    var daysPastLock = nextLockDate.HasValue ? (now - nextLockDate.Value).TotalDays : 0;

                    if (daysPastLock > 7)
                    {
                        result.ArrearsCount++;
                        if (daysPastLock > 90)
                        {
                            result.NonPayingCount++;
                        }
                    }
                    else if (daysPastLock > 0)
                    {
                        result.SlowCount++;
                    }
                    else
                    {
                        result.GoodCount++;
                    }
                }

                return result;
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "Dealer lock-classification query failed for dealer {DealerId}", dealerId);
                return new DashboardLockClassificationRow();
            }
        }

        private class DeviceLockDateRow
        {
            public string DeviceName { get; set; } = "";
            public string? LockDate { get; set; }
        }

        public async Task<Dictionary<string, DashboardLockClassificationRow>> GetDealerDeviceLockClassificationAsync(int dealerId)
        {
            // Same NextLockDate-based good/arrears split as
            // GetDealerLockClassificationAsync, grouped by device instead of
            // dealer-wide -- replaces RefreshDeviceStockAsync's accrual-based
            // per-device GoodPct/ArrearsPct for the same restructuring reason.
            // DeviceName derivation matches RefreshDeviceStockAsync exactly so
            // rows line up by name.
            const string sql = @"
                SELECT
                    ISNULL(NULLIF(LTRIM(RTRIM(ISNULL(d.Make, '') + ' ' + ISNULL(d.Model, ''))), ''), 'Unknown Device') AS DeviceName,
                    d.NextLockDateIsoFormat AS LockDate
                FROM Contract_Info ci
                INNER JOIN Devices d ON d.Id = ci.ID
                INNER JOIN Dealers dl ON dl.DealerReference = d.DeviceGroupId
                WHERE dl.DealerId = @DealerId AND ci.StartDate IS NOT NULL";

            try
            {
                var rows = await _db.QueryAsync<DeviceLockDateRow>(sql, new { DealerId = dealerId });
                var now = DateTime.Now;
                var result = new Dictionary<string, DashboardLockClassificationRow>();

                foreach (var row in rows)
                {
                    if (!result.TryGetValue(row.DeviceName, out var bucket))
                    {
                        bucket = new DashboardLockClassificationRow();
                        result[row.DeviceName] = bucket;
                    }

                    var nextLockDate = ParseNextLockDate(row.LockDate);
                    var daysPastLock = nextLockDate.HasValue ? (now - nextLockDate.Value).TotalDays : 0;

                    if (daysPastLock > 7)
                    {
                        bucket.ArrearsCount++;
                    }
                    else
                    {
                        bucket.GoodCount++;
                    }
                }

                return result;
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "Dealer device lock-classification query failed for dealer {DealerId}", dealerId);
                return new Dictionary<string, DashboardLockClassificationRow>();
            }
        }

        private class AccountArrearsRow
        {
            public decimal Arrears { get; set; }
            public string? LockDate { get; set; }
            public bool IsFullTermElapsed { get; set; }
            public decimal PaidThisMonth { get; set; }
        }

        public async Task<DashboardArrearsClassificationRow> GetDealerArrearsClassificationAsync(int dealerId)
        {
            // Same accrual Arrears $ formula as ComputePortfolioClassificationRollupAsync
            // (Deposit + Daily/Weekly/Monthly accrual since StartDate, capped
            // at the contract's term, vs TotalPaid) -- only the population
            // summed changes: "true" = NextLockDate already passed (more than
            // 0 days -- no 7-day grace here, unlike the Paying vs Non-Paying
            // card, since even 1 day past a lock date is real exposure for a
            // dollar total); "restructured" = still shows a shortfall but
            // NextLockDate is in the future or unset, i.e. being paid down on
            // a plan rather than truly delinquent.
            const string sql = @"
                ;WITH ValidPayments AS (
                    SELECT COALESCE(op.AccountNoBigint, kp.AccountNoBigint) AS AccountNo, kp.AmountValue, kp.PaymentDateValue
                    FROM KosePayments kp
                    LEFT JOIN OrphanedPayments op ON op.MpesaCode = kp.MpesaCode
                ),
                PaymentTotals AS (
                    SELECT AccountNo, SUM(AmountValue) AS TotalPaid
                    FROM ValidPayments
                    GROUP BY AccountNo
                ),
                PaymentTotalsThisMonth AS (
                    SELECT AccountNo, SUM(AmountValue) AS PaidThisMonth
                    FROM ValidPayments
                    WHERE MONTH(PaymentDateValue) = MONTH(GETDATE()) AND YEAR(PaymentDateValue) = YEAR(GETDATE())
                    GROUP BY AccountNo
                )
                SELECT
                    ISNULL(pt.TotalPaid, 0) - (
                        ci.Deposit
                        + ci.Daily * DaysAccrued.Days
                        + ci.Weekly * (DaysAccrued.Days / 7.0)
                        + ci.Monthly * (DaysAccrued.Days / 30.0)
                    ) AS Arrears,
                    d.NextLockDateIsoFormat AS LockDate,
                    CASE WHEN DATEDIFF(DAY, ci.StartDate, GETDATE()) >= CAST(ci.Term_in_Months * 30 AS INT)
                         THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END AS IsFullTermElapsed,
                    ISNULL(ptm.PaidThisMonth, 0) AS PaidThisMonth
                FROM Contract_Info ci
                INNER JOIN Devices d ON d.Id = ci.ID
                INNER JOIN Dealers dl ON dl.DealerReference = d.DeviceGroupId
                LEFT JOIN PaymentTotals pt ON pt.AccountNo = ci.ID
                LEFT JOIN PaymentTotalsThisMonth ptm ON ptm.AccountNo = ci.ID
                CROSS APPLY (
                    SELECT CASE
                        WHEN DATEDIFF(DAY, ci.StartDate, GETDATE()) < CAST(ci.Term_in_Months * 30 AS INT)
                            THEN DATEDIFF(DAY, ci.StartDate, GETDATE())
                        ELSE CAST(ci.Term_in_Months * 30 AS INT)
                    END AS Days
                ) DaysAccrued
                WHERE dl.DealerId = @DealerId AND ci.StartDate IS NOT NULL";

            try
            {
                var rows = await _db.QueryAsync<AccountArrearsRow>(sql, new { DealerId = dealerId });
                var now = DateTime.Now;
                var result = new DashboardArrearsClassificationRow();
                decimal totalDaysLocked = 0;
                decimal totalBadDebtDaysLocked = 0;

                foreach (var row in rows)
                {
                    if (row.Arrears >= 0)
                    {
                        continue; // no dollar shortfall -- not in arrears at all
                    }

                    var shortfall = -row.Arrears;
                    var nextLockDate = ParseNextLockDate(row.LockDate);
                    var daysPastLock = nextLockDate.HasValue ? (now - nextLockDate.Value).TotalDays : 0;

                    if (daysPastLock > 0)
                    {
                        result.TrueArrearsTotal += shortfall;
                        result.TrueArrearsCount++;
                        totalDaysLocked += (decimal)daysPastLock;

                        if (daysPastLock > 90)
                        {
                            result.BadDebtTotal += shortfall;
                            result.BadDebtCount++;
                            totalBadDebtDaysLocked += (decimal)daysPastLock;
                        }
                    }
                    else
                    {
                        result.RestructuredArrearsTotal += shortfall;
                        result.RestructuredArrearsCount++;
                    }

                    // Independent of lock status -- a write-off is about the
                    // contract's term being over, not about whether it was
                    // ever locked.
                    if (row.IsFullTermElapsed)
                    {
                        result.WriteOffTotal += shortfall;
                        result.WriteOffCount++;
                        result.WriteOffRecoveredThisMonth += row.PaidThisMonth;
                    }
                }

                result.TrueArrearsAvgDaysLocked = result.TrueArrearsCount > 0
                    ? Math.Round(totalDaysLocked / result.TrueArrearsCount, 1)
                    : 0;
                result.BadDebtAvgDaysLocked = result.BadDebtCount > 0
                    ? Math.Round(totalBadDebtDaysLocked / result.BadDebtCount, 1)
                    : 0;

                return result;
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "Dealer arrears-classification query failed for dealer {DealerId}", dealerId);
                return new DashboardArrearsClassificationRow();
            }
        }

        public async Task<decimal> GetDealerAgentCommissionPaidForPeriodAsync(int dealerId, DateTime periodStart, DateTime periodEndExclusive)
        {
            const string sql = @"
                SELECT ISNULL(SUM(acp.AmountPaid), 0)
                FROM AgentCommissionPayments acp
                INNER JOIN Contract_Info ci ON ci.ContractID = acp.ContractId
                INNER JOIN Devices d ON d.Id = ci.ID
                INNER JOIN Dealers dl ON dl.DealerReference = d.DeviceGroupId
                WHERE dl.DealerId = @DealerId
                  AND acp.PaymentDate >= @PeriodStart AND acp.PaymentDate < @PeriodEnd";

            try
            {
                return await _db.QuerySingleAsync<decimal>(sql, new
                {
                    DealerId = dealerId,
                    PeriodStart = periodStart,
                    PeriodEnd = periodEndExclusive,
                });
            }
            catch (SqlException ex) when (IsMissingTable(ex))
            {
                _logger.LogWarning(ex, "AgentCommissionPayments table not found; returning 0 for dealer {DealerId}. Apply Database/Commissions/001_create_agent_commission_payments.sql first.", dealerId);
                return 0;
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "Dealer agent-commission-paid-for-period query failed for dealer {DealerId}", dealerId);
                return 0;
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

        private class AgentCommissionAccountRow
        {
            public int DealerId { get; set; }

            // Null for direct dealer sales with no agent -- these still
            // contribute to DealerCommissionEarned (see the query comment
            // below), just never enter the per-agent pooling.
            public int? AssignedAgentId { get; set; }
            public decimal AgentGrossCommission { get; set; }
            public decimal TotalPaid { get; set; }

            // Null (not 0) when Contract_Info.BuyingPrice was never recorded
            // -- a real data gap, not a free device. DealerCommissionEarned
            // is computed in C# (not SQL) specifically so a missing value
            // can be excluded from the calculation entirely instead of
            // silently defaulting to 0, which would understate the dealer's
            // cost of sale and overstate their commission. See
            // ComputeCommissionSnapshotRollupAsync's aggregation.
            public decimal? BuyingPrice { get; set; }

            public decimal Arrears { get; set; }
            public string? LockDate { get; set; }
            public decimal AgentPaid { get; set; }
            public decimal DealerPaid { get; set; }
        }

        public async Task<List<DashboardCommissionRollupRow>> ComputeCommissionSnapshotRollupAsync()
        {
            // AgentGrossCommission per account: 50% of Deposit vests immediately
            // (StartDate), plus 25% once 90+ days have passed -- both based on
            // CURRENT elapsed time, recomputed fresh each run (no historical
            // "was it in arrears exactly at day 90" snapshot is stored).
            // Computed the SAME way for every account regardless of whether
            // ci.AssignedAgentId is set -- deliberately NOT zeroed out for
            // direct dealer sales. If it were, a dealer's earned commission
            // would jump the moment an agent was unassigned from a contract
            // (or never assigned in the first place), creating a fraud
            // incentive to skip agent assignment purely to inflate
            // DealerCommissionEarned below. Since this "standard" 50%+25%
            // agent-fee rate is fixed/known regardless of whether a real
            // agent collects it, applying it uniformly makes the dealer's
            // commission blind to agent-assignment status, as it should be.
            // Only PerAgent (below, filtered to AssignedAgentId IS NOT NULL)
            // treats this figure as money actually owed to a specific agent.
            //
            // DealerCommissionEarned per account: 0.30 x (lifetime TotalPaid -
            // BuyingPrice - AgentGrossCommission), floored at 0 -- i.e. once
            // TotalPaid clears the (BuyingPrice + AgentGrossCommission)
            // threshold, the dealer earns 30% of everything paid beyond it.
            // Based on actual TotalPaid, not an overdue estimate, so
            // unaffected by the restructuring issue below. Computed for
            // EVERY paid account (StartDate IS NOT NULL), not just
            // agent-assigned ones -- dealer commission is independent of
            // agent commission and is earned on all of a dealer's sales,
            // including direct ones with no agent. (This population was
            // previously wrongly restricted to AssignedAgentId IS NOT NULL,
            // the same filter agent commission needs -- given how few
            // accounts have an agent assigned at all, that undercounted
            // almost every dealer's earned commission.)
            //
            // Computed in C# (not SQL) specifically so an account with no
            // recorded BuyingPrice can be EXCLUDED from the sum entirely,
            // rather than ISNULL'd to 0 -- a missing device cost is a data
            // gap that should be visibly flagged (DealerCommissionMissingCostCount)
            // and fixed at the source, not silently treated as "this device
            // was free", which would understate cost and overstate the
            // dealer's commission the same way the agent-assignment bug did.
            //
            // Per-agent NetCommission pools (sums) AgentGrossCommission across
            // ALL of that agent's accounts, then subtracts dollar arrears --
            // but only from accounts that are genuinely "true arrears" (more
            // than 0 days past Devices.NextLockDateIsoFormat), same
            // methodology as GetDealerArrearsClassificationAsync/the Total
            // Arrears card, not the old accrual-based figure for every
            // account regardless of restructuring status. Deliberately
            // switched off SQL-side aggregation for this one step (pulls
            // per-account rows and aggregates in C#) because NextLockDate
            // needs the same tolerant multi-format parse ParseNextLockDate
            // already uses elsewhere -- not reliably convertible in T-SQL.
            // Runs once nightly across the whole system, so the per-row pull
            // is not a live-request cost.
            //
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
                        -- NOT wrapped in ISNULL(..., 0) -- a missing
                        -- BuyingPrice needs to be visibly excluded from the
                        -- dealer commission calc, not silently treated as a
                        -- free device (see AgentCommissionAccountRow.BuyingPrice).
                        ci.BuyingPrice AS BuyingPrice,
                        -- Computed unconditionally for EVERY account, agent
                        -- assigned or not (see the class comment above) --
                        -- this is the dealer's cost-of-sale deduction, not a
                        -- claim that a real agent is owed this amount. Only
                        -- PerAgent below (filtered to AssignedAgentId IS NOT
                        -- NULL) treats it as money actually owed to someone.
                        (ci.Deposit * 0.50)
                            + (CASE WHEN DATEDIFF(DAY, ci.StartDate, GETDATE()) >= 90 THEN ci.Deposit * 0.25 ELSE 0 END)
                            AS AgentGrossCommission,
                        ISNULL(pt.TotalPaid, 0)
                            - (
                                ci.Deposit
                                + ci.Daily * DaysAccrued.Days
                                + ci.Weekly * (DaysAccrued.Days / 7.0)
                                + ci.Monthly * (DaysAccrued.Days / 30.0)
                              ) AS Arrears,
                        d.NextLockDateIsoFormat AS LockDate
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
                AgentPaymentsAgg AS (
                    SELECT ContractId, SUM(ISNULL(AmountPaid, 0)) AS TotalAgentPaid
                    FROM AgentCommissionPayments
                    GROUP BY ContractId
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
                )
                SELECT
                    ac.DealerId,
                    ac.AssignedAgentId,
                    ac.AgentGrossCommission,
                    ac.TotalPaid,
                    ac.BuyingPrice,
                    ac.Arrears,
                    ac.LockDate,
                    ISNULL(ap.TotalAgentPaid, 0) AS AgentPaid,
                    ISNULL(dp.TotalDealerPaid, 0) AS DealerPaid
                FROM AccountCommission ac
                LEFT JOIN AgentPaymentsAgg ap ON ap.ContractId = ac.ContractID
                LEFT JOIN DealerPaymentsAgg dp ON dp.ContractId = ac.ContractID";

            try
            {
                var accountRows = await _db.QueryAsync<AgentCommissionAccountRow>(sql);
                var now = DateTime.Now;

                return accountRows
                    .GroupBy(r => r.DealerId)
                    .Select(dealerGroup =>
                    {
                        // Direct dealer sales (AssignedAgentId null) still
                        // count toward DealerCommissionEarned/AccountCount
                        // below (dealerGroup, unfiltered), but never enter
                        // the per-agent pool -- there's no agent to net
                        // arrears against or pay out.
                        var perAgent = dealerGroup
                            .Where(r => r.AssignedAgentId.HasValue)
                            .GroupBy(r => r.AssignedAgentId!.Value)
                            .Select(agentGroup =>
                            {
                                var gross = agentGroup.Sum(r => r.AgentGrossCommission);
                                var trueArrearsDeduction = agentGroup
                                    .Where(r => IsPastLockDate(r.LockDate, now))
                                    .Sum(r => r.Arrears < 0 ? -r.Arrears : 0);
                                var paid = agentGroup.Sum(r => r.AgentPaid);
                                // Can't withhold more than the agent actually
                                // earned -- deduction beyond gross just means
                                // the pool is fully wiped, not "negatively withheld".
                                var withheld = Math.Min(trueArrearsDeduction, gross);
                                return (NetCommission: gross - trueArrearsDeduction, Paid: paid, Withheld: withheld);
                            })
                            .ToList();

                        // Rows with no recorded BuyingPrice are excluded from
                        // DealerCommissionEarned/DealerCommissionAccountCount
                        // entirely (see AgentCommissionAccountRow.BuyingPrice)
                        // and counted separately so the gap is visible.
                        var withCost = dealerGroup.Where(r => r.BuyingPrice.HasValue).ToList();
                        var commissionReceived = withCost.Sum(r =>
                            Math.Max(0, r.TotalPaid - r.BuyingPrice!.Value - r.AgentGrossCommission) * 0.30m);
                        var totalDealerPaid = dealerGroup.Sum(r => r.DealerPaid);

                        // Incentive figure: how much MORE dealer commission
                        // would be unlocked if every true-arrears account
                        // (locked, more than 0 days past NextLockDate) paid
                        // off its shortfall today. Approximates the marginal
                        // commission on that shortfall as a flat 30% -- exact
                        // for accounts already past their (BuyingPrice +
                        // AgentGrossCommission) threshold, a slight
                        // overstatement for ones still climbing toward it,
                        // same class of simplification as the Agent
                        // Commissions card's own "withheld" figure.
                        var dealerCommissionWithheldForArrears = withCost
                            .Where(r => IsPastLockDate(r.LockDate, now))
                            .Sum(r => (r.Arrears < 0 ? -r.Arrears : 0) * 0.30m);

                        return new DashboardCommissionRollupRow
                        {
                            DealerId = dealerGroup.Key,
                            CommissionReceived = commissionReceived,
                            CommissionPaidToAgents = perAgent.Sum(a => a.Paid),
                            // Floored per agent before summing: one agent's
                            // arrears wiping out their own pool shouldn't
                            // offset what's genuinely still owed to a
                            // different agent.
                            CommissionOutstanding = perAgent.Sum(a => Math.Max(0, a.NetCommission - a.Paid)),
                            DealerCommissionOutstanding = Math.Max(0, commissionReceived - totalDealerPaid),
                            // Agent Commissions card: only agent-assigned
                            // accounts (the pool population). Dealer
                            // Commissions card's own count (below) covers
                            // every paid account with a recorded cost,
                            // including direct sales -- but not the
                            // missing-cost ones, which get their own count.
                            CommissionAccountCount = dealerGroup.Count(r => r.AssignedAgentId.HasValue),
                            DealerCommissionAccountCount = withCost.Count,
                            DealerCommissionMissingCostCount = dealerGroup.Count(r => !r.BuyingPrice.HasValue),
                            CommissionWithheldForArrears = perAgent.Sum(a => a.Withheld),
                            DealerCommissionWithheldForArrears = dealerCommissionWithheldForArrears,
                        };
                    })
                    .ToList();
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "Commission snapshot rollup computation failed");
                return new List<DashboardCommissionRollupRow>();
            }
        }

        private static bool IsPastLockDate(string? lockDate, DateTime now)
        {
            var parsed = ParseNextLockDate(lockDate);
            return parsed.HasValue && parsed.Value < now;
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

        // Same decoupled-upsert pattern as UpsertDealerCommissionOutstandingAsync
        // -- these two columns are new (005_add_commission_account_stats.sql),
        // so keeping them off the main UpsertSnapshotCommissionAsync MERGE
        // means a not-yet-applied migration only skips these two fields.
        public async Task UpsertCommissionAccountStatsAsync(DashboardScope scope, int? commissionAccountCount, decimal? commissionWithheldForArrears)
        {
            const string sql = @"
                MERGE DashboardSnapshot AS target
                USING (SELECT @DealerId AS DealerId, @AgentId AS AgentId) AS source
                ON  (target.DealerId = source.DealerId OR (target.DealerId IS NULL AND source.DealerId IS NULL))
                AND (target.AgentId = source.AgentId OR (target.AgentId IS NULL AND source.AgentId IS NULL))
                WHEN MATCHED THEN
                    UPDATE SET
                        CommissionAccountCount = @CommissionAccountCount,
                        CommissionWithheldForArrears = @CommissionWithheldForArrears,
                        RefreshedAtUtc = SYSUTCDATETIME()
                WHEN NOT MATCHED THEN
                    INSERT (DealerId, AgentId, CommissionAccountCount, CommissionWithheldForArrears, RefreshedAtUtc)
                    VALUES (source.DealerId, source.AgentId, @CommissionAccountCount, @CommissionWithheldForArrears, SYSUTCDATETIME());";

            try
            {
                await _db.ExecuteAsync(sql, new
                {
                    scope.DealerId,
                    scope.AgentId,
                    CommissionAccountCount = commissionAccountCount,
                    CommissionWithheldForArrears = commissionWithheldForArrears,
                });
            }
            catch (SqlException ex) when (IsMissingTable(ex))
            {
                _logger.LogWarning(ex, "DashboardSnapshot.CommissionAccountCount/CommissionWithheldForArrears columns not found; skipping for scope {@Scope}. Apply Database/Dashboard/005_add_commission_account_stats.sql first.", scope);
            }
        }

        // Same decoupled-upsert pattern, for 006_add_dealer_commission_account_count.sql.
        public async Task UpsertDealerCommissionAccountCountAsync(DashboardScope scope, int? dealerCommissionAccountCount)
        {
            const string sql = @"
                MERGE DashboardSnapshot AS target
                USING (SELECT @DealerId AS DealerId, @AgentId AS AgentId) AS source
                ON  (target.DealerId = source.DealerId OR (target.DealerId IS NULL AND source.DealerId IS NULL))
                AND (target.AgentId = source.AgentId OR (target.AgentId IS NULL AND source.AgentId IS NULL))
                WHEN MATCHED THEN
                    UPDATE SET
                        DealerCommissionAccountCount = @DealerCommissionAccountCount,
                        RefreshedAtUtc = SYSUTCDATETIME()
                WHEN NOT MATCHED THEN
                    INSERT (DealerId, AgentId, DealerCommissionAccountCount, RefreshedAtUtc)
                    VALUES (source.DealerId, source.AgentId, @DealerCommissionAccountCount, SYSUTCDATETIME());";

            try
            {
                await _db.ExecuteAsync(sql, new
                {
                    scope.DealerId,
                    scope.AgentId,
                    DealerCommissionAccountCount = dealerCommissionAccountCount,
                });
            }
            catch (SqlException ex) when (IsMissingTable(ex))
            {
                _logger.LogWarning(ex, "DashboardSnapshot.DealerCommissionAccountCount column not found; skipping for scope {@Scope}. Apply Database/Dashboard/006_add_dealer_commission_account_count.sql first.", scope);
            }
        }

        // Same decoupled-upsert pattern, for 007_add_dealer_commission_cost_flags.sql.
        public async Task UpsertDealerCommissionCostFlagsAsync(DashboardScope scope, int? dealerCommissionMissingCostCount, decimal? dealerCommissionWithheldForArrears)
        {
            const string sql = @"
                MERGE DashboardSnapshot AS target
                USING (SELECT @DealerId AS DealerId, @AgentId AS AgentId) AS source
                ON  (target.DealerId = source.DealerId OR (target.DealerId IS NULL AND source.DealerId IS NULL))
                AND (target.AgentId = source.AgentId OR (target.AgentId IS NULL AND source.AgentId IS NULL))
                WHEN MATCHED THEN
                    UPDATE SET
                        DealerCommissionMissingCostCount = @DealerCommissionMissingCostCount,
                        DealerCommissionWithheldForArrears = @DealerCommissionWithheldForArrears,
                        RefreshedAtUtc = SYSUTCDATETIME()
                WHEN NOT MATCHED THEN
                    INSERT (DealerId, AgentId, DealerCommissionMissingCostCount, DealerCommissionWithheldForArrears, RefreshedAtUtc)
                    VALUES (source.DealerId, source.AgentId, @DealerCommissionMissingCostCount, @DealerCommissionWithheldForArrears, SYSUTCDATETIME());";

            try
            {
                await _db.ExecuteAsync(sql, new
                {
                    scope.DealerId,
                    scope.AgentId,
                    DealerCommissionMissingCostCount = dealerCommissionMissingCostCount,
                    DealerCommissionWithheldForArrears = dealerCommissionWithheldForArrears,
                });
            }
            catch (SqlException ex) when (IsMissingTable(ex))
            {
                _logger.LogWarning(ex, "DashboardSnapshot.DealerCommissionMissingCostCount/DealerCommissionWithheldForArrears columns not found; skipping for scope {@Scope}. Apply Database/Dashboard/007_add_dealer_commission_cost_flags.sql first.", scope);
            }
        }

        public async Task<decimal> GetDealerCommissionPaidForPeriodAsync(int dealerId, DateTime periodStart, DateTime periodEndExclusive)
        {
            const string sql = @"
                SELECT ISNULL(SUM(dcp.AmountPaid), 0)
                FROM DealerCommissionPayments dcp
                INNER JOIN Contract_Info ci ON ci.ContractID = dcp.ContractId
                INNER JOIN Devices d ON d.Id = ci.ID
                INNER JOIN Dealers dl ON dl.DealerReference = d.DeviceGroupId
                WHERE dl.DealerId = @DealerId
                  AND dcp.PaidDate >= @PeriodStart AND dcp.PaidDate < @PeriodEnd";

            try
            {
                return await _db.QuerySingleAsync<decimal>(sql, new
                {
                    DealerId = dealerId,
                    PeriodStart = periodStart,
                    PeriodEnd = periodEndExclusive,
                });
            }
            catch (SqlException ex) when (IsMissingTable(ex))
            {
                _logger.LogWarning(ex, "DealerCommissionPayments table not found; returning 0 for dealer {DealerId}.", dealerId);
                return 0;
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "Dealer commission-paid-for-period query failed for dealer {DealerId}", dealerId);
                return 0;
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
