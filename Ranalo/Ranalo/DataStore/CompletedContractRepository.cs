using System.Data;
using Dapper;
using Ranalo.Models;

namespace Ranalo.DataStore
{
    public interface ICompletedContractRepository
    {
        Task<List<CompletedContractRow>> GetClosedAsync();
        Task<CompletedContractRow?> GetAsync(int contractId);
        Task<(List<CompletedPayment> Page, int Total)> GetPaymentsAsync(int contractId, int page, int pageSize);
    }

    // Closed contracts with what was paid to each. An account can carry more
    // than one contract: a repo contract's payments were renamed to its
    // PaymentAccount (<acc>_R), and otherwise a contract's payments run from
    // its start (from the first payment, for the account's first contract)
    // up to the start of the account's next contract.
    public class CompletedContractRepository : ICompletedContractRepository
    {
        private readonly IDbConnection _db;

        public CompletedContractRepository(IDbConnection db)
        {
            _db = db;
        }

        private const string SelectSql = @"
            SELECT ci.ContractID AS ContractId, ci.ID AS AccountNo, ci.First_Name AS CustomerName,
                   dl.CompanyName AS DealerName,
                   NULLIF(LTRIM(RTRIM(ISNULL(au.[Name], '') + ' ' + ISNULL(au.[LastName], ''))), '') AS AgentName,
                   d.Make, d.Model, CAST(d.ImeiNo AS VARCHAR(20)) AS Imei, d.LockGroup, d.Status AS NuovoStatus,
                   ci.StartDate, ci.EndDate, ci.Deposit, ci.Daily, ci.Weekly, ci.Monthly, ci.Term_in_Months AS TermInMonths,
                   ci.BuyingPrice, ci.PaymentAccount,
                   ISNULL(p.Paid, 0) AS Paid, ISNULL(p.PaymentCount, 0) AS PaymentCount, p.FirstPaid, p.LastPaid,
                   rt.Status AS ReleaseStatus, rt.CompletedAtUtc AS ReleaseCompletedAt
            FROM Contract_Info ci
            LEFT JOIN Devices d ON d.Id = ci.ID
            LEFT JOIN Dealers dl ON dl.DealerReference = COALESCE(ci.DealerReference, d.DeviceGroupId)
            LEFT JOIN Users au ON au.UserId = ci.AssignedAgentId
            CROSS APPLY (" + WindowSql + @") w
            OUTER APPLY (
                SELECT SUM(x.AmountValue) AS Paid, COUNT(*) AS PaymentCount,
                       MIN(x.PaymentDateValue) AS FirstPaid, MAX(x.PaymentDateValue) AS LastPaid
                FROM (" + PaymentsSql + @") x
            ) p
            OUTER APPLY (SELECT TOP 1 t.Status, t.CompletedAtUtc FROM DeviceRemovalTasks t
                         WHERE t.AccountId = ci.ID ORDER BY t.Id DESC) rt
            WHERE ci.EndDate IS NOT NULL";

        // The dates this contract's payments fall between (NULL = open-ended).
        private const string WindowSql = @"
                SELECT CASE WHEN EXISTS (SELECT 1 FROM Contract_Info x
                                         WHERE x.ID = ci.ID AND x.PaymentAccount IS NULL AND x.ContractID < ci.ContractID)
                            THEN ci.StartDate END AS FromDate,
                       (SELECT MIN(x.StartDate) FROM Contract_Info x
                        WHERE x.ID = ci.ID AND x.PaymentAccount IS NULL AND x.ContractID > ci.ContractID) AS ToDate";

        // This contract's payments, direct or assigned from Orphaned payments.
        private const string PaymentsSql = @"
                    SELECT y.MpesaCode, y.AmountValue, y.PaymentDateValue
                    FROM (
                        SELECT kp.MpesaCode, kp.AmountValue, kp.PaymentDateValue
                        FROM KosePayments kp
                        WHERE ci.PaymentAccount IS NULL AND kp.AccountNoBigint = ci.ID
                        UNION
                        SELECT kp.MpesaCode, kp.AmountValue, kp.PaymentDateValue
                        FROM OrphanedPayments op
                        INNER JOIN KosePayments kp ON kp.MpesaCode = op.MpesaCode
                        WHERE ci.PaymentAccount IS NULL AND op.AccountNoBigint = ci.ID
                        UNION
                        SELECT kp.MpesaCode, kp.AmountValue, kp.PaymentDateValue
                        FROM KosePayments kp
                        WHERE ci.PaymentAccount IS NOT NULL AND LTRIM(RTRIM(kp.AccountNo)) = ci.PaymentAccount
                        UNION
                        SELECT kp.MpesaCode, kp.AmountValue, kp.PaymentDateValue
                        FROM OrphanedPayments op
                        INNER JOIN KosePayments kp ON kp.MpesaCode = op.MpesaCode
                        WHERE ci.PaymentAccount IS NOT NULL AND LTRIM(RTRIM(op.AccountNo)) = ci.PaymentAccount
                    ) y
                    WHERE (w.FromDate IS NULL OR y.PaymentDateValue >= w.FromDate)
                      AND (w.ToDate IS NULL OR y.PaymentDateValue < w.ToDate)";

        public async Task<List<CompletedContractRow>> GetClosedAsync() =>
            (await _db.QueryAsync<CompletedContractRow>(SelectSql, commandTimeout: 120)).ToList();

        public async Task<CompletedContractRow?> GetAsync(int contractId) =>
            await _db.QueryFirstOrDefaultAsync<CompletedContractRow>(SelectSql + " AND ci.ContractID = @ContractId",
                new { ContractId = contractId });

        // Newest first, one page at a time.
        public async Task<(List<CompletedPayment> Page, int Total)> GetPaymentsAsync(int contractId, int page, int pageSize)
        {
            using var multi = await _db.QueryMultipleAsync(@"
                SELECT COUNT(*)
                FROM Contract_Info ci CROSS APPLY (" + WindowSql + @") w CROSS APPLY (" + PaymentsSql + @") x
                WHERE ci.ContractID = @ContractId;
                SELECT x.MpesaCode, x.AmountValue AS Amount, x.PaymentDateValue AS PaymentDate
                FROM Contract_Info ci CROSS APPLY (" + WindowSql + @") w CROSS APPLY (" + PaymentsSql + @") x
                WHERE ci.ContractID = @ContractId
                ORDER BY x.PaymentDateValue DESC, x.MpesaCode
                OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY;",
                new { ContractId = contractId, Skip = (Math.Max(page, 1) - 1) * pageSize, Take = pageSize });
            var total = await multi.ReadSingleAsync<int>();
            var rows = (await multi.ReadAsync<CompletedPayment>()).ToList();
            return (rows, total);
        }
    }
}
