using System.Data;
using System.Data.SqlClient;
using Dapper;
using Ranalo.Models;

namespace Ranalo.DataStore
{
    // Fully-paid device removal queue (Database/DeviceLock/001). Every state
    // change is a single conditional UPDATE, so two admins (or a double
    // click) can never both act on the same task: whoever's UPDATE matches
    // first wins, the other gets false.
    public interface IDeviceRemovalTaskRepository
    {
        // false = the account already has a live task (or was already removed).
        Task<bool> EnqueueAsync(DeviceRemovalTask task);

        Task<DeviceRemovalTask?> GetAsync(int id);
        Task<(List<DeviceRemovalTask> Items, int TotalCount)> ListAsync(string status, int page, int pageSize);
        Task<int> CountAwaitingDecisionAsync();

        // Pending/Failed -> Processing. A Processing task left behind by a
        // crash is claimable again after StaleProcessingMinutes.
        Task<bool> TryClaimAsync(int id, int userId, DateTime nowUtc);
        Task MarkCompletedAsync(int id, string? response, DateTime nowUtc);
        Task MarkFailedAsync(int id, string? response, DateTime nowUtc);

        // Pending/Failed -> Rejected.
        Task<bool> RejectAsync(int id, int userId, string? note, DateTime nowUtc);

        // Devices already at the fully-paid lock date (31/12/9999, or
        // 31/12/2099 for Transsion) with no live task.
        Task<List<DeviceRemovalTask>> FindFullyPaidWithoutTaskAsync();
    }

    public class DeviceRemovalTaskRepository : IDeviceRemovalTaskRepository
    {
        public const int StaleProcessingMinutes = 15;

        private readonly IDbConnection _db;

        public DeviceRemovalTaskRepository(IDbConnection db)
        {
            _db = db;
        }

        public async Task<bool> EnqueueAsync(DeviceRemovalTask task)
        {
            const string sql = @"
                INSERT INTO dbo.DeviceRemovalTasks
                    (AccountId, Imei, CustomerName, LockGroup, Provider, Reason, Status, CreatedAtUtc)
                SELECT @AccountId, @Imei, @CustomerName, @LockGroup, @Provider, @Reason, 'Pending', SYSUTCDATETIME()
                WHERE NOT EXISTS (
                    SELECT 1 FROM dbo.DeviceRemovalTasks WITH (UPDLOCK, HOLDLOCK)
                    WHERE AccountId = @AccountId
                      AND Status IN ('Pending', 'Processing', 'Failed', 'Completed'));";

            try
            {
                return await _db.ExecuteAsync(sql, task) == 1;
            }
            catch (SqlException ex) when (ex.Number is 2627 or 2601)
            {
                // Unique index backstop: queued concurrently.
                return false;
            }
        }

        public async Task<DeviceRemovalTask?> GetAsync(int id)
        {
            return await _db.QueryFirstOrDefaultAsync<DeviceRemovalTask>(
                "SELECT * FROM dbo.DeviceRemovalTasks WHERE Id = @Id", new { Id = id });
        }

        public async Task<(List<DeviceRemovalTask> Items, int TotalCount)> ListAsync(string status, int page, int pageSize)
        {
            if (page <= 0) page = 1;
            if (pageSize <= 0) pageSize = 50;

            // "Awaiting" = what an admin still has to act on.
            const string filter = @"
                (@Status = 'Awaiting' AND Status IN ('Pending', 'Failed', 'Processing'))
                OR (@Status = 'All')
                OR (Status = @Status)";

            var sql = $@"
                SELECT COUNT(*) FROM dbo.DeviceRemovalTasks WHERE {filter};

                SELECT * FROM dbo.DeviceRemovalTasks
                WHERE {filter}
                ORDER BY CreatedAtUtc, Id
                OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY;";

            using var multi = await _db.QueryMultipleAsync(sql,
                new { Status = status, Skip = (page - 1) * pageSize, Take = pageSize });

            var total = await multi.ReadSingleAsync<int>();
            var items = (await multi.ReadAsync<DeviceRemovalTask>()).ToList();
            return (items, total);
        }

        public async Task<int> CountAwaitingDecisionAsync()
        {
            return await _db.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM dbo.DeviceRemovalTasks WHERE Status IN ('Pending', 'Failed')");
        }

        public async Task<bool> TryClaimAsync(int id, int userId, DateTime nowUtc)
        {
            const string sql = @"
                UPDATE dbo.DeviceRemovalTasks
                SET Status = 'Processing',
                    DecidedByUserId = @UserId,
                    DecidedAtUtc = @Now,
                    AttemptCount = AttemptCount + 1,
                    LastAttemptAtUtc = @Now
                WHERE Id = @Id
                  AND (Status IN ('Pending', 'Failed')
                       OR (Status = 'Processing' AND LastAttemptAtUtc < DATEADD(MINUTE, -@StaleMinutes, @Now)));";

            return await _db.ExecuteAsync(sql,
                new { Id = id, UserId = userId, Now = nowUtc, StaleMinutes = StaleProcessingMinutes }) == 1;
        }

        public async Task MarkCompletedAsync(int id, string? response, DateTime nowUtc)
        {
            await _db.ExecuteAsync(@"
                UPDATE dbo.DeviceRemovalTasks
                SET Status = 'Completed', LastResponse = @Response, CompletedAtUtc = @Now
                WHERE Id = @Id AND Status = 'Processing'",
                new { Id = id, Response = response, Now = nowUtc });
        }

        public async Task MarkFailedAsync(int id, string? response, DateTime nowUtc)
        {
            await _db.ExecuteAsync(@"
                UPDATE dbo.DeviceRemovalTasks
                SET Status = 'Failed', LastResponse = @Response
                WHERE Id = @Id AND Status = 'Processing'",
                new { Id = id, Response = response, Now = nowUtc });
        }

        public async Task<bool> RejectAsync(int id, int userId, string? note, DateTime nowUtc)
        {
            return await _db.ExecuteAsync(@"
                UPDATE dbo.DeviceRemovalTasks
                SET Status = 'Rejected', DecidedByUserId = @UserId, DecidedAtUtc = @Now, DecisionNote = @Note
                WHERE Id = @Id AND Status IN ('Pending', 'Failed')",
                new { Id = id, UserId = userId, Note = note, Now = nowUtc }) == 1;
        }

        public async Task<List<DeviceRemovalTask>> FindFullyPaidWithoutTaskAsync()
        {
            // NextLockDateIsoFormat is dd/MM/yyyy'T'HH:mm:ss (TimestampHelper).
            const string sql = @"
                SELECT d.Id AS AccountId,
                       d.ImeiNo AS Imei,
                       COALESCE(ci.First_Name, d.Name) AS CustomerName,
                       d.LockGroup
                FROM dbo.Devices d
                LEFT JOIN dbo.Contract_Info ci ON ci.ID = d.Id AND ci.EndDate IS NULL
                WHERE d.Status = 'enrolled'
                  AND (d.NextLockDateIsoFormat LIKE '%/9999T%'      -- fully paid
                       OR d.NextLockDateIsoFormat LIKE '31/12/2099T%')  -- Transsion fallback
                  AND NOT EXISTS (
                      SELECT 1 FROM dbo.DeviceRemovalTasks t
                      WHERE t.AccountId = d.Id
                        AND t.Status IN ('Pending', 'Processing', 'Failed', 'Completed'))";

            return (await _db.QueryAsync<DeviceRemovalTask>(sql)).ToList();
        }
    }
}
