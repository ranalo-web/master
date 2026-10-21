using System.Data;
using Dapper;
using Ranalo.DataStore.DataModels;

namespace Ranalo.DataStore
{
    public class PasswordResetRequest
    {
        public int UserId { get; set; }
        public string? Name { get; set; }
        public string? LastName { get; set; }
        public string? Email { get; set; }
        public UserRole RoleId { get; set; }
        public int DealerId { get; set; }
        public DateTime RequestedAtUtc { get; set; }
    }

    public interface IPasswordResetRepository
    {
        Task RequestAsync(int userId);
        Task<List<PasswordResetRequest>> GetOpenAsync();
        Task<DateTime?> GetOpenRequestedAtAsync(int userId);
        Task ResolveAsync(int userId, int resolvedByUserId);
    }

    // "Forgot password?" requests (Database/Security/002). Every query
    // checks the table exists, so nothing breaks before the migration runs.
    public class PasswordResetRepository : IPasswordResetRepository
    {
        private const string TableExists = "OBJECT_ID('dbo.PasswordResetRequests') IS NOT NULL";
        private readonly IDbConnection _db;

        public PasswordResetRepository(IDbConnection db)
        {
            _db = db;
        }

        // One open request per person, however often they ask.
        public async Task RequestAsync(int userId)
        {
            var sql = $@"
                IF {TableExists}
                   AND NOT EXISTS (SELECT 1 FROM PasswordResetRequests WHERE UserId = @UserId AND ResolvedAtUtc IS NULL)
                    INSERT INTO PasswordResetRequests (UserId) VALUES (@UserId);";
            await _db.ExecuteAsync(sql, new { UserId = userId });
        }

        public async Task<List<PasswordResetRequest>> GetOpenAsync()
        {
            var sql = $@"
                IF {TableExists}
                    SELECT r.UserId, u.Name, u.LastName, u.Email, u.RoleId, u.DealerId, r.RequestedAtUtc
                    FROM PasswordResetRequests r
                    INNER JOIN Users u ON u.UserId = r.UserId
                    WHERE r.ResolvedAtUtc IS NULL
                    ORDER BY r.RequestedAtUtc;";
            return (await _db.QueryAsync<PasswordResetRequest>(sql)).ToList();
        }

        public async Task<DateTime?> GetOpenRequestedAtAsync(int userId)
        {
            var sql = $@"
                IF {TableExists}
                    SELECT MIN(RequestedAtUtc) FROM PasswordResetRequests WHERE UserId = @UserId AND ResolvedAtUtc IS NULL
                ELSE
                    SELECT CAST(NULL AS DATETIME2);";
            return await _db.QueryFirstOrDefaultAsync<DateTime?>(sql, new { UserId = userId });
        }

        public async Task ResolveAsync(int userId, int resolvedByUserId)
        {
            var sql = $@"
                IF {TableExists}
                    UPDATE PasswordResetRequests
                    SET ResolvedAtUtc = SYSUTCDATETIME(), ResolvedByUserId = @By
                    WHERE UserId = @UserId AND ResolvedAtUtc IS NULL;";
            await _db.ExecuteAsync(sql, new { UserId = userId, By = resolvedByUserId });
        }
    }
}
