using System.Data;
using Dapper;
using Ranalo.Models;

namespace Ranalo.DataStore
{
    public interface ILockCheckRepository
    {
        Task<List<LockCheckRow>> GetNuovoRowsAsync();
        Task LogAsync(long accountNo, string action, bool success, string? result, int? userId);
        Task<bool> EndContractAsync(int contractId, long accountNo);
        Task<bool> EndPaidOffContractAsync(long accountNo);
    }

    // Nuovo phones with open contracts, their payments and what was last
    // done to them (Database/LockCheck/001).
    public class LockCheckRepository : ILockCheckRepository
    {
        private readonly IDbConnection _db;

        public LockCheckRepository(IDbConnection db)
        {
            _db = db;
        }

        public async Task<List<LockCheckRow>> GetNuovoRowsAsync() =>
            (await _db.QueryAsync<LockCheckRow>(@"
                ;WITH Payments AS (
                    SELECT COALESCE(op.AccountNoBigint, kp.AccountNoBigint) AS AccountNo,
                           SUM(kp.AmountValue) AS Paid, MAX(kp.PaymentDateValue) AS LastPaid
                    FROM KosePayments kp
                    LEFT JOIN OrphanedPayments op ON op.MpesaCode = kp.MpesaCode
                    GROUP BY COALESCE(op.AccountNoBigint, kp.AccountNoBigint)
                )
                SELECT d.Id AS AccountNo, c.ContractID AS ContractId, c.First_Name AS CustomerName,
                       d.Make, d.Model, CAST(d.ImeiNo AS VARCHAR(20)) AS Imei, d.Status AS NuovoStatus,
                       d.Locked, d.NextLockDate, d.LastConnectedAt, d.LockGroup, dl.CompanyName AS DealerName,
                       c.Deposit, c.Daily, c.Weekly, c.Monthly, c.Term_in_Months AS TermInMonths, c.StartDate,
                       ISNULL(p.Paid, 0) AS Paid, p.LastPaid,
                       r.Id AS RecoveryId, r.Status AS RecoveryStatus,
                       la.Action AS LastAction, la.Success AS LastActionSuccess, la.AtUtc AS LastActionAt, la.Result AS LastActionResult,
                       CAST(CASE WHEN rs.Date_Agreed IS NOT NULL THEN 1 ELSE 0 END AS BIT) AS Restructured,
                       rs.Date_Agreed AS RestructureAgreed, rs.Amount_Res AS RestructureDaily,
                       ISNULL(rp.PaidSinceAgreed, 0) AS PaidSinceRestructure
                FROM Devices d
                INNER JOIN Contract_Info c ON c.ID = d.Id AND c.EndDate IS NULL
                LEFT JOIN Payments p ON p.AccountNo = d.Id
                LEFT JOIN Dealers dl ON dl.DealerReference = d.DeviceGroupId
                OUTER APPLY (SELECT TOP 1 lr.Id, lr.Status FROM LockRecoveries lr
                             WHERE lr.AccountNo = d.Id AND lr.Status NOT IN ('Closed', 'NotCatchable')
                             ORDER BY lr.Id DESC) r
                OUTER APPLY (SELECT TOP 1 a.Action, a.Success, a.AtUtc, a.Result FROM LockCheckActions a
                             WHERE a.AccountNo = d.Id ORDER BY a.AtUtc DESC, a.Id DESC) la
                -- The agreed restructure, and what's been paid since it was
                -- agreed (as the Restructured page works it out).
                OUTER APPLY (SELECT TOP 1 rr.Date_Agreed, rr.Amount_Res FROM RestructuredRecords rr
                             WHERE rr.AccountNo = d.Id ORDER BY rr.Date_Agreed DESC) rs
                OUTER APPLY (SELECT SUM(kp.AmountValue) AS PaidSinceAgreed
                             FROM KosePayments kp
                             LEFT JOIN OrphanedPayments op ON op.MpesaCode = kp.MpesaCode
                             WHERE rs.Date_Agreed IS NOT NULL
                               AND COALESCE(op.AccountNoBigint, kp.AccountNoBigint) = d.Id
                               AND kp.PaymentDateValue > rs.Date_Agreed) rp
                WHERE ISNULL(d.LockGroup, 1) = 1", commandTimeout: 120)).ToList();

        public async Task LogAsync(long accountNo, string action, bool success, string? result, int? userId) =>
            await _db.ExecuteAsync(@"
                INSERT INTO LockCheckActions (AccountNo, Action, Success, Result, ByUserId)
                VALUES (@AccountNo, @Action, @Success, @Result, @UserId)",
                new
                {
                    AccountNo = accountNo, Action = action, Success = success,
                    Result = result is { Length: > 1000 } ? result[..1000] : result, UserId = userId,
                });

        // Fully paid: the contract ends on the date of the payment that paid
        // it off (running total reaches the contract value, less the 1-shilling
        // tolerance) -- not the day it's closed. Today if that can't be found.
        public async Task<bool> EndContractAsync(int contractId, long accountNo) =>
            await _db.ExecuteAsync(EndContractSql + @"
                WHERE ci.ContractID = @ContractId AND ci.ID = @AccountNo AND ci.EndDate IS NULL",
                new { ContractId = contractId, AccountNo = accountNo, Tolerance = Ranalo.Services.LockCheckRules.PaidTolerance }) == 1;

        // The fully-paid job (any lock group): the account's open contract,
        // dated as above. Left alone when the account has more than one open
        // contract -- its payments can't be told apart here.
        public async Task<bool> EndPaidOffContractAsync(long accountNo) =>
            await _db.ExecuteAsync(EndContractSql + @"
                WHERE ci.ID = @AccountNo AND ci.EndDate IS NULL
                  AND (SELECT COUNT(*) FROM Contract_Info x WHERE x.ID = ci.ID AND x.EndDate IS NULL) = 1",
                new { AccountNo = accountNo, Tolerance = Ranalo.Services.LockCheckRules.PaidTolerance }) == 1;

        private const string EndContractSql = @"
                UPDATE ci SET EndDate = COALESCE(po.PaidOffAt, GETDATE())
                FROM Contract_Info ci
                OUTER APPLY (
                    SELECT TOP 1 x.PaymentDateValue AS PaidOffAt
                    FROM (
                        SELECT p.PaymentDateValue,
                               SUM(p.AmountValue) OVER (ORDER BY p.PaymentDateValue, p.MpesaCode ROWS UNBOUNDED PRECEDING) AS RunningPaid
                        FROM (SELECT kp.MpesaCode, kp.AmountValue, kp.PaymentDateValue
                              FROM KosePayments kp
                              LEFT JOIN OrphanedPayments op ON op.MpesaCode = kp.MpesaCode
                              WHERE COALESCE(op.AccountNoBigint, kp.AccountNoBigint) = ci.ID) p
                    ) x
                    WHERE x.RunningPaid >= ci.Deposit + ci.Daily * 30 * ci.Term_in_Months
                                           + ci.Weekly * (30.0 / 7.0) * ci.Term_in_Months + ci.Monthly * ci.Term_in_Months - @Tolerance
                    ORDER BY x.PaymentDateValue
                ) po";
    }
}
