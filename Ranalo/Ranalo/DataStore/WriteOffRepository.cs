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
                      AND ci.StartDate IS NOT NULL
                      AND ci.ContractID IS NOT NULL
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
    }
}
