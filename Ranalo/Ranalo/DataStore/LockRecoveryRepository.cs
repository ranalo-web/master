using System.Data;
using Dapper;
using Ranalo.Models;

namespace Ranalo.DataStore
{
    public interface ILockRecoveryRepository
    {
        Task<List<LockRecovery>> GetListAsync(string status);
        Task<LockRecovery?> GetAsync(int id);
        Task<List<LockRecoveryAttempt>> GetAttemptsAsync(int recoveryId);
        Task<RecoveryDeviceLookup?> LookupDeviceAsync(long accountNo);
        Task<int?> GetOpenIdForAccountAsync(long accountNo);
        Task<int> CreateAsync(LockRecovery recovery);
        Task UpdateAsync(int id, string status, string? veritechTransId);
        Task SetNotesAsync(int id, string? notes);
        Task LogAsync(int recoveryId, string step, bool success, string? response, int? userId);
        Task SetLockGroupAsync(long accountNo, int lockGroup, bool? locked);
        Task MarkMovedToKnoxAsync(int id);
    }

    // LockRecoveries / LockRecoveryAttempts (Database/Recovery/001).
    public class LockRecoveryRepository : ILockRecoveryRepository
    {
        private readonly IDbConnection _db;

        public LockRecoveryRepository(IDbConnection db)
        {
            _db = db;
        }

        private const string SelectSql = @"
            SELECT r.Id, r.AccountNo, r.Imei, r.Make, r.Model, r.CustomerName, r.DealerName, r.OriginalLockGroup,
                   r.Reason, r.Status, r.VeritechTransId, r.Notes, r.CreatedByUserId,
                   LTRIM(RTRIM(ISNULL(u.[Name], '') + ' ' + ISNULL(u.[LastName], ''))) AS CreatedByName,
                   r.CreatedAtUtc, r.UpdatedAtUtc, r.MovedToKnoxAtUtc,
                   d.LockGroup AS CurrentLockGroup, d.Locked, d.NextLockDate, d.LastConnectedAt
            FROM LockRecoveries r
            LEFT JOIN Users u ON u.UserId = r.CreatedByUserId
            LEFT JOIN Devices d ON d.Id = r.AccountNo";

        public async Task<List<LockRecovery>> GetListAsync(string status)
        {
            var where = status switch
            {
                "moved" => "WHERE r.Status = 'MovedToKnox'",
                "ended" => "WHERE r.Status IN ('NotCatchable', 'Closed')",
                "all" => "",
                _ => "WHERE r.Status NOT IN ('MovedToKnox', 'NotCatchable', 'Closed')",
            };
            return (await _db.QueryAsync<LockRecovery>($"{SelectSql} {where} ORDER BY r.UpdatedAtUtc DESC")).ToList();
        }

        public async Task<LockRecovery?> GetAsync(int id) =>
            await _db.QueryFirstOrDefaultAsync<LockRecovery>($"{SelectSql} WHERE r.Id = @Id", new { Id = id });

        public async Task<List<LockRecoveryAttempt>> GetAttemptsAsync(int recoveryId)
        {
            const string sql = @"
                SELECT a.Id, a.RecoveryId, a.Step, a.Success, a.Response, a.AtUtc,
                       LTRIM(RTRIM(ISNULL(u.[Name], '') + ' ' + ISNULL(u.[LastName], ''))) AS ByName
                FROM LockRecoveryAttempts a
                LEFT JOIN Users u ON u.UserId = a.ByUserId
                WHERE a.RecoveryId = @RecoveryId
                ORDER BY a.AtUtc DESC, a.Id DESC";
            return (await _db.QueryAsync<LockRecoveryAttempt>(sql, new { RecoveryId = recoveryId })).ToList();
        }

        public async Task<RecoveryDeviceLookup?> LookupDeviceAsync(long accountNo)
        {
            const string sql = @"
                SELECT TOP 1
                    d.Id AS AccountNo, d.ImeiNo AS Imei, d.Make, d.Model,
                    COALESCE(ci.First_Name, d.Name, d.CustomerName) AS CustomerName,
                    dl.DealerId, dl.CompanyName AS DealerName, d.LockGroup
                FROM Devices d
                LEFT JOIN Contract_Info ci ON ci.ID = d.Id AND ci.EndDate IS NULL
                LEFT JOIN Dealers dl ON dl.DealerReference = d.DeviceGroupId
                WHERE d.Id = @AccountNo";
            return await _db.QueryFirstOrDefaultAsync<RecoveryDeviceLookup>(sql, new { AccountNo = accountNo });
        }

        public async Task<int?> GetOpenIdForAccountAsync(long accountNo) =>
            await _db.QueryFirstOrDefaultAsync<int?>(@"
                SELECT TOP 1 Id FROM LockRecoveries
                WHERE AccountNo = @AccountNo AND Status NOT IN ('MovedToKnox', 'NotCatchable', 'Closed')",
                new { AccountNo = accountNo });

        public async Task<int> CreateAsync(LockRecovery r)
        {
            const string sql = @"
                INSERT INTO LockRecoveries (AccountNo, Imei, Make, Model, CustomerName, DealerName, OriginalLockGroup, Reason, Status, Notes, CreatedByUserId)
                OUTPUT INSERTED.Id
                VALUES (@AccountNo, @Imei, @Make, @Model, @CustomerName, @DealerName, @OriginalLockGroup, @Reason, @Status, @Notes, @CreatedByUserId)";
            return await _db.ExecuteScalarAsync<int>(sql, r);
        }

        public async Task UpdateAsync(int id, string status, string? veritechTransId) =>
            await _db.ExecuteAsync(@"
                UPDATE LockRecoveries
                SET Status = @Status, VeritechTransId = COALESCE(@VeritechTransId, VeritechTransId), UpdatedAtUtc = SYSUTCDATETIME()
                WHERE Id = @Id", new { Id = id, Status = status, VeritechTransId = veritechTransId });

        public async Task SetNotesAsync(int id, string? notes) =>
            await _db.ExecuteAsync("UPDATE LockRecoveries SET Notes = @Notes, UpdatedAtUtc = SYSUTCDATETIME() WHERE Id = @Id",
                new { Id = id, Notes = notes });

        public async Task LogAsync(int recoveryId, string step, bool success, string? response, int? userId) =>
            await _db.ExecuteAsync(@"
                INSERT INTO LockRecoveryAttempts (RecoveryId, Step, Success, Response, ByUserId)
                VALUES (@RecoveryId, @Step, @Success, @Response, @UserId)",
                new { RecoveryId = recoveryId, Step = step, Success = success, Response = response, UserId = userId });

        public async Task SetLockGroupAsync(long accountNo, int lockGroup, bool? locked) =>
            await _db.ExecuteAsync(@"
                UPDATE Devices
                SET LockGroup = @LockGroup,
                    Locked = COALESCE(@Locked, Locked),
                    LockType = CASE WHEN @Locked IS NULL THEN LockType WHEN @Locked = 1 THEN 'complete' ELSE 'unlocked' END,
                    LastUpdatedDate = GETDATE()
                WHERE Id = @AccountNo", new { AccountNo = accountNo, LockGroup = lockGroup, Locked = locked });

        public async Task MarkMovedToKnoxAsync(int id) =>
            await _db.ExecuteAsync(@"
                UPDATE LockRecoveries
                SET Status = 'MovedToKnox', MovedToKnoxAtUtc = SYSUTCDATETIME(), UpdatedAtUtc = SYSUTCDATETIME()
                WHERE Id = @Id", new { Id = id });
    }
}
