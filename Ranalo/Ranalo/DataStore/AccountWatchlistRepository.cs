using System.Data;
using System.Data.SqlClient;
using Dapper;
using Ranalo.DataStore.DataModels;
using Ranalo.Models;

namespace Ranalo.DataStore
{
    public class AccountWatchlistRepository : IAccountWatchlistRepository
    {
        private readonly IDbConnection _db;

        public AccountWatchlistRepository(IDbConnection db)
        {
            _db = db;
        }

        public async Task<List<AccountWatchlistListItem>> GetActiveWatchlistAsync(int? dealerId, int? agentUserId)
        {
            const string sql = @"
                SELECT
                    aw.Id,
                    aw.AccountId,
                    ci.First_Name AS CustomerName,
                    dl.DealerId,
                    dl.CompanyName AS DealerName,
                    ci.AssignedAgentId,
                    agentUser.[Name] + ' ' + agentUser.[LastName] AS AgentName,
                    aw.AddedByUserId,
                    addedByUser.[Name] + ' ' + addedByUser.[LastName] AS AddedByName,
                    aw.AddedByRole,
                    aw.AddedAtUtc
                FROM AccountWatchlist aw
                INNER JOIN Contract_Info ci ON ci.ID = aw.AccountId
                INNER JOIN Devices d ON d.Id = ci.ID
                INNER JOIN Dealers dl ON dl.DealerReference = d.DeviceGroupId
                LEFT JOIN Users agentUser ON agentUser.UserId = ci.AssignedAgentId
                INNER JOIN Users addedByUser ON addedByUser.UserId = aw.AddedByUserId
                WHERE aw.RemovedAtUtc IS NULL
                  AND (@DealerId IS NULL OR dl.DealerId = @DealerId)
                  AND (@AgentUserId IS NULL OR ci.AssignedAgentId = @AgentUserId)
                ORDER BY aw.AddedAtUtc DESC";

            var rows = await _db.QueryAsync<AccountWatchlistListItem>(sql, new { DealerId = dealerId, AgentUserId = agentUserId });
            return rows.ToList();
        }

        public async Task<(int AddedByUserId, UserRole AddedByRole)?> GetActiveEntryOwnerAsync(int watchlistId)
        {
            const string sql = @"
                SELECT AddedByUserId, AddedByRole
                FROM AccountWatchlist
                WHERE Id = @WatchlistId AND RemovedAtUtc IS NULL";

            var row = await _db.QueryFirstOrDefaultAsync<(int AddedByUserId, string AddedByRole)?>(sql, new { WatchlistId = watchlistId });
            if (row == null)
            {
                return null;
            }

            return (row.Value.AddedByUserId, Enum.Parse<UserRole>(row.Value.AddedByRole));
        }

        public async Task<bool> AddAsync(long accountId, int addedByUserId, UserRole addedByRole)
        {
            const string sql = @"
                INSERT INTO AccountWatchlist (AccountId, AddedByUserId, AddedByRole)
                VALUES (@AccountId, @AddedByUserId, @AddedByRole)";

            try
            {
                await _db.ExecuteAsync(sql, new { AccountId = accountId, AddedByUserId = addedByUserId, AddedByRole = addedByRole.ToString() });
                return true;
            }
            catch (SqlException ex) when (IsUniqueConstraintViolation(ex))
            {
                // Account already has an active watchlist entry.
                return false;
            }
        }

        public async Task<bool> RemoveAsync(int watchlistId, int removedByUserId)
        {
            const string sql = @"
                UPDATE AccountWatchlist
                SET RemovedByUserId = @RemovedByUserId, RemovedAtUtc = SYSUTCDATETIME()
                WHERE Id = @WatchlistId AND RemovedAtUtc IS NULL";

            var rowsAffected = await _db.ExecuteAsync(sql, new { WatchlistId = watchlistId, RemovedByUserId = removedByUserId });
            return rowsAffected > 0;
        }

        // SQL Server error 2601/2627 = unique index/constraint violation.
        private static bool IsUniqueConstraintViolation(SqlException ex) => ex.Number is 2601 or 2627;
    }
}
