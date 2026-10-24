using System.Data;
using Dapper;
using Ranalo.Models;

namespace Ranalo.DataStore
{
    public interface ICommissionExclusionRepository
    {
        // Sets a contract's commission status (CommissionStatus.*) and logs
        // the change. Returns false when it already had that status.
        Task<bool> SetStatusAsync(long contractId, string status, string? reason, string? notes, int changedByUserId);

        Task<List<CommissionExclusionLogEntry>> GetLogAsync(long contractId);
    }

    // Writes CommissionExclusions and CommissionExclusionLog
    // (Database/Commissions/005). Reads for the commission figures are in
    // DashboardReportRepository.GetCommissionExclusionsAsync.
    public class CommissionExclusionRepository : ICommissionExclusionRepository
    {
        private readonly IDbConnection _db;

        public CommissionExclusionRepository(IDbConnection db)
        {
            _db = db;
        }

        public async Task<bool> SetStatusAsync(long contractId, string status, string? reason, string? notes, int changedByUserId)
        {
            if (_db.State != ConnectionState.Open)
            {
                _db.Open();
            }

            using var transaction = _db.BeginTransaction(IsolationLevel.Serializable);

            var current = await _db.QuerySingleOrDefaultAsync<CommissionExclusion>(
                "SELECT ContractId, Reason, RecoverArrears, Notes FROM CommissionExclusions WITH (UPDLOCK) WHERE ContractId = @ContractId",
                new { ContractId = contractId }, transaction);
            var oldStatus = CommissionStatus.Of(current);
            var newReason = status == CommissionStatus.Qualifies ? null : reason ?? current?.Reason ?? CommissionExclusionReason.Legacy;

            if (oldStatus == status && (status == CommissionStatus.Qualifies || newReason == current?.Reason))
            {
                transaction.Rollback();
                return false;
            }

            var args = new
            {
                ContractId = contractId,
                Reason = newReason,
                RecoverArrears = status == CommissionStatus.Recover,
                Notes = notes,
                UserId = changedByUserId,
                OldStatus = oldStatus,
                NewStatus = status,
            };

            if (status == CommissionStatus.Qualifies)
            {
                await _db.ExecuteAsync("DELETE FROM CommissionExclusions WHERE ContractId = @ContractId", args, transaction);
            }
            else if (current == null)
            {
                await _db.ExecuteAsync(@"
                    INSERT INTO CommissionExclusions (ContractId, Reason, RecoverArrears, Notes, SetByUserId, SetAtUtc)
                    VALUES (@ContractId, @Reason, @RecoverArrears, @Notes, @UserId, SYSUTCDATETIME())", args, transaction);
            }
            else
            {
                await _db.ExecuteAsync(@"
                    UPDATE CommissionExclusions
                    SET Reason = @Reason, RecoverArrears = @RecoverArrears, Notes = @Notes,
                        SetByUserId = @UserId, SetAtUtc = SYSUTCDATETIME()
                    WHERE ContractId = @ContractId", args, transaction);
            }

            await _db.ExecuteAsync(@"
                INSERT INTO CommissionExclusionLog (ContractId, OldStatus, NewStatus, Reason, Notes, ChangedByUserId, ChangedAtUtc)
                VALUES (@ContractId, @OldStatus, @NewStatus, @Reason, @Notes, @UserId, SYSUTCDATETIME())", args, transaction);

            transaction.Commit();
            return true;
        }

        public async Task<List<CommissionExclusionLogEntry>> GetLogAsync(long contractId) =>
            (await _db.QueryAsync<CommissionExclusionLogEntry>(@"
                SELECT l.ContractId, l.OldStatus, l.NewStatus, l.Reason, l.Notes, l.ChangedByUserId,
                       LTRIM(RTRIM(ISNULL(u.[Name], '') + ' ' + ISNULL(u.[LastName], ''))) AS ChangedByName, l.ChangedAtUtc
                FROM CommissionExclusionLog l
                LEFT JOIN Users u ON u.UserId = l.ChangedByUserId
                WHERE l.ContractId = @ContractId
                ORDER BY l.Id DESC", new { ContractId = contractId })).ToList();
    }
}
