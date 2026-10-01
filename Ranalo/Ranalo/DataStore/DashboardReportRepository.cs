using System.Data;
using Dapper;
using System.Data.SqlClient;
using Ranalo.Models;
using Ranalo.Services;

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

        // Same KosePayments -> Devices -> Dealers linkage and "this calendar
        // month" window as ComputeKpiRollupAsync's RevenueByDealer CTE, but
        // grouped by dl.CompanyName instead of dl.DealerId so the result can
        // be joined directly against DashboardAccountDetailRow.DealerName
        // (which only carries the name, not the id) for the Approver
        // Dashboard's Dealer Performance table.
        public async Task<List<DashboardDealerRevenueRow>> GetRevenueThisMonthByDealerAsync()
        {
            const string sql = @"
                SELECT
                    dl.CompanyName AS DealerName,
                    SUM(CASE WHEN MONTH(kp.PaymentDateValue) = MONTH(GETDATE()) AND YEAR(kp.PaymentDateValue) = YEAR(GETDATE())
                             THEN kp.AmountValue ELSE 0 END) AS RevenueThisMonth
                FROM KosePayments kp
                INNER JOIN Devices d ON d.Id = kp.AccountNoBigint
                INNER JOIN Dealers dl ON dl.DealerReference = d.DeviceGroupId
                GROUP BY dl.CompanyName";

            try
            {
                var rows = await _db.QueryAsync<DashboardDealerRevenueRow>(sql);
                return rows.ToList();
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "Revenue-by-dealer computation failed");
                return new List<DashboardDealerRevenueRow>();
            }
        }

        // Same KosePayments->Devices->Dealers-style linkage as
        // GetRevenueThisMonthByDealerAsync above, but over
        // DealerCommissionPayments (money paid OUT to dealers) grouped by
        // dl.CompanyName for the same name-based join against
        // DashboardAccountDetailRow.DealerName.
        public async Task<List<DashboardDealerCommissionRow>> GetDealerCommissionPaidThisMonthByDealerAsync()
        {
            const string sql = @"
                SELECT
                    dl.CompanyName AS DealerName,
                    SUM(CASE WHEN MONTH(dcp.PaidDate) = MONTH(GETDATE()) AND YEAR(dcp.PaidDate) = YEAR(GETDATE())
                             THEN dcp.AmountPaid ELSE 0 END) AS CommissionPaidThisMonth
                FROM DealerCommissionPayments dcp
                INNER JOIN Contract_Info ci ON ci.ContractID = dcp.ContractId
                INNER JOIN Devices d ON d.Id = ci.ID
                INNER JOIN Dealers dl ON dl.DealerReference = d.DeviceGroupId
                GROUP BY dl.CompanyName";

            try
            {
                var rows = await _db.QueryAsync<DashboardDealerCommissionRow>(sql);
                return rows.ToList();
            }
            catch (SqlException ex) when (IsMissingTable(ex))
            {
                _logger.LogWarning(ex, "DealerCommissionPayments table not found; returning no dealer commission rows.");
                return new List<DashboardDealerCommissionRow>();
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "Dealer commission-paid-by-dealer computation failed");
                return new List<DashboardDealerCommissionRow>();
            }
        }

        // Company-wide revenue by calendar month -- Financials page's
        // monthly comparison chart. Only months with at least one payment
        // get a row; GetFinancialsAsync fills any gaps the same way
        // OperatingExpenseRepository.GetMonthlyTotalsAsync does.
        public async Task<List<DashboardMonthAmountRow>> GetRevenueByMonthAsync(int months)
        {
            var rangeStart = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).AddMonths(-(months - 1));

            const string sql = @"
                SELECT YEAR(kp.PaymentDateValue) AS Year, MONTH(kp.PaymentDateValue) AS Month, SUM(kp.AmountValue) AS Total
                FROM KosePayments kp
                WHERE kp.PaymentDateValue >= @RangeStart
                  AND " + DevicePaymentFilter + @"
                GROUP BY YEAR(kp.PaymentDateValue), MONTH(kp.PaymentDateValue)";

            try
            {
                var rows = await _db.QueryAsync<DashboardMonthAmountRow>(sql, new { RangeStart = rangeStart });
                return rows.ToList();
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "Revenue-by-month computation failed");
                return new List<DashboardMonthAmountRow>();
            }
        }

        public async Task<List<DashboardMonthAmountRow>> GetCommissionsPaidByMonthAsync(int months)
        {
            var rangeStart = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).AddMonths(-(months - 1));

            const string sql = @"
                SELECT YEAR(PaidDate) AS Year, MONTH(PaidDate) AS Month, SUM(AmountPaid) AS Total
                FROM DealerCommissionPayments
                WHERE PaidDate >= @RangeStart
                GROUP BY YEAR(PaidDate), MONTH(PaidDate)";

            try
            {
                var rows = await _db.QueryAsync<DashboardMonthAmountRow>(sql, new { RangeStart = rangeStart });
                return rows.ToList();
            }
            catch (SqlException ex) when (IsMissingTable(ex))
            {
                _logger.LogWarning(ex, "DealerCommissionPayments table not found; returning no commissions-by-month rows.");
                return new List<DashboardMonthAmountRow>();
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "Commissions-paid-by-month computation failed");
                return new List<DashboardMonthAmountRow>();
            }
        }

        public async Task<decimal> GetAllTimeRevenueAsync()
        {
            const string sql = "SELECT ISNULL(SUM(kp.AmountValue), 0) FROM KosePayments kp WHERE " + DevicePaymentFilter;
            try
            {
                return await _db.QuerySingleAsync<decimal>(sql);
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "All-time revenue computation failed");
                return 0;
            }
        }

        public async Task<decimal> GetAllTimeCommissionsPaidAsync()
        {
            const string sql = "SELECT ISNULL(SUM(AmountPaid), 0) FROM DealerCommissionPayments";
            try
            {
                return await _db.QuerySingleAsync<decimal>(sql);
            }
            catch (SqlException ex) when (IsMissingTable(ex))
            {
                return 0;
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "All-time commissions-paid computation failed");
                return 0;
            }
        }

        // Financials revenue counts a payment only when it belongs to a device:
        // its account number is a device; or it's a recovered account's
        // payment, renamed "<device>_R" by ContractRepository.CreateRecoveredAccount
        // when the device went to a new customer (AccountNoBigint is NULL for
        // those, so match on the number before "_R"); or it was an orphaned
        // payment since assigned (OrphanedPayments, by MpesaCode) to an
        // account that is a device. Reversals and stray payments to
        // non-account numbers drop out. Deliberately no Dealers join -- a
        // device with no dealer mapping is still company revenue.
        // The LEFT length is clamped because SQL Server may evaluate it before
        // the LIKE: an AccountNo shorter than 2 chars would make it negative
        // and fail the whole query ("Invalid length parameter"), depending on
        // the plan -- which is why it broke only for some period filters.
        private const string DevicePaymentFilter = @"(
                    EXISTS (SELECT 1 FROM Devices d WHERE d.Id = kp.AccountNoBigint)
                    OR (kp.AccountNo LIKE '%\_R' ESCAPE '\'
                        AND EXISTS (SELECT 1 FROM Devices rd
                                    WHERE rd.Id = TRY_CAST(NULLIF(LEFT(kp.AccountNo, IIF(LEN(kp.AccountNo) > 2, LEN(kp.AccountNo) - 2, 0)), '') AS BIGINT)))
                    OR EXISTS (SELECT 1 FROM OrphanedPayments op
                               INNER JOIN Devices ad ON ad.Id = op.AccountNoBigint
                               WHERE op.MpesaCode = kp.MpesaCode))";

        public async Task<decimal> GetRevenueForPeriodAsync(DateTime? fromDate, DateTime? toDateExclusive)
        {
            const string sql = @"
                SELECT ISNULL(SUM(kp.AmountValue), 0)
                FROM KosePayments kp
                WHERE " + DevicePaymentFilter + @"
                  AND (@FromDate IS NULL OR kp.PaymentDateValue >= @FromDate)
                  AND (@ToDate IS NULL OR kp.PaymentDateValue < @ToDate)";

            try
            {
                return await _db.QuerySingleAsync<decimal>(sql, new { FromDate = fromDate, ToDate = toDateExclusive });
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "Revenue-for-period computation failed");
                return 0;
            }
        }

        public async Task<decimal> GetCommissionsPaidForPeriodAsync(DateTime? fromDate, DateTime? toDateExclusive)
        {
            const string sql = @"
                SELECT ISNULL(SUM(dcp.AmountPaid), 0)
                FROM DealerCommissionPayments dcp
                WHERE (@FromDate IS NULL OR dcp.PaidDate >= @FromDate)
                  AND (@ToDate IS NULL OR dcp.PaidDate < @ToDate)";

            try
            {
                return await _db.QuerySingleAsync<decimal>(sql, new { FromDate = fromDate, ToDate = toDateExclusive });
            }
            catch (SqlException ex) when (IsMissingTable(ex))
            {
                _logger.LogWarning(ex, "DealerCommissionPayments table not found; returning 0 commissions paid for period.");
                return 0;
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "Commissions-paid-for-period computation failed");
                return 0;
            }
        }

        public async Task<(decimal DealerOutstanding, decimal AgentOutstanding)> GetTotalCommissionsOutstandingAsync()
        {
            const string sql = @"
                SELECT ISNULL(SUM(DealerCommissionOutstanding), 0), ISNULL(SUM(CommissionOutstanding), 0)
                FROM DashboardSnapshot
                WHERE DealerId IS NOT NULL";

            try
            {
                return await _db.QuerySingleAsync<(decimal, decimal)>(sql);
            }
            catch (SqlException ex) when (IsMissingTable(ex))
            {
                _logger.LogWarning(ex, "DashboardSnapshot table not found; returning 0 commissions outstanding.");
                return (0, 0);
            }
        }

        public async Task<DashboardRevenuePeriodRow> GetDealerRevenueForPeriodAsync(
            int? dealerId,
            DateTime periodStart,
            DateTime periodEndExclusive,
            DateTime priorPeriodStart,
            DateTime priorPeriodEndExclusive,
            int? agentUserId = null)
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
                     WHERE (@DealerId IS NULL OR dl2.DealerId = @DealerId) AND ci.StartDate IS NOT NULL
                       AND (@AgentUserId IS NULL OR ci.AssignedAgentId = @AgentUserId)) AS TotalAccounts,
                    (SELECT COUNT(*)
                     FROM Contract_Info ci4
                     INNER JOIN Devices d4 ON d4.Id = ci4.ID
                     INNER JOIN Dealers dl4 ON dl4.DealerReference = d4.DeviceGroupId
                     WHERE (@DealerId IS NULL OR dl4.DealerId = @DealerId) AND ci4.StartDate IS NOT NULL
                       AND ci4.StartDate < @PeriodEnd
                       AND (@AgentUserId IS NULL OR ci4.AssignedAgentId = @AgentUserId)) AS TotalAccountsAsOfPeriod,
                    (SELECT COUNT(*)
                     FROM Contract_Info ci4
                     INNER JOIN Devices d4 ON d4.Id = ci4.ID
                     INNER JOIN Dealers dl4 ON dl4.DealerReference = d4.DeviceGroupId
                     WHERE (@DealerId IS NULL OR dl4.DealerId = @DealerId) AND ci4.StartDate >= @PeriodStart
                       AND ci4.StartDate < @PeriodEnd
                       AND (@AgentUserId IS NULL OR ci4.AssignedAgentId = @AgentUserId)) AS NewAccountsInPeriod,
                    (SELECT COUNT(*)
                     FROM Contract_Info ci4
                     INNER JOIN Devices d4 ON d4.Id = ci4.ID
                     INNER JOIN Dealers dl4 ON dl4.DealerReference = d4.DeviceGroupId
                     WHERE (@DealerId IS NULL OR dl4.DealerId = @DealerId) AND ci4.StartDate >= @PriorPeriodStart
                       AND ci4.StartDate < @PriorPeriodEnd
                       AND (@AgentUserId IS NULL OR ci4.AssignedAgentId = @AgentUserId)) AS NewAccountsPriorPeriod,
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
                         WHERE (@DealerId IS NULL OR dl3.DealerId = @DealerId) AND ci3.StartDate IS NOT NULL
                           AND (@AgentUserId IS NULL OR ci3.AssignedAgentId = @AgentUserId)
                     ) ca) AS TargetRevenue
                FROM KosePayments kp
                INNER JOIN Devices d ON d.Id = kp.AccountNoBigint
                INNER JOIN Dealers dl ON dl.DealerReference = d.DeviceGroupId
                LEFT JOIN Contract_Info ci5 ON ci5.ID = d.Id
                WHERE (@DealerId IS NULL OR dl.DealerId = @DealerId)
                AND (@AgentUserId IS NULL OR ci5.AssignedAgentId = @AgentUserId)";

            try
            {
                var row = await _db.QueryFirstOrDefaultAsync<DashboardRevenuePeriodRow>(sql, new
                {
                    DealerId = dealerId,
                    PeriodStart = periodStart,
                    PeriodEnd = periodEndExclusive,
                    PriorPeriodStart = priorPeriodStart,
                    PriorPeriodEnd = priorPeriodEndExclusive,
                    AgentUserId = agentUserId,
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

        internal static DateTime? ParseNextLockDate(string? raw)
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

        // Single definition of the Good/Slow/Arrears split used everywhere an
        // account is classified off Devices.NextLockDateIsoFormat -- the
        // dealer-wide and per-device aggregate classifications below, the
        // per-account watchlist/agent-performance/contracts sections in
        // GetDealerAccountDetailsAsync, and the Dealer Dashboard's own
        // Paying-vs-Non-Paying card (Index.cshtml treats ArrearsCount as
        // "Non-Paying") all resolve through this one method so none of them
        // can drift out of sync with each other.
        internal enum LockBucket { Good, Slow, Arrears }

        internal static LockBucket ClassifyLock(double daysPastLock) =>
            daysPastLock > 7 ? LockBucket.Arrears : daysPastLock > 0 ? LockBucket.Slow : LockBucket.Good;

        internal static double DaysPastLock(string? nextLockDateRaw, DateTime? now = null)
        {
            var nextLockDate = ParseNextLockDate(nextLockDateRaw);
            return nextLockDate.HasValue ? ((now ?? DateTime.Now) - nextLockDate.Value).TotalDays : 0;
        }

        public async Task<DashboardLockClassificationRow> GetDealerLockClassificationAsync(int? dealerId, int? agentUserId = null)
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
                WHERE (@DealerId IS NULL OR dl.DealerId = @DealerId) AND ci.StartDate IS NOT NULL
                AND (@AgentUserId IS NULL OR ci.AssignedAgentId = @AgentUserId)";

            try
            {
                var rawDates = await _db.QueryAsync<string?>(sql, new { DealerId = dealerId, AgentUserId = agentUserId });
                var now = DateTime.Now;
                var result = new DashboardLockClassificationRow();

                foreach (var raw in rawDates)
                {
                    var daysPastLock = DaysPastLock(raw, now);

                    switch (ClassifyLock(daysPastLock))
                    {
                        case LockBucket.Arrears:
                            result.ArrearsCount++;
                            if (daysPastLock > 90)
                            {
                                result.NonPayingCount++;
                            }
                            break;
                        case LockBucket.Slow:
                            result.SlowCount++;
                            break;
                        default:
                            result.GoodCount++;
                            break;
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

                    var daysPastLock = DaysPastLock(row.LockDate, now);

                    if (ClassifyLock(daysPastLock) == LockBucket.Arrears)
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

        public async Task<DashboardArrearsClassificationRow> GetDealerArrearsClassificationAsync(int? dealerId, int? agentUserId = null)
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
                WHERE (@DealerId IS NULL OR dl.DealerId = @DealerId) AND ci.StartDate IS NOT NULL
                AND (@AgentUserId IS NULL OR ci.AssignedAgentId = @AgentUserId)
                OPTION (RECOMPILE)";

            try
            {
                // RECOMPILE + longer timeout: one query serves an agent, a dealer
                // and the whole company; a plan cached for one timed out on the
                // company-wide call (Admin Total Arrears / Bad Debt showed 0).
                var rows = await _db.QueryAsync<AccountArrearsRow>(sql, new { DealerId = dealerId, AgentUserId = agentUserId }, commandTimeout: 90);
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
                }

                await ApplyWriteOffRegisterAsync(result, dealerId, agentUserId);

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

        // Write-off card figures come from the WriteOffs register (approved,
        // not reinstated) -- the same definition as Financials and the
        // Write-offs page -- replacing the old "full term elapsed with a
        // shortfall" rule. "Recovered this month" is the payments received
        // this calendar month on write-offs that were reinstated this month.
        // Left at 0 if Database/WriteOffs/001 hasn't been run.
        private async Task ApplyWriteOffRegisterAsync(DashboardArrearsClassificationRow result, int? dealerId, int? agentUserId)
        {
            const string sql = @"
                ;WITH scoped AS (
                    SELECT wo.*
                    FROM WriteOffs wo
                    INNER JOIN Devices d ON d.Id = wo.AccountNo
                    INNER JOIN Dealers dl ON dl.DealerReference = d.DeviceGroupId
                    OUTER APPLY (SELECT TOP 1 c.AssignedAgentId FROM Contract_Info c
                                 WHERE c.ContractID = wo.ContractId AND c.ID = wo.AccountNo) ci
                    WHERE wo.Status = 'Approved'
                      AND (@DealerId IS NULL OR dl.DealerId = @DealerId)
                      AND (@AgentUserId IS NULL OR ci.AssignedAgentId = @AgentUserId)
                )
                SELECT
                    (SELECT COUNT(*) FROM scoped WHERE ReinstatedDate IS NULL) AS WriteOffCount,
                    (SELECT ISNULL(SUM(OutstandingBalance), 0) FROM scoped WHERE ReinstatedDate IS NULL) AS WriteOffTotal,
                    (SELECT ISNULL(SUM(kp.AmountValue), 0)
                     FROM scoped s
                     INNER JOIN KosePayments kp ON kp.AccountNoBigint = s.AccountNo
                     WHERE s.ReinstatedDate >= DATEFROMPARTS(YEAR(GETDATE()), MONTH(GETDATE()), 1)
                       AND kp.PaymentDateValue >= DATEFROMPARTS(YEAR(GETDATE()), MONTH(GETDATE()), 1)) AS WriteOffRecoveredThisMonth";

            try
            {
                var row = await _db.QuerySingleAsync<(int WriteOffCount, decimal WriteOffTotal, decimal WriteOffRecoveredThisMonth)>(
                    sql, new { DealerId = dealerId, AgentUserId = agentUserId });
                result.WriteOffCount = row.WriteOffCount;
                result.WriteOffTotal = row.WriteOffTotal;
                result.WriteOffRecoveredThisMonth = row.WriteOffRecoveredThisMonth;
            }
            catch (SqlException ex) when (IsMissingTable(ex))
            {
                _logger.LogWarning(ex, "WriteOffs table not found; write-off card left at 0. Apply Database/WriteOffs/001_create_write_off_tables.sql.");
            }
        }

        private class AccountDetailQueryRow
        {
            public long AccountId { get; set; }
            public string CustomerName { get; set; } = "";
            public int? AssignedAgentId { get; set; }
            public string? AgentName { get; set; }
            public string DeviceName { get; set; } = "";
            public string? NextLockDateRaw { get; set; }
            public decimal Daily { get; set; }
            public decimal Weekly { get; set; }
            public decimal Monthly { get; set; }
            public decimal TotalPaid { get; set; }
            public decimal FullContractValue { get; set; }
            public int DaysAccrued { get; set; }
            public decimal Deposit { get; set; }
            public DateTime StartDate { get; set; }
            public string? DealerName { get; set; }
            public string? CustomerPhone { get; set; }
            public decimal? BuyingPrice { get; set; }
            public DateTime? LastPaymentDate { get; set; }
            public bool IsManuallyRestructured { get; set; }
            public string? NextOfKinName { get; set; }
            public string? NextOfKinPhone { get; set; }
            public string? NextOfKin2Name { get; set; }
            public string? NextOfKin2Phone { get; set; }
        }

        public async Task<List<DashboardAccountDetailRow>> GetDealerAccountDetailsAsync(int? dealerId, int? agentUserId = null)
        {
            // Single live per-account source for Non-Payers/Slow-Payers/
            // Good-Payers, Agent Performance, My Contracts, and Contracts
            // Ending Soon (see IDashboardReportRepository's doc comment) --
            // same join/CTE pattern and formulas as
            // GetDealerArrearsClassificationAsync (ArrearsAmount) and
            // RefreshCompletedContractsAsync (FullContractValue), plus the
            // agent join AccountWatchlistRepository.GetActiveWatchlistAsync
            // already uses. Classification into Good/Slow/Arrears happens in
            // C# via ClassifyLock, same as everywhere else in this file.
            const string sql = @"
                ;WITH ValidPayments AS (
                    SELECT COALESCE(op.AccountNoBigint, kp.AccountNoBigint) AS AccountNo, kp.AmountValue, kp.PaymentDateValue
                    FROM KosePayments kp
                    LEFT JOIN OrphanedPayments op ON op.MpesaCode = kp.MpesaCode
                ),
                PaymentTotals AS (
                    SELECT AccountNo, SUM(AmountValue) AS TotalPaid, MAX(PaymentDateValue) AS LastPaymentDate
                    FROM ValidPayments
                    GROUP BY AccountNo
                )
                SELECT
                    ci.ID AS AccountId,
                    ci.First_Name AS CustomerName,
                    ci.AssignedAgentId,
                    agentUser.[Name] + ' ' + agentUser.[LastName] AS AgentName,
                    ISNULL(NULLIF(LTRIM(RTRIM(ISNULL(d.Make, '') + ' ' + ISNULL(d.Model, ''))), ''), 'Unknown Device') AS DeviceName,
                    d.NextLockDateIsoFormat AS NextLockDateRaw,
                    ci.Daily,
                    ci.Weekly,
                    ci.Monthly,
                    ci.Deposit,
                    ISNULL(pt.TotalPaid, 0) AS TotalPaid,
                    (ci.Deposit + ci.Daily * 30 * ci.Term_in_Months + ci.Weekly * (30.0 / 7.0) * ci.Term_in_Months + ci.Monthly * ci.Term_in_Months) AS FullContractValue,
                    DaysAccrued.Days AS DaysAccrued,
                    ci.StartDate,
                    dl.CompanyName AS DealerName,
                    ph.Phone AS CustomerPhone,
                    nk1.[Name] AS NextOfKinName,
                    nk1.Phone AS NextOfKinPhone,
                    nk2.[Name] AS NextOfKin2Name,
                    nk2.Phone AS NextOfKin2Phone,
                    ci.BuyingPrice,
                    pt.LastPaymentDate,
                    -- Manual restructures are the only ones stored; auto
                    -- restructures are derived in C# (see DashboardReportService.IsRestructured).
                    CASE WHEN EXISTS (SELECT 1 FROM RestructuredRecords rr WHERE rr.AccountNo = ci.ID)
                         THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END AS IsManuallyRestructured
                FROM Contract_Info ci
                INNER JOIN Devices d ON d.Id = ci.ID
                INNER JOIN Dealers dl ON dl.DealerReference = d.DeviceGroupId
                LEFT JOIN Users agentUser ON agentUser.UserId = ci.AssignedAgentId
                LEFT JOIN PaymentTotals pt ON pt.AccountNo = ci.ID
                CROSS APPLY (
                    SELECT CASE
                        WHEN DATEDIFF(DAY, ci.StartDate, GETDATE()) < CAST(ci.Term_in_Months * 30 AS INT)
                            THEN DATEDIFF(DAY, ci.StartDate, GETDATE())
                        ELSE CAST(ci.Term_in_Months * 30 AS INT)
                    END AS Days
                ) DaysAccrued
                OUTER APPLY (
                    -- Devices.CustomerPhoneNumber is never populated in this
                    -- dataset -- Woo_Orders.Phone (via KosePayments.MpesaCode)
                    -- is the only reliably-populated phone source, so pull
                    -- whichever of this account's orders is most recent.
                    SELECT TOP 1 wo.Phone, wo.OrderID
                    FROM KosePayments kpPhone
                    INNER JOIN Woo_Orders wo ON wo.MpesaDepositRef = kpPhone.MpesaCode
                    WHERE kpPhone.AccountNoBigint = ci.ID
                    ORDER BY wo.DateCreated DESC
                ) ph
                -- Next of kin captured on that same order (primary and second),
                -- same table and IsPrimary flag as the customer details pages.
                OUTER APPLY (
                    SELECT TOP 1 nk.[Name], nk.Phone
                    FROM Woo_Orders_NextOfKin nk
                    WHERE nk.OrderId = ph.OrderID AND nk.IsPrimary = 1
                ) nk1
                OUTER APPLY (
                    SELECT TOP 1 nk.[Name], nk.Phone
                    FROM Woo_Orders_NextOfKin nk
                    WHERE nk.OrderId = ph.OrderID AND nk.IsPrimary = 0
                ) nk2
                WHERE (@DealerId IS NULL OR dl.DealerId = @DealerId) AND ci.StartDate IS NOT NULL
                AND (@AgentUserId IS NULL OR ci.AssignedAgentId = @AgentUserId)";

            try
            {
                var rows = await _db.QueryAsync<AccountDetailQueryRow>(sql, new { DealerId = dealerId, AgentUserId = agentUserId });

                return rows.Select(row => new DashboardAccountDetailRow
                {
                    AccountId = row.AccountId,
                    CustomerName = row.CustomerName,
                    AssignedAgentId = row.AssignedAgentId,
                    AgentName = row.AgentName,
                    DeviceName = row.DeviceName,
                    NextLockDateRaw = row.NextLockDateRaw,
                    ArrearsAmount = row.TotalPaid - (row.Deposit + row.Daily * row.DaysAccrued + row.Weekly * (row.DaysAccrued / 7.0m) + row.Monthly * (row.DaysAccrued / 30.0m)),
                    DailyBlendedRate = row.Daily + (row.Weekly / 7.0m) + (row.Monthly / 30.0m),
                    MonthlyPayment = row.Daily * 30 + row.Weekly * (30.0m / 7.0m) + row.Monthly,
                    TotalPaid = row.TotalPaid,
                    FullContractValue = row.FullContractValue,
                    StartDate = row.StartDate,
                    DealerName = row.DealerName,
                    CustomerPhone = row.CustomerPhone,
                    BuyingPrice = row.BuyingPrice,
                    LastPaymentDate = row.LastPaymentDate,
                    IsManuallyRestructured = row.IsManuallyRestructured,
                    NextOfKinName = row.NextOfKinName,
                    NextOfKinPhone = row.NextOfKinPhone,
                    NextOfKin2Name = row.NextOfKin2Name,
                    NextOfKin2Phone = row.NextOfKin2Phone,
                }).ToList();
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "Dealer account-details query failed for dealer {DealerId}", dealerId);
                return new List<DashboardAccountDetailRow>();
            }
        }

        public async Task<decimal> GetDealerAgentCommissionPaidForPeriodAsync(int? dealerId, DateTime periodStart, DateTime periodEndExclusive, int? agentUserId = null)
        {
            const string sql = @"
                SELECT ISNULL(SUM(acp.AmountPaid), 0)
                FROM AgentCommissionPayments acp
                INNER JOIN Contract_Info ci ON ci.ContractID = acp.ContractId
                INNER JOIN Devices d ON d.Id = ci.ID
                INNER JOIN Dealers dl ON dl.DealerReference = d.DeviceGroupId
                WHERE (@DealerId IS NULL OR dl.DealerId = @DealerId)
                  AND acp.PaymentDate >= @PeriodStart AND acp.PaymentDate < @PeriodEnd
                  AND (@AgentUserId IS NULL OR ci.AssignedAgentId = @AgentUserId)";

            try
            {
                return await _db.QuerySingleAsync<decimal>(sql, new
                {
                    DealerId = dealerId,
                    PeriodStart = periodStart,
                    PeriodEnd = periodEndExclusive,
                    AgentUserId = agentUserId,
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
            public long AccountId { get; set; }
            public string? CustomerName { get; set; }
            public string? AgentName { get; set; }
            public string? DealerName { get; set; }
            public decimal Deposit { get; set; }

            // Null for direct dealer sales with no agent -- these still
            // contribute to DealerCommissionEarned (see the query comment
            // below), just never enter the per-agent pooling.
            public int? AssignedAgentId { get; set; }
            public string? ContractId { get; set; }
            public DateTime StartDate { get; set; }
            public decimal? TotalCost { get; set; }
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

            // Of AgentPaid, lines recorded against the bonus (the rest is upfront).
            public decimal AgentBonusPaid { get; set; }
            public decimal DealerPaid { get; set; }

            // Days since Contract_Info.StartDate; drives the 90-day bonus.
            public int DaysSinceStart { get; set; }

            // Devices.Make + Model and ImeiNo, for display only.
            public string? ProductName { get; set; }
            public string? Imei { get; set; }

            // A Woo_Orders row exists for this contract; the agent bonus needs one.
            public bool HasWooOrder { get; set; }

            // The contract's latest collections case, if it has ever had one.
            public CollectionTerms? Collections { get; set; }
        }

        // The raw per-account commission inputs used by every commission
        // figure in the app. Commission itself is calculated from these rows
        // by Services.CommissionCalculator (see Breakdown below).
        private async Task<List<AgentCommissionAccountRow>> FetchAgentCommissionAccountRowsAsync(int? dealerId = null, int? agentUserId = null)
        {
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
                        ci.ID AS AccountId,
                        ci.AssignedAgentId,
                        ci.ContractID,
                        ci.First_Name AS CustomerName,
                        au.[Name] + ' ' + au.[LastName] AS AgentName,
                        dl.CompanyName AS DealerName,
                        ci.Deposit,
                        ISNULL(pt.TotalPaid, 0) AS TotalPaid,
                        -- NOT wrapped in ISNULL(..., 0) -- a missing
                        -- BuyingPrice needs to be visibly excluded from the
                        -- dealer commission calc, not silently treated as a
                        -- free device (see AgentCommissionAccountRow.BuyingPrice).
                        ci.BuyingPrice AS BuyingPrice,
                        NULLIF(LTRIM(RTRIM(ISNULL(d.Make, '') + ' ' + ISNULL(d.Model, ''))), '') AS ProductName,
                        CAST(d.ImeiNo AS NVARCHAR(50)) AS Imei,
                        CAST(CASE WHEN EXISTS (SELECT 1 FROM Woo_Orders wo WHERE wo.ContractId = ci.ContractID)
                                  THEN 1 ELSE 0 END AS BIT) AS HasWooOrder,
                        -- Commission itself is calculated in C# by
                        -- Services.CommissionCalculator, the one formula for the app.
                        ci.StartDate,
                        ci.Total_Cost AS TotalCost,
                        CAST(ci.ContractID AS NVARCHAR(50)) AS ContractIdText,
                        ISNULL(pt.TotalPaid, 0)
                            - (
                                ci.Deposit
                                + ci.Daily * DaysAccrued.Days
                                + ci.Weekly * (DaysAccrued.Days / 7.0)
                                + ci.Monthly * (DaysAccrued.Days / 30.0)
                              ) AS Arrears,
                        d.NextLockDateIsoFormat AS LockDate,
                        DATEDIFF(DAY, ci.StartDate, GETDATE()) AS DaysSinceStart
                    FROM Contract_Info ci
                    INNER JOIN Devices d ON d.Id = ci.ID
                    INNER JOIN Dealers dl ON dl.DealerReference = d.DeviceGroupId
                    LEFT JOIN Users au ON au.UserId = ci.AssignedAgentId
                    LEFT JOIN PaymentTotals pt ON pt.AccountNo = ci.ID
                    CROSS APPLY (
                        SELECT CASE
                            WHEN DATEDIFF(DAY, ci.StartDate, GETDATE()) < CAST(ci.Term_in_Months * 30 AS INT)
                                THEN DATEDIFF(DAY, ci.StartDate, GETDATE())
                            ELSE CAST(ci.Term_in_Months * 30 AS INT)
                        END AS Days
                    ) DaysAccrued
                    WHERE ci.StartDate IS NOT NULL
                    AND (@DealerId IS NULL OR dl.DealerId = @DealerId)
                    AND (@AgentUserId IS NULL OR ci.AssignedAgentId = @AgentUserId)
                ),
                AgentPaymentsAgg AS (
                    SELECT ContractId, SUM(ISNULL(AmountPaid, 0)) AS TotalAgentPaid,
                        SUM(CASE WHEN CommissionPart = 'Bonus' THEN ISNULL(AmountPaid, 0) ELSE 0 END) AS AgentBonusPaid
                    FROM AgentCommissionPayments
                    GROUP BY ContractId
                ),
                -- What Ranalo has actually paid the dealer, joined via
                -- ContractId (like AgentPaymentsAgg) rather than
                -- DealerCommissionPayments.DealerId -- existing usage
                -- elsewhere in this codebase (the former CommissionsRepository)
                -- deliberately does the same, since that column's semantics
                -- aren't confirmed (only ContractId/AmountPaid are).
                DealerPaymentsAgg AS (
                    SELECT ContractId, SUM(ISNULL(AmountPaid, 0)) AS TotalDealerPaid
                    FROM DealerCommissionPayments
                    GROUP BY ContractId
                )
                SELECT
                    ac.DealerId,
                    ac.AccountId,
                    ac.CustomerName,
                    ac.AgentName,
                    ac.DealerName,
                    ac.Deposit,
                    ac.AssignedAgentId,
                    ac.ContractIdText AS ContractId,
                    ac.StartDate,
                    ac.TotalCost,
                    ac.TotalPaid,
                    ac.BuyingPrice,
                    ac.ProductName,
                    ac.Imei,
                    ac.HasWooOrder,
                    ac.Arrears,
                    ac.LockDate,
                    ac.DaysSinceStart,
                    ISNULL(ap.TotalAgentPaid, 0) AS AgentPaid,
                    ISNULL(ap.AgentBonusPaid, 0) AS AgentBonusPaid,
                    ISNULL(dp.TotalDealerPaid, 0) AS DealerPaid
                FROM AccountCommission ac
                LEFT JOIN AgentPaymentsAgg ap ON ap.ContractId = ac.ContractID
                LEFT JOIN DealerPaymentsAgg dp ON dp.ContractId = ac.ContractID
                OPTION (RECOMPILE)";

            // RECOMPILE: the same query serves one agent, one dealer and the
            // whole company ("@X IS NULL OR ..."), so a cached plan built for
            // one of those can time out on another (seen on the company-wide
            // call). Takes ~4s company-wide; a compile is negligible next to that.
            var rows = (await _db.QueryAsync<AgentCommissionAccountRow>(sql, new { DealerId = dealerId, AgentUserId = agentUserId }, commandTimeout: 90)).ToList();

            var terms = await GetCollectionTermsAsync();
            if (terms.Count > 0)
            {
                foreach (var r in rows)
                {
                    if (long.TryParse(r.ContractId, out var cid) && terms.TryGetValue(cid, out var t))
                    {
                        r.Collections = t;
                    }
                }
            }
            return rows;
        }

        private class CollectionTermsRow : CollectionTerms
        {
            public long ContractId { get; set; }
        }

        // Each contract's latest collections case (Database/Collections/001),
        // keyed by ContractID. Empty until that script has been run.
        public async Task<Dictionary<long, CollectionTerms>> GetCollectionTermsAsync()
        {
            const string casesSql = @"
                SELECT c.Id AS CaseId, c.ContractId,
                       CAST(CASE WHEN c.Status = 'Returned' THEN 1 ELSE 0 END AS BIT) AS IsReturned,
                       c.FrozenDeduction, c.CommissionPaidBasis,
                       ISNULL(c.ShortfallAtReturn, 0) AS ShortfallAtReturn,
                       ISNULL(c.TotalPaidAtReturn, 0) AS TotalPaidAtReturn,
                       ISNULL(c.CollectorEarnedOnCase, 0) AS CollectorEarned
                FROM CollectionCases c
                WHERE c.Id = (SELECT MAX(x.Id) FROM CollectionCases x WHERE x.ContractId = c.ContractId)";

            const string repoCostSql = @"
                SELECT CAST(OldContractId AS BIGINT) AS ContractId, SUM(ISNULL(RepossessionCost, 0)) AS Cost
                FROM DeviceRecoveries
                WHERE OldContractId IN @Ids
                GROUP BY OldContractId";

            List<CollectionTermsRow> cases;
            try
            {
                cases = (await _db.QueryAsync<CollectionTermsRow>(casesSql)).ToList();
            }
            catch (SqlException ex) when (IsMissingTable(ex))
            {
                return new Dictionary<long, CollectionTerms>();
            }

            // Repossession cost is a collection cost charged back once the
            // contract is returned to its agent and dealer.
            var returned = cases.Where(c => c.IsReturned).Select(c => c.ContractId).ToList();
            if (returned.Count > 0)
            {
                try
                {
                    var costs = new Dictionary<long, decimal>();
                    foreach (var chunk in returned.Chunk(1000))
                    {
                        foreach (var row in await _db.QueryAsync<(long ContractId, decimal Cost)>(repoCostSql, new { Ids = chunk }))
                        {
                            costs[row.ContractId] = row.Cost;
                        }
                    }
                    foreach (var c in cases)
                    {
                        c.RepossessionCost = costs.GetValueOrDefault(c.ContractId);
                    }
                }
                catch (SqlException ex) when (IsMissingTable(ex))
                {
                    // DeviceRecoveries not created yet: no repossession costs.
                }
            }

            return cases.ToDictionary(c => c.ContractId, c => (CollectionTerms)c);
        }

        // Runs one account row through the app-wide commission formula.
        private static CommissionBreakdown Breakdown(AgentCommissionAccountRow r, DateTime now) =>
            CommissionCalculator.Calculate(new CommissionInputs
            {
                AgentBonusPaid = r.AgentBonusPaid,
                Collections = r.Collections,
                Deposit = r.Deposit,
                DaysSinceStart = r.DaysSinceStart,
                IsPastLockDate = IsPastLockDate(r.LockDate, now),
                Arrears = r.Arrears,
                TotalPaid = r.TotalPaid,
                BuyingPrice = r.BuyingPrice,
                HasAgent = r.AssignedAgentId.HasValue,
                AgentPaid = r.AgentPaid,
                DealerPaid = r.DealerPaid,
                HasWooOrder = r.HasWooOrder,
            });

        // A single agent's own Commission card (Agent Dashboard), live.
        public async Task<(decimal CommissionOutstanding, int CommissionAccountCount, decimal CommissionWithheldForArrears)> GetAgentCommissionSummaryAsync(int dealerId, int agentUserId)
        {
            var rows = await FetchAgentCommissionAccountRowsAsync(dealerId, agentUserId);
            var now = DateTime.Now;
            var pool = CommissionCalculator.PoolAgent(rows.Select(r => Breakdown(r, now)));
            return (pool.Owed, rows.Count, pool.Withheld);
        }

        // Dashboard Commissions section: one row per agent-assigned account.
        public async Task<List<DashboardAccountCommissionRow>> GetAccountCommissionsAsync(int? dealerId, int? agentUserId = null)
        {
            try
            {
                var rows = await FetchAgentCommissionAccountRowsAsync(dealerId, agentUserId);
                var now = DateTime.Now;

                return rows
                    .Where(r => r.AssignedAgentId.HasValue)
                    .Select(r =>
                    {
                        var b = Breakdown(r, now);
                        return new DashboardAccountCommissionRow
                        {
                            AccountId = r.AccountId,
                            AgentId = r.AssignedAgentId!.Value,
                            Earned = b.AgentEarned,
                            ArrearsDeducted = b.AgentArrearsDeducted,
                            Paid = b.AgentPaid,
                        };
                    })
                    .ToList();
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "Account commission query failed for dealer {DealerId}", dealerId);
                return new List<DashboardAccountCommissionRow>();
            }
        }

        // Every account with its full commission breakdown. Backs the
        // Account Commissions page and the admin Commissions pages.
        public async Task<List<CommissionAccount>> GetCommissionAccountsAsync(int? dealerId, int? agentUserId = null)
        {
            try
            {
                var rows = await FetchAgentCommissionAccountRowsAsync(dealerId, agentUserId);
                var now = DateTime.Now;
                return rows.Select(r => new CommissionAccount
                {
                    AccountId = r.AccountId,
                    ContractId = r.ContractId,
                    CustomerName = r.CustomerName ?? "",
                    AgentId = r.AssignedAgentId,
                    AgentName = r.AgentName,
                    DealerId = r.DealerId,
                    DealerName = r.DealerName ?? "",
                    StartDate = r.StartDate,
                    DaysSinceStart = r.DaysSinceStart,
                    Deposit = r.Deposit,
                    TotalCost = r.TotalCost,
                    TotalPaid = r.TotalPaid,
                    BuyingPrice = r.BuyingPrice,
                    DaysPastLock = DaysPastLock(r.LockDate, now),
                    ProductName = r.ProductName,
                    Imei = r.Imei,
                    AgentBonusPaid = r.AgentBonusPaid,
                    Commission = Breakdown(r, now),
                }).ToList();
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "Commission accounts query failed for dealer {DealerId}", dealerId);
                return new List<CommissionAccount>();
            }
        }

        // Performance Bonus Tracker card (Agent Dashboard): lifetime
        // commission paid to this agent, plus their accounts' standing against
        // the 90-day bonus. Earned / at risk use CommissionCalculator's rule;
        // "upcoming" is 60-89 days in, within 30 days of the milestone.
        public async Task<(decimal CommissionPaidLifetime, int BonusEarnedAccountCount, int BonusAtRiskAccountCount, int BonusUpcomingAccountCount)> GetAgentBonusTrackerAsync(int dealerId, int agentUserId)
        {
            var rows = await FetchAgentCommissionAccountRowsAsync(dealerId, agentUserId);
            var now = DateTime.Now;
            var breakdowns = rows.Select(r => Breakdown(r, now)).ToList();

            var paidLifetime = rows.Sum(r => r.AgentPaid);
            var earned = breakdowns.Count(b => b.BonusEarned);
            var atRisk = breakdowns.Count(b => b.BonusAtRisk);
            var upcoming = rows.Count(r => r.DaysSinceStart >= CommissionCalculator.BonusDays - 30 && r.DaysSinceStart < CommissionCalculator.BonusDays);

            return (paidLifetime, earned, atRisk, upcoming);
        }

        public async Task<List<DashboardCommissionRollupRow>> ComputeCommissionSnapshotRollupAsync()
        {
            // Nightly Agent Commissions / Dealer Commissions card figures for
            // every dealer. Each account goes through CommissionCalculator (the
            // one formula for the app), then agent figures are pooled per agent
            // and dealer figures per dealer. Accounts with no buying price add no
            // dealer commission until it is entered and are counted separately.
            try
            {
                var accountRows = await FetchAgentCommissionAccountRowsAsync();
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
                        var breakdowns = dealerGroup.Select(r => (Row: r, Commission: Breakdown(r, now))).ToList();

                        // Agent side: pooled per agent (direct sales have no agent).
                        var perAgent = breakdowns
                            .Where(x => x.Row.AssignedAgentId.HasValue)
                            .GroupBy(x => x.Row.AssignedAgentId!.Value)
                            .Select(g => CommissionCalculator.PoolAgent(g.Select(x => x.Commission)))
                            .ToList();

                        // Dealer side: pooled across the dealer. Accounts with no
                        // buying price contribute no commission until it is entered.
                        var dealerPool = CommissionCalculator.PoolDealer(breakdowns.Select(x => x.Commission));

                        return new DashboardCommissionRollupRow
                        {
                            DealerId = dealerGroup.Key,
                            CommissionReceived = dealerPool.Earned,
                            CommissionPaidToAgents = perAgent.Sum(a => a.Paid),
                            // Owed is floored per agent, so one agent's arrears
                            // never offset what another agent is owed.
                            CommissionOutstanding = perAgent.Sum(a => a.Owed),
                            DealerCommissionOutstanding = dealerPool.Owed,
                            CommissionAccountCount = dealerGroup.Count(r => r.AssignedAgentId.HasValue),
                            DealerCommissionAccountCount = dealerGroup.Count(r => r.BuyingPrice.HasValue),
                            DealerCommissionMissingCostCount = dealerGroup.Count(r => !r.BuyingPrice.HasValue),
                            CommissionWithheldForArrears = perAgent.Sum(a => a.Withheld),
                            DealerCommissionWithheldForArrears = dealerPool.Withheld,
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

        public async Task<decimal> GetDealerCommissionPaidForPeriodAsync(int? dealerId, DateTime periodStart, DateTime periodEndExclusive)
        {
            const string sql = @"
                SELECT ISNULL(SUM(dcp.AmountPaid), 0)
                FROM DealerCommissionPayments dcp
                INNER JOIN Contract_Info ci ON ci.ContractID = dcp.ContractId
                INNER JOIN Devices d ON d.Id = ci.ID
                INNER JOIN Dealers dl ON dl.DealerReference = d.DeviceGroupId
                WHERE (@DealerId IS NULL OR dl.DealerId = @DealerId)
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

        private class OrdersAwaitingApprovalSummaryRow
        {
            public int Count { get; set; }
            public int OldestPendingDays { get; set; }
        }

        // Approver Dashboard's Orders Awaiting Approval card -- same
        // definition as ApplicationReportRepository.GetAllWaitingApprovalAsync
        // (Woo_Orders.Status IN ('approval-waiting', 'approved')), system-wide
        // (no dealer scoping -- an Approver reviews every dealer's orders).
        public async Task<(int Count, int OldestPendingDays)> GetOrdersAwaitingApprovalSummaryAsync()
        {
            const string sql = @"
                SELECT
                    COUNT(*) AS Count,
                    ISNULL(DATEDIFF(DAY, MIN(DateCreated), GETDATE()), 0) AS OldestPendingDays
                FROM Woo_Orders
                WHERE [Status] IN ('approval-waiting', 'approved')";

            try
            {
                var row = await _db.QuerySingleAsync<OrdersAwaitingApprovalSummaryRow>(sql);
                return (row.Count, row.OldestPendingDays);
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "Orders-awaiting-approval summary query failed");
                return (0, 0);
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
