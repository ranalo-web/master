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
                       la.Action AS LastAction, la.Success AS LastActionSuccess, la.AtUtc AS LastActionAt, la.Result AS LastActionResult
                FROM Devices d
                INNER JOIN Contract_Info c ON c.ID = d.Id AND c.EndDate IS NULL
                LEFT JOIN Payments p ON p.AccountNo = d.Id
                LEFT JOIN Dealers dl ON dl.DealerReference = d.DeviceGroupId
                OUTER APPLY (SELECT TOP 1 lr.Id, lr.Status FROM LockRecoveries lr
                             WHERE lr.AccountNo = d.Id AND lr.Status NOT IN ('Closed', 'NotCatchable')
                             ORDER BY lr.Id DESC) r
                OUTER APPLY (SELECT TOP 1 a.Action, a.Success, a.AtUtc, a.Result FROM LockCheckActions a
                             WHERE a.AccountNo = d.Id ORDER BY a.AtUtc DESC, a.Id DESC) la
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

        // Fully paid: the contract ends today.
        public async Task<bool> EndContractAsync(int contractId, long accountNo) =>
            await _db.ExecuteAsync(
                "UPDATE Contract_Info SET EndDate = GETDATE() WHERE ContractID = @ContractId AND ID = @AccountNo AND EndDate IS NULL",
                new { ContractId = contractId, AccountNo = accountNo }) == 1;
    }
}
