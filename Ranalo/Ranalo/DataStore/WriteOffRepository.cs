using System.Data;
using Dapper;
using Ranalo.Models;

namespace Ranalo.DataStore
{
    public interface IWriteOffRepository
    {
        Task<int> ReinstatePaidAsync();
        Task<int> ProposeNoPaymentWriteOffsAsync(DateTime today);

        Task<(List<WriteOffRow> Rows, int TotalRecords)> GetPageAsync(string status, string searchTerm, int page, int pageSize);
        Task<List<WriteOffStatusSummary>> GetSummaryAsync();
        Task<int> ApproveAsync(IEnumerable<int> ids, int userId);
        Task<int> ApproveAllPendingAsync(int userId);
        Task<int> HoldAsync(IEnumerable<int> ids, string reason, int userId);

        Task<List<WriteOffRow>> GetHistoryPreviewAsync();
        Task<(int WriteOffs, int Recoveries)> RecordHistoryAsync(int userId);

        Task<WriteOffPeriodSummary> GetPeriodSummaryAsync(DateTime? fromDate, DateTime? toDateExclusive);
        Task<List<LoanBookAgeBucket>> GetLoanBookAgeingAsync(DateTime today);
    }

    // Maintains the WriteOffs register (Database/WriteOffs/001). See that
    // script for the policy. The nightly job (ScheduledWriteOffCheck) calls
    // ReinstatePaidAsync first, then ProposeNoPaymentWriteOffsAsync, so an
    // account that paid starts a fresh 360-day clock before new proposals
    // are made.
    public class WriteOffRepository : IWriteOffRepository
    {
        public const int DaysWithoutPayment = 360;

        private readonly IDbConnection _db;

        public WriteOffRepository(IDbConnection db)
        {
            _db = db;
        }

        // Background work (the nightly check / Run Check Now) scans every
        // payment; the 30-second default is too short.
        private const int CheckTimeoutSeconds = 300;

        // Every payment keyed by the device it belongs to, built once per
        // query: payments on the device's own account number (the current
        // customer's -- a previous customer's were renamed "_R" on recovery)
        // plus orphaned payments assigned to the device. UNION (not UNION
        // ALL) so a payment reachable both ways is counted once.
        private const string DevicePaymentsCte = @"
                device_payments AS (
                    SELECT kp.AccountNoBigint AS AccountNo, kp.MpesaCode, kp.AmountValue, kp.PaymentDateValue
                    FROM KosePayments kp
                    WHERE kp.AccountNoBigint IS NOT NULL
                    UNION
                    SELECT op.AccountNoBigint, kp.MpesaCode, kp.AmountValue, kp.PaymentDateValue
                    FROM OrphanedPayments op
                    INNER JOIN KosePayments kp ON kp.MpesaCode = op.MpesaCode
                    WHERE op.AccountNoBigint IS NOT NULL
                )";

        // Any payment on or after the write-off date brings a "no payment"
        // write-off back, whatever its status: an Approved one is reinstated
        // (full balance back, dated on the payment day), and a Pending/Held
        // proposal is closed the same way so it drops off the review list.
        // Only payments before the contract's EndDate count, so a new
        // customer's payments on a recovered device never reinstate the old
        // contract. Repossessed write-offs are never reinstated -- the device
        // has gone to a new customer.
        public async Task<int> ReinstatePaidAsync()
        {
            var sql = @"
                ;WITH " + DevicePaymentsCte + @",
                first_payment AS (
                    SELECT wo.Id, dp.PaymentDateValue, dp.MpesaCode,
                           ROW_NUMBER() OVER (PARTITION BY wo.Id ORDER BY dp.PaymentDateValue) AS rn
                    FROM WriteOffs wo
                    INNER JOIN Contract_Info ci ON ci.ContractID = wo.ContractId AND ci.ID = wo.AccountNo
                    INNER JOIN device_payments dp ON dp.AccountNo = wo.AccountNo
                    WHERE wo.ReinstatedDate IS NULL
                      AND wo.Reason = 'NoPayment360'
                      AND dp.PaymentDateValue >= wo.WrittenOffDate
                      AND (ci.EndDate IS NULL OR dp.PaymentDateValue < ci.EndDate)
                )
                UPDATE wo
                SET ReinstatedDate = CAST(fp.PaymentDateValue AS DATE),
                    ReinstatedMpesaCode = fp.MpesaCode
                FROM WriteOffs wo
                INNER JOIN first_payment fp ON fp.Id = wo.Id AND fp.rn = 1";

            return await _db.ExecuteAsync(sql, commandTimeout: CheckTimeoutSeconds);
        }

        // Proposes (Status = Pending) a write-off for every open contract
        // that still owes money and has gone DaysWithoutPayment days since
        // its last payment -- or since its start date if it never paid --
        // dated on the day it crossed that line. Skips contracts that
        // already have an open (not reinstated) write-off of any status.
        // Open contracts (EndDate IS NULL) own every payment on their device,
        // so no EndDate cut-off is needed here.
        public async Task<int> ProposeNoPaymentWriteOffsAsync(DateTime today)
        {
            var sql = @"
                ;WITH " + DevicePaymentsCte + @",
                paid AS (
                    SELECT AccountNo, SUM(AmountValue) AS TotalPaid, MAX(PaymentDateValue) AS LastPaymentDate
                    FROM device_payments
                    GROUP BY AccountNo
                ),
                contracts AS (
                    SELECT
                        ci.ContractID,
                        CAST(ci.ID AS BIGINT) AS AccountNo,
                        ci.StartDate,
                        CAST(ci.Deposit + ci.Daily * 30 * ci.Term_in_Months
                             + ci.Weekly * (30.0 / 7.0) * ci.Term_in_Months
                             + ci.Monthly * ci.Term_in_Months AS DECIMAL(18,2)) AS ContractValue,
                        ISNULL(paid.TotalPaid, 0) AS TotalPaid,
                        paid.LastPaymentDate
                    FROM Contract_Info ci
                    LEFT JOIN paid ON paid.AccountNo = ci.ID
                    WHERE ci.EndDate IS NULL
                      AND ci.ContractID IS NOT NULL
                      -- Recovered contracts are created with no StartDate
                      -- (CreateRecoveredAccount); count those from their
                      -- payments instead.
                      AND (ci.StartDate IS NOT NULL OR paid.LastPaymentDate IS NOT NULL)
                ),
                -- One row per ContractID (the latest start) so a duplicated
                -- ContractID can never trip UX_WriteOffs_Contract_Date and
                -- fail the whole insert.
                due AS (
                    SELECT *,
                        DATEADD(DAY, @Days, CAST(COALESCE(LastPaymentDate, StartDate) AS DATE)) AS CrossedDate,
                        ROW_NUMBER() OVER (PARTITION BY ContractID ORDER BY StartDate DESC) AS rn
                    FROM contracts
                    WHERE TotalPaid < ContractValue
                )
                INSERT INTO WriteOffs
                    (ContractId, AccountNo, Reason, WrittenOffDate, LastPaymentDate, ContractValue, TotalPaid, OutstandingBalance, Status)
                SELECT
                    ContractID, AccountNo, 'NoPayment360', CrossedDate, CAST(LastPaymentDate AS DATE),
                    ContractValue, TotalPaid, ContractValue - TotalPaid, 'Pending'
                FROM due
                WHERE rn = 1
                  AND CrossedDate <= @Today
                  AND NOT EXISTS (SELECT 1 FROM WriteOffs wo
                                  WHERE wo.ContractId = due.ContractID
                                    AND (wo.ReinstatedDate IS NULL OR wo.WrittenOffDate = due.CrossedDate))";

            return await _db.ExecuteAsync(sql, new { Days = DaysWithoutPayment, Today = today.Date }, commandTimeout: CheckTimeoutSeconds);
        }

        // --- Review screen -------------------------------------------------

        // "reinstated" is derived (ReinstatedDate set); the other statuses
        // only cover rows that are still open.
        private const string StatusFilter = @"
                (   (@Status = 'reinstated' AND wo.ReinstatedDate IS NOT NULL)
                 OR (@Status <> 'reinstated' AND wo.ReinstatedDate IS NULL AND wo.Status = @Status))";

        private const string SearchFilter = @"
                (@SearchTerm IS NULL
                 OR CAST(wo.AccountNo AS NVARCHAR(30)) LIKE '%' + @SearchTerm + '%'
                 OR ci.First_Name LIKE '%' + @SearchTerm + '%'
                 OR dl.CompanyName LIKE '%' + @SearchTerm + '%')";

        public async Task<(List<WriteOffRow> Rows, int TotalRecords)> GetPageAsync(string status, string searchTerm, int page, int pageSize)
        {
            var fromSql = @"
                FROM WriteOffs wo
                LEFT JOIN Contract_Info ci ON ci.ContractID = wo.ContractId AND ci.ID = wo.AccountNo
                LEFT JOIN Devices d ON d.Id = wo.AccountNo
                LEFT JOIN Dealers dl ON dl.DealerReference = d.DeviceGroupId
                WHERE " + StatusFilter + " AND " + SearchFilter;

            var countSql = "SELECT COUNT(*) " + fromSql;

            var sql = @"
                SELECT
                    wo.Id, wo.ContractId, wo.AccountNo, wo.Reason, wo.WrittenOffDate, wo.LastPaymentDate,
                    wo.ContractValue, wo.TotalPaid, wo.OutstandingBalance, wo.Status, wo.HoldReason,
                    wo.ReviewedAtUtc, wo.ReinstatedDate, wo.ReinstatedMpesaCode,
                    ci.First_Name AS CustomerName,
                    ci.StartDate AS ContractStartDate,
                    ci.BuyingPrice,
                    dl.CompanyName AS DealerName,
                    NULLIF(LTRIM(RTRIM(ISNULL(d.Make, '') + ' ' + ISNULL(d.Model, ''))), '') AS DeviceName,
                    u.[Name] + ' ' + u.[LastName] AS ReviewedByName,
                    ISNULL((SELECT SUM(AmountPaid) FROM DealerCommissionPayments WHERE ContractId = wo.ContractId), 0)
                      + ISNULL((SELECT SUM(AmountPaid) FROM AgentCommissionPayments WHERE ContractId = wo.ContractId), 0) AS CommissionsPaid,
                    ISNULL(rec.RepossessionCost, 0) AS RepossessionCost,
                    ISNULL(rec.ResaleValue, 0) AS ResaleValue
                " + fromSql.Replace("WHERE", @"
                LEFT JOIN Users u ON u.UserId = wo.ReviewedByUserId
                OUTER APPLY (SELECT SUM(RepossessionCost) AS RepossessionCost, SUM(ResaleValue) AS ResaleValue
                             FROM DeviceRecoveries WHERE OldContractId = wo.ContractId) rec
                WHERE") + @"
                ORDER BY wo.WrittenOffDate DESC, wo.Id DESC
                OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

            var parameters = new
            {
                Status = status,
                SearchTerm = string.IsNullOrWhiteSpace(searchTerm) ? null : searchTerm.Trim(),
                Offset = (page - 1) * pageSize,
                PageSize = pageSize,
            };

            var total = await _db.QuerySingleAsync<int>(countSql, parameters);
            var rows = await _db.QueryAsync<WriteOffRow>(sql, parameters);
            return (rows.ToList(), total);
        }

        public async Task<List<WriteOffStatusSummary>> GetSummaryAsync()
        {
            const string sql = @"
                SELECT CASE WHEN ReinstatedDate IS NOT NULL THEN 'reinstated' ELSE LOWER(Status) END AS Status,
                       COUNT(*) AS Count,
                       SUM(OutstandingBalance) AS Outstanding
                FROM WriteOffs
                GROUP BY CASE WHEN ReinstatedDate IS NOT NULL THEN 'reinstated' ELSE LOWER(Status) END";

            return (await _db.QueryAsync<WriteOffStatusSummary>(sql)).ToList();
        }

        // Only open (not reinstated) Pending/Held rows can be approved.
        public async Task<int> ApproveAsync(IEnumerable<int> ids, int userId)
        {
            const string sql = @"
                UPDATE WriteOffs
                SET Status = 'Approved', ReviewedByUserId = @UserId, ReviewedAtUtc = SYSUTCDATETIME(), HoldReason = NULL
                WHERE Id IN @Ids AND ReinstatedDate IS NULL AND Status IN ('Pending', 'Held')";

            var idList = ids.Distinct().ToList();
            return idList.Count == 0 ? 0 : await _db.ExecuteAsync(sql, new { Ids = idList, UserId = userId });
        }

        public async Task<int> ApproveAllPendingAsync(int userId)
        {
            const string sql = @"
                UPDATE WriteOffs
                SET Status = 'Approved', ReviewedByUserId = @UserId, ReviewedAtUtc = SYSUTCDATETIME()
                WHERE ReinstatedDate IS NULL AND Status = 'Pending'";

            return await _db.ExecuteAsync(sql, new { UserId = userId });
        }

        public async Task<int> HoldAsync(IEnumerable<int> ids, string reason, int userId)
        {
            const string sql = @"
                UPDATE WriteOffs
                SET Status = 'Held', HoldReason = @Reason, ReviewedByUserId = @UserId, ReviewedAtUtc = SYSUTCDATETIME()
                WHERE Id IN @Ids AND ReinstatedDate IS NULL AND Status IN ('Pending', 'Held')";

            var idList = ids.Distinct().ToList();
            return idList.Count == 0 ? 0 : await _db.ExecuteAsync(sql, new { Ids = idList, Reason = reason, UserId = userId });
        }

        // --- Historical rebuild (one-off) -----------------------------------

        // Rebuilds every past write-off event from payment history, using the
        // same policy as the nightly check. For each contract, payments are
        // walked in date order from its start date (a recovered contract has
        // no StartDate, so it starts at its first payment):
        //  * a gap of more than 360 days that later ended with a payment is a
        //    write-off on day 360, reinstated on that payment;
        //  * a contract ended by a device recovery is written off on day 360
        //    of its last gap if that came before the recovery, otherwise as
        //    Repossessed on the recovery (EndDate) date.
        // Current gaps on open contracts are left to the nightly check.
        //
        // Payments: an open contract owns its device's un-renamed payments
        // (plus assigned orphaned ones); an ended contract's payments were
        // renamed "<device>_R" on recovery, so they're mapped back to the
        // device and taken between the contract's start (or the previous
        // contract's end) and its EndDate.
        private const string HistoryCte = @"
                ;WITH raw_payments AS (
                    SELECT kp.MpesaCode, kp.AmountValue, kp.PaymentDateValue,
                           CAST(kp.AccountNo AS NVARCHAR(50)) AS RawAccount, kp.AccountNoBigint AS AccountBig
                    FROM KosePayments kp
                    UNION ALL
                    SELECT kp.MpesaCode, kp.AmountValue, kp.PaymentDateValue,
                           CAST(op.AccountNo AS NVARCHAR(50)), op.AccountNoBigint
                    FROM OrphanedPayments op
                    INNER JOIN KosePayments kp ON kp.MpesaCode = op.MpesaCode
                ),
                device_payments AS (
                    SELECT DISTINCT MpesaCode, AmountValue, PaymentDateValue,
                        CASE WHEN RawAccount LIKE '%\_R' ESCAPE '\'
                             THEN TRY_CAST(LEFT(RawAccount, LEN(RawAccount) - 2) AS BIGINT)
                             ELSE AccountBig END AS AccountNo,
                        CASE WHEN RawAccount LIKE '%\_R' ESCAPE '\' THEN 1 ELSE 0 END AS IsRenamed
                    FROM raw_payments
                ),
                contracts AS (
                    SELECT ci.ContractID, CAST(ci.ID AS BIGINT) AS AccountNo, ci.StartDate, ci.EndDate,
                        CAST(ci.Deposit + ci.Daily * 30 * ci.Term_in_Months
                             + ci.Weekly * (30.0 / 7.0) * ci.Term_in_Months
                             + ci.Monthly * ci.Term_in_Months AS DECIMAL(18,2)) AS ContractValue,
                        COALESCE(ci.StartDate,
                                 (SELECT MAX(p.EndDate) FROM Contract_Info p
                                  WHERE p.ID = ci.ID AND p.EndDate IS NOT NULL
                                    AND (ci.EndDate IS NULL OR p.EndDate < ci.EndDate))) AS LowerBound
                    FROM Contract_Info ci
                    WHERE ci.ContractID IS NOT NULL
                ),
                contract_payments AS (
                    SELECT c.ContractID, c.AccountNo, dp.PaymentDateValue, dp.MpesaCode, dp.AmountValue, 1 AS IsPayment
                    FROM contracts c
                    INNER JOIN device_payments dp
                        ON dp.AccountNo = c.AccountNo
                       AND dp.IsRenamed = CASE WHEN c.EndDate IS NULL THEN 0 ELSE 1 END
                       AND (c.LowerBound IS NULL OR dp.PaymentDateValue >= c.LowerBound)
                       AND (c.EndDate IS NULL OR dp.PaymentDateValue < c.EndDate)
                    UNION ALL
                    -- The contract start is where the first 360 days count from.
                    SELECT c.ContractID, c.AccountNo, c.StartDate, NULL, 0, 0
                    FROM contracts c
                    WHERE c.StartDate IS NOT NULL
                ),
                timeline AS (
                    SELECT cp.*,
                        SUM(cp.AmountValue) OVER (PARTITION BY cp.ContractID, cp.AccountNo
                            ORDER BY cp.PaymentDateValue, cp.IsPayment, cp.MpesaCode ROWS UNBOUNDED PRECEDING) AS PaidToDate,
                        LEAD(cp.PaymentDateValue) OVER (PARTITION BY cp.ContractID, cp.AccountNo
                            ORDER BY cp.PaymentDateValue, cp.IsPayment, cp.MpesaCode) AS NextPaymentDate,
                        LEAD(cp.MpesaCode) OVER (PARTITION BY cp.ContractID, cp.AccountNo
                            ORDER BY cp.PaymentDateValue, cp.IsPayment, cp.MpesaCode) AS NextMpesaCode
                    FROM contract_payments cp
                ),
                history AS (
                    -- Gaps of more than 360 days that ended with a payment.
                    SELECT t.ContractID, t.AccountNo, CAST('NoPayment360' AS VARCHAR(20)) AS Reason,
                        DATEADD(DAY, @Days, CAST(t.PaymentDateValue AS DATE)) AS WrittenOffDate,
                        CASE WHEN t.IsPayment = 1 THEN CAST(t.PaymentDateValue AS DATE) END AS LastPaymentDate,
                        c.ContractValue, t.PaidToDate AS TotalPaid,
                        CAST(t.NextPaymentDate AS DATE) AS ReinstatedDate, t.NextMpesaCode AS ReinstatedMpesaCode
                    FROM timeline t
                    INNER JOIN contracts c ON c.ContractID = t.ContractID AND c.AccountNo = t.AccountNo
                    WHERE t.NextPaymentDate IS NOT NULL
                      AND DATEDIFF(DAY, CAST(t.PaymentDateValue AS DATE), CAST(t.NextPaymentDate AS DATE)) > @Days
                      AND t.PaidToDate < c.ContractValue
                    UNION ALL
                    -- Contracts ended by a recovery, still owing at the end.
                    SELECT t.ContractID, t.AccountNo,
                        CAST(CASE WHEN DATEADD(DAY, @Days, CAST(t.PaymentDateValue AS DATE)) < CAST(c.EndDate AS DATE)
                                  THEN 'NoPayment360' ELSE 'Repossessed' END AS VARCHAR(20)),
                        CASE WHEN DATEADD(DAY, @Days, CAST(t.PaymentDateValue AS DATE)) < CAST(c.EndDate AS DATE)
                             THEN DATEADD(DAY, @Days, CAST(t.PaymentDateValue AS DATE))
                             ELSE CAST(c.EndDate AS DATE) END,
                        CASE WHEN t.IsPayment = 1 THEN CAST(t.PaymentDateValue AS DATE) END,
                        c.ContractValue, t.PaidToDate, NULL, NULL
                    FROM timeline t
                    INNER JOIN contracts c ON c.ContractID = t.ContractID AND c.AccountNo = t.AccountNo
                    WHERE t.NextPaymentDate IS NULL
                      AND c.EndDate IS NOT NULL
                      AND t.PaidToDate < c.ContractValue
                      -- Only a device that was enrolled can be recovered: a
                      -- contract on an account with no device (a duplicate,
                      -- or a dealer's ghost customer) is ended without being
                      -- a repossession; fraud is written off by an admin.
                      AND EXISTS (SELECT 1 FROM Devices dv WHERE dv.Id = c.AccountNo)
                ),
                pending_history AS (
                    SELECT h.*
                    FROM history h
                    WHERE h.WrittenOffDate <= CAST(GETDATE() AS DATE)
                      AND NOT EXISTS (SELECT 1 FROM WriteOffs wo
                                      WHERE wo.ContractId = h.ContractID
                                        AND (wo.WrittenOffDate = h.WrittenOffDate
                                             -- an ended contract already written off (e.g. on recovery since step 1)
                                             OR (h.ReinstatedDate IS NULL AND wo.ReinstatedDate IS NULL)))
                )";

        public async Task<List<WriteOffRow>> GetHistoryPreviewAsync()
        {
            var sql = HistoryCte + @"
                SELECT
                    0 AS Id, ph.ContractID AS ContractId, ph.AccountNo, ph.Reason, ph.WrittenOffDate, ph.LastPaymentDate,
                    ph.ContractValue, ph.TotalPaid, ph.ContractValue - ph.TotalPaid AS OutstandingBalance,
                    'Pending' AS Status, ph.ReinstatedDate, ph.ReinstatedMpesaCode,
                    ci.First_Name AS CustomerName, ci.StartDate AS ContractStartDate,
                    dl.CompanyName AS DealerName
                FROM pending_history ph
                LEFT JOIN Contract_Info ci ON ci.ContractID = ph.ContractID AND ci.ID = ph.AccountNo
                LEFT JOIN Devices d ON d.Id = ph.AccountNo
                LEFT JOIN Dealers dl ON dl.DealerReference = d.DeviceGroupId
                ORDER BY ph.WrittenOffDate";

            var rows = await _db.QueryAsync<WriteOffRow>(sql, new { Days = DaysWithoutPayment }, commandTimeout: CheckTimeoutSeconds);
            return rows.ToList();
        }

        // Records the rebuilt history as Approved (the admin approves it as
        // a whole from the preview), and adds a DeviceRecoveries row for each
        // past recovery that doesn't have one -- resale value = the next
        // contract's value on the same device, repossession cost unknown (0).
        public async Task<(int WriteOffs, int Recoveries)> RecordHistoryAsync(int userId)
        {
            if (_db.State != ConnectionState.Open)
            {
                _db.Open();
            }

            using var transaction = _db.BeginTransaction();

            var writeOffsSql = HistoryCte + @"
                INSERT INTO WriteOffs
                    (ContractId, AccountNo, Reason, WrittenOffDate, LastPaymentDate, ContractValue, TotalPaid, OutstandingBalance,
                     Status, ReviewedByUserId, ReviewedAtUtc, ReinstatedDate, ReinstatedMpesaCode)
                SELECT ContractID, AccountNo, Reason, WrittenOffDate, LastPaymentDate, ContractValue, TotalPaid, ContractValue - TotalPaid,
                       'Approved', @UserId, SYSUTCDATETIME(), ReinstatedDate, ReinstatedMpesaCode
                FROM pending_history";

            var writeOffs = await _db.ExecuteAsync(writeOffsSql, new { Days = DaysWithoutPayment, UserId = userId }, transaction, CheckTimeoutSeconds);

            const string recoveriesSql = @"
                INSERT INTO DeviceRecoveries
                    (AccountNo, OldContractId, NewContractId, RecoveredDate, ResaleValue, RepossessionCost, Notes, RecordedByUserId)
                SELECT CAST(ci.ID AS BIGINT), ci.ContractID, nxt.ContractID, CAST(ci.EndDate AS DATE),
                       ISNULL(nxt.ContractValue, 0), 0, 'Recorded from history; repossession cost not known', @UserId
                FROM Contract_Info ci
                OUTER APPLY (
                    SELECT TOP 1 n.ContractID,
                        CAST(n.Deposit + n.Daily * 30 * n.Term_in_Months
                             + n.Weekly * (30.0 / 7.0) * n.Term_in_Months
                             + n.Monthly * n.Term_in_Months AS DECIMAL(18,2)) AS ContractValue
                    FROM Contract_Info n
                    WHERE n.ID = ci.ID AND n.ContractID <> ci.ContractID
                      AND (n.EndDate IS NULL OR n.EndDate > ci.EndDate)
                    ORDER BY CASE WHEN n.EndDate IS NULL THEN 1 ELSE 0 END, n.EndDate
                ) nxt
                WHERE ci.EndDate IS NOT NULL
                  AND ci.ContractID IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM DeviceRecoveries r WHERE r.OldContractId = ci.ContractID)";

            var recoveries = await _db.ExecuteAsync(recoveriesSql, new { UserId = userId }, transaction, CheckTimeoutSeconds);

            transaction.Commit();
            return (writeOffs, recoveries);
        }

        // --- Financials ------------------------------------------------------

        // Approved write-offs dated in the period (including ones later
        // reinstated -- they were written off in that period), and approved
        // write-offs reinstated in the period. Real loss = device cost +
        // commissions + repossession cost - paid - resale, summed only where
        // the device cost is known.
        public async Task<WriteOffPeriodSummary> GetPeriodSummaryAsync(DateTime? fromDate, DateTime? toDateExclusive)
        {
            const string sql = @"
                ;WITH wo AS (
                    SELECT w.*,
                        ci.BuyingPrice,
                        ISNULL((SELECT SUM(AmountPaid) FROM DealerCommissionPayments WHERE ContractId = w.ContractId), 0)
                          + ISNULL((SELECT SUM(AmountPaid) FROM AgentCommissionPayments WHERE ContractId = w.ContractId), 0) AS CommissionsPaid,
                        ISNULL((SELECT SUM(RepossessionCost) FROM DeviceRecoveries WHERE OldContractId = w.ContractId), 0) AS RepossessionCost,
                        ISNULL((SELECT SUM(ResaleValue) FROM DeviceRecoveries WHERE OldContractId = w.ContractId), 0) AS ResaleValue
                    FROM WriteOffs w
                    OUTER APPLY (SELECT TOP 1 c.BuyingPrice FROM Contract_Info c
                                 WHERE c.ContractID = w.ContractId AND c.ID = w.AccountNo) ci
                    WHERE w.Status = 'Approved'
                )
                SELECT
                    ISNULL(SUM(CASE WHEN InPeriod = 1 THEN 1 ELSE 0 END), 0) AS WrittenOffCount,
                    ISNULL(SUM(CASE WHEN InPeriod = 1 THEN OutstandingBalance END), 0) AS WrittenOffBalance,
                    ISNULL(SUM(CASE WHEN InPeriod = 1 AND BuyingPrice IS NOT NULL
                                    THEN BuyingPrice + CommissionsPaid + RepossessionCost - TotalPaid - ResaleValue END), 0) AS RealLoss,
                    ISNULL(SUM(CASE WHEN InPeriod = 1 AND BuyingPrice IS NULL THEN 1 ELSE 0 END), 0) AS MissingCostCount,
                    ISNULL(SUM(CASE WHEN ReinstatedInPeriod = 1 THEN 1 ELSE 0 END), 0) AS ReinstatedCount,
                    ISNULL(SUM(CASE WHEN ReinstatedInPeriod = 1 THEN OutstandingBalance END), 0) AS ReinstatedBalance
                FROM (
                    SELECT wo.*,
                        CASE WHEN (@FromDate IS NULL OR WrittenOffDate >= @FromDate)
                              AND (@ToDate IS NULL OR WrittenOffDate < @ToDate) THEN 1 ELSE 0 END AS InPeriod,
                        CASE WHEN ReinstatedDate IS NOT NULL
                              AND (@FromDate IS NULL OR ReinstatedDate >= @FromDate)
                              AND (@ToDate IS NULL OR ReinstatedDate < @ToDate) THEN 1 ELSE 0 END AS ReinstatedInPeriod
                    FROM wo
                ) x";

            return await _db.QuerySingleAsync<WriteOffPeriodSummary>(sql,
                new { FromDate = fromDate, ToDate = toDateExclusive }, commandTimeout: CheckTimeoutSeconds);
        }

        // Open contracts still owing money, grouped by days since the last
        // payment (or since the start if never paid) -- the age-of-debt
        // report and the balance sheet's loan book. Contracts with an
        // approved, not-reinstated write-off are their own "Written off"
        // bucket (off the loan book); ones awaiting review are "Pending
        // write-off". Same payment rules as the write-off check.
        public async Task<List<LoanBookAgeBucket>> GetLoanBookAgeingAsync(DateTime today)
        {
            var sql = @"
                ;WITH " + DevicePaymentsCte + @",
                paid AS (
                    SELECT AccountNo, SUM(AmountValue) AS TotalPaid, MAX(PaymentDateValue) AS LastPaymentDate
                    FROM device_payments
                    GROUP BY AccountNo
                ),
                open_contracts AS (
                    SELECT ci.ContractID, ci.ID,
                        CAST(ci.Deposit + ci.Daily * 30 * ci.Term_in_Months
                             + ci.Weekly * (30.0 / 7.0) * ci.Term_in_Months
                             + ci.Monthly * ci.Term_in_Months AS DECIMAL(18,2)) - ISNULL(paid.TotalPaid, 0) AS Outstanding,
                        DATEDIFF(DAY, CAST(COALESCE(paid.LastPaymentDate, ci.StartDate) AS DATE), @Today) AS DaysSince,
                        (SELECT TOP 1 wo.Status FROM WriteOffs wo
                         WHERE wo.ContractId = ci.ContractID AND wo.AccountNo = ci.ID AND wo.ReinstatedDate IS NULL
                         ORDER BY wo.WrittenOffDate DESC) AS OpenWriteOffStatus
                    FROM Contract_Info ci
                    LEFT JOIN paid ON paid.AccountNo = ci.ID
                    WHERE ci.EndDate IS NULL
                      AND (ci.StartDate IS NOT NULL OR paid.LastPaymentDate IS NOT NULL)
                ),
                bucketed AS (
                    SELECT Outstanding,
                        CASE
                            WHEN OpenWriteOffStatus = 'Approved' THEN 7
                            WHEN OpenWriteOffStatus IS NOT NULL THEN 6
                            WHEN DaysSince <= 30 THEN 1
                            WHEN DaysSince <= 90 THEN 2
                            WHEN DaysSince <= 180 THEN 3
                            WHEN DaysSince <= 360 THEN 4
                            ELSE 5
                        END AS SortOrder
                    FROM open_contracts
                    WHERE Outstanding > 0
                )
                SELECT SortOrder,
                    CASE SortOrder
                        WHEN 1 THEN '0-30 days' WHEN 2 THEN '31-90 days' WHEN 3 THEN '91-180 days'
                        WHEN 4 THEN '181-360 days' WHEN 5 THEN 'Over 360 days (not yet proposed)'
                        WHEN 6 THEN 'Pending write-off review' ELSE 'Written off' END AS Label,
                    COUNT(*) AS Accounts,
                    SUM(Outstanding) AS Outstanding
                FROM bucketed
                GROUP BY SortOrder
                ORDER BY SortOrder";

            var rows = await _db.QueryAsync<LoanBookAgeBucket>(sql, new { Today = today.Date }, commandTimeout: CheckTimeoutSeconds);
            return rows.ToList();
        }
    }
}
