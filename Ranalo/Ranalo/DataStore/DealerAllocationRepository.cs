using System.Data;
using Dapper;
using Ranalo.Models;

namespace Ranalo.DataStore
{
    public interface IDealerAllocationRepository
    {
        Task<List<DealerAllocationAccount>> GetAccountsAsync(bool unassignedOnly, string? search);
        Task<(int Unassigned, int NoDevice)> GetUnassignedCountsAsync();
        Task<List<DealerOption>> GetDealersAsync();
        Task<List<DealerAllocationLogEntry>> GetRecentChangesAsync(int take);

        // Sets these accounts' device group to the dealer's, logging each
        // change. Accounts with no device record are skipped. Returns how
        // many were moved.
        Task<int> AssignAsync(IReadOnlyCollection<long> accountNos, int dealerId, int userId, string? note);
    }

    // An account's dealer is its device's group: Devices.DeviceGroupId =
    // Dealers.DealerReference -- the join every dashboard, report and
    // commission figure uses, so changing it moves the account everywhere.
    // The device sync only inserts new devices and never updates
    // DeviceGroupId, so a change made here sticks.
    public class DealerAllocationRepository : IDealerAllocationRepository
    {
        private readonly IDbConnection _db;

        public DealerAllocationRepository(IDbConnection db)
        {
            _db = db;
        }

        public async Task<List<DealerAllocationAccount>> GetAccountsAsync(bool unassignedOnly, string? search)
        {
            const string sql = @"
                ;WITH Paid AS (
                    SELECT COALESCE(op.AccountNoBigint, kp.AccountNoBigint) AS AccountNo, SUM(kp.AmountValue) AS TotalPaid
                    FROM KosePayments kp
                    LEFT JOIN OrphanedPayments op ON op.MpesaCode = kp.MpesaCode
                    GROUP BY COALESCE(op.AccountNoBigint, kp.AccountNoBigint)
                )
                SELECT TOP (@Take)
                    ci.ID AS AccountNo,
                    ci.ContractID AS ContractId,
                    ci.First_Name AS CustomerName,
                    ci.StartDate,
                    CAST(CASE WHEN d.Id IS NULL THEN 0 ELSE 1 END AS BIT) AS HasDevice,
                    NULLIF(LTRIM(RTRIM(ISNULL(d.Make, '') + ' ' + ISNULL(d.Model, ''))), '') AS ProductName,
                    CAST(d.ImeiNo AS NVARCHAR(50)) AS Imei,
                    d.DeviceGroupId,
                    dl.DealerId,
                    dl.CompanyName AS DealerName,
                    au.[Name] + ' ' + au.[LastName] AS AgentName,
                    ISNULL(p.TotalPaid, 0) AS TotalPaid
                FROM Contract_Info ci
                LEFT JOIN Devices d ON d.Id = ci.ID
                LEFT JOIN Dealers dl ON dl.DealerReference = d.DeviceGroupId
                LEFT JOIN Users au ON au.UserId = ci.AssignedAgentId
                LEFT JOIN Paid p ON p.AccountNo = ci.ID
                WHERE ci.StartDate IS NOT NULL
                  AND (@UnassignedOnly = 0 OR dl.DealerId IS NULL)
                  AND (@Search IS NULL
                       OR CAST(ci.ID AS NVARCHAR(30)) = @Search
                       OR CAST(ci.ContractID AS NVARCHAR(30)) = @Search
                       OR CAST(d.ImeiNo AS NVARCHAR(50)) = @Search
                       OR ci.First_Name LIKE '%' + @Search + '%'
                       OR dl.CompanyName LIKE '%' + @Search + '%')
                ORDER BY ci.StartDate DESC";

            var term = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
            var rows = await _db.QueryAsync<DealerAllocationAccount>(sql, new
            {
                Take = unassignedOnly || term != null ? 1000 : 300,
                UnassignedOnly = unassignedOnly ? 1 : 0,
                Search = term,
            }, commandTimeout: 90);
            return rows.ToList();
        }

        public async Task<(int Unassigned, int NoDevice)> GetUnassignedCountsAsync()
        {
            const string sql = @"
                SELECT
                    SUM(CASE WHEN dl.DealerId IS NULL THEN 1 ELSE 0 END),
                    SUM(CASE WHEN d.Id IS NULL THEN 1 ELSE 0 END)
                FROM Contract_Info ci
                LEFT JOIN Devices d ON d.Id = ci.ID
                LEFT JOIN Dealers dl ON dl.DealerReference = d.DeviceGroupId
                WHERE ci.StartDate IS NOT NULL";

            var (unassigned, noDevice) = await _db.QuerySingleAsync<(int?, int?)>(sql);
            return (unassigned ?? 0, noDevice ?? 0);
        }

        public async Task<List<DealerOption>> GetDealersAsync()
        {
            const string sql = @"
                SELECT DealerId, ISNULL(CompanyName, 'Dealer ' + CAST(DealerId AS NVARCHAR(10))) AS CompanyName,
                       TRY_CAST(DealerReference AS BIGINT) AS GroupId
                FROM Dealers
                ORDER BY CompanyName";

            return (await _db.QueryAsync<DealerOption>(sql)).ToList();
        }

        public async Task<List<DealerAllocationLogEntry>> GetRecentChangesAsync(int take)
        {
            const string sql = @"
                SELECT TOP (@Take)
                    l.AccountNo, l.OldDeviceGroupId, od.CompanyName AS OldDealerName,
                    ISNULL(nd.CompanyName, 'Dealer ' + CAST(l.NewDealerId AS NVARCHAR(10))) AS NewDealerName,
                    l.Note, u.[Name] + ' ' + u.[LastName] AS ChangedByName, l.ChangedAtUtc
                FROM DealerAllocationLog l
                LEFT JOIN Dealers od ON od.DealerReference = l.OldDeviceGroupId
                LEFT JOIN Dealers nd ON nd.DealerId = l.NewDealerId
                LEFT JOIN Users u ON u.UserId = l.ChangedByUserId
                ORDER BY l.Id DESC";

            return (await _db.QueryAsync<DealerAllocationLogEntry>(sql, new { Take = take })).ToList();
        }

        public async Task<int> AssignAsync(IReadOnlyCollection<long> accountNos, int dealerId, int userId, string? note)
        {
            if (_db.State != ConnectionState.Open)
            {
                _db.Open();
            }

            using var transaction = _db.BeginTransaction();

            var groupId = await _db.QuerySingleOrDefaultAsync<long?>(
                "SELECT TRY_CAST(DealerReference AS BIGINT) FROM Dealers WHERE DealerId = @DealerId",
                new { DealerId = dealerId }, transaction);
            if (groupId == null)
            {
                throw new InvalidOperationException($"Dealer {dealerId} has no numeric DealerReference to use as a device group.");
            }

            // Log first (captures the old group), then move. Accounts already
            // in this group or with no device record are left alone.
            const string logSql = @"
                INSERT INTO DealerAllocationLog (AccountNo, OldDeviceGroupId, NewDeviceGroupId, NewDealerId, Note, ChangedByUserId)
                SELECT d.Id, d.DeviceGroupId, @GroupId, @DealerId, @Note, @UserId
                FROM Devices d
                WHERE d.Id IN @Ids AND (d.DeviceGroupId IS NULL OR d.DeviceGroupId <> @GroupId)";

            const string moveSql = @"
                UPDATE Devices SET DeviceGroupId = @GroupId
                WHERE Id IN @Ids AND (DeviceGroupId IS NULL OR DeviceGroupId <> @GroupId)";

            var moved = 0;
            foreach (var chunk in accountNos.Distinct().Chunk(1000))
            {
                var args = new { Ids = chunk, GroupId = groupId.Value, DealerId = dealerId, Note = note, UserId = userId };
                await _db.ExecuteAsync(logSql, args, transaction);
                moved += await _db.ExecuteAsync(moveSql, args, transaction);
            }

            transaction.Commit();
            return moved;
        }
    }
}
