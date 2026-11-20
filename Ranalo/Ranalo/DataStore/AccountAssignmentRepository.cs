using System.Data;
using Dapper;
using Ranalo.Models;

namespace Ranalo.DataStore
{
    public interface IAccountAssignmentRepository
    {
        Task<List<AssignmentAccount>> GetAccountsAsync(int? dealerId, string show, string? search);
        Task<List<AssignmentPerson>> GetAgentsAsync();
        Task<List<AssignmentPerson>> GetCollectorsAsync();

        // Sets (or, with a null userId, removes) the agent or collector on
        // these accounts' open contracts, logging each change. An agent is
        // only set on accounts of their own dealer. Returns how many changed.
        Task<int> AssignAsync(string role, IReadOnlyCollection<long> accountNos, int? userId, int changedByUserId);

        // An agent's own sales are theirs (2026-11-20): each open contract
        // whose phone an agent enrolled gets that agent, so every agent
        // report and their commission shows it. Logged with the agent as
        // ChangedBy. Never overrides: skips accounts that already have an
        // agent, or that have ever been assigned or unassigned by hand.
        Task<int> AssignAgentSalesAsync();
    }

    // Agent and collector are stored on the account's open contract
    // (Contract_Info.AssignedAgentId / DebtCollectorUserId, EndDate NULL) --
    // an ended contract keeps its agent, so past commission isn't moved.
    public class AccountAssignmentRepository : IAccountAssignmentRepository
    {
        private readonly IDbConnection _db;

        public AccountAssignmentRepository(IDbConnection db)
        {
            _db = db;
        }

        public async Task<List<AssignmentAccount>> GetAccountsAsync(int? dealerId, string show, string? search)
        {
            const string sql = @"
                SELECT TOP (1000)
                    ci.ID AS AccountNo,
                    ci.ContractID AS ContractId,
                    ci.First_Name AS CustomerName,
                    ci.StartDate,
                    dl.DealerId,
                    dl.CompanyName AS DealerName,
                    ci.AssignedAgentId AS AgentUserId,
                    au.[Name] + ' ' + ISNULL(au.[LastName], '') AS AgentName,
                    ci.DebtCollectorUserId AS CollectorUserId,
                    cu.[Name] + ' ' + ISNULL(cu.[LastName], '') AS CollectorName,
                    NULLIF(LTRIM(RTRIM(ISNULL(d.Make, '') + ' ' + ISNULL(d.Model, ''))), '') AS ProductName,
                    d.NextLockDateIsoFormat AS NextLockDateRaw
                FROM Contract_Info ci
                LEFT JOIN Devices d ON d.Id = ci.ID
                LEFT JOIN Dealers dl ON dl.DealerReference = d.DeviceGroupId
                LEFT JOIN Users au ON au.UserId = ci.AssignedAgentId
                LEFT JOIN Users cu ON cu.UserId = ci.DebtCollectorUserId
                WHERE ci.StartDate IS NOT NULL AND ci.EndDate IS NULL
                  AND (@DealerId IS NULL OR dl.DealerId = @DealerId)
                  AND (@Show = 'all'
                       OR (@Show = 'noagent' AND ci.AssignedAgentId IS NULL)
                       OR (@Show = 'nocollector' AND ci.DebtCollectorUserId IS NULL))
                  AND (@Search IS NULL
                       OR CAST(ci.ID AS NVARCHAR(30)) = @Search
                       OR CAST(ci.ContractID AS NVARCHAR(30)) = @Search
                       OR ci.First_Name LIKE '%' + @Search + '%'
                       OR au.[Name] + ' ' + ISNULL(au.[LastName], '') LIKE '%' + @Search + '%'
                       OR cu.[Name] + ' ' + ISNULL(cu.[LastName], '') LIKE '%' + @Search + '%')
                ORDER BY dl.CompanyName, ci.StartDate DESC
                OPTION (RECOMPILE)";

            var rows = await _db.QueryAsync<AssignmentAccount>(sql, new
            {
                DealerId = dealerId,
                Show = show,
                Search = string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
            }, commandTimeout: 90);
            return rows.ToList();
        }

        public async Task<List<AssignmentPerson>> GetAgentsAsync()
        {
            const string sql = @"
                SELECT u.UserId, u.[Name] + ' ' + ISNULL(u.[LastName], '') AS Name, u.DealerId, dl.CompanyName AS DealerName
                FROM Users u
                LEFT JOIN Dealers dl ON dl.DealerId = u.DealerId
                WHERE u.RoleId = 7 AND ISNULL(u.IsActive, 1) = 1
                ORDER BY dl.CompanyName, u.[Name]";
            return (await _db.QueryAsync<AssignmentPerson>(sql)).ToList();
        }

        public async Task<List<AssignmentPerson>> GetCollectorsAsync()
        {
            const string sql = @"
                SELECT u.UserId, u.[Name] + ' ' + ISNULL(u.[LastName], '') AS Name, u.DealerId, NULL AS DealerName
                FROM Users u
                WHERE u.RoleId = 6 AND ISNULL(u.IsActive, 1) = 1
                ORDER BY u.[Name]";
            return (await _db.QueryAsync<AssignmentPerson>(sql)).ToList();
        }

        public async Task<int> AssignAsync(string role, IReadOnlyCollection<long> accountNos, int? userId, int changedByUserId)
        {
            if (_db.State != ConnectionState.Open)
            {
                _db.Open();
            }

            using var transaction = _db.BeginTransaction();
            var isAgent = role == AssignmentRole.Agent;

            // An agent may only take accounts of their own dealer.
            int? agentDealerId = null;
            if (isAgent && userId.HasValue)
            {
                agentDealerId = await _db.QuerySingleOrDefaultAsync<int?>(
                    "SELECT DealerId FROM Users WHERE UserId = @UserId AND RoleId = 7", new { UserId = userId }, transaction);
                if (agentDealerId is null or <= 0)
                {
                    throw new InvalidOperationException("That agent isn't linked to a dealer. Set their dealer on Users > Edit User first.");
                }
            }

            var column = isAgent ? "AssignedAgentId" : "DebtCollectorUserId";

            // Open contracts of these accounts whose value actually changes
            // (and, for an agent, that belong to the agent's dealer).
            var targetSql = $@"
                FROM Contract_Info ci
                LEFT JOIN Devices d ON d.Id = ci.ID
                LEFT JOIN Dealers dl ON dl.DealerReference = d.DeviceGroupId
                WHERE ci.ID IN @Ids AND ci.StartDate IS NOT NULL AND ci.EndDate IS NULL
                  AND (@AgentDealerId IS NULL OR dl.DealerId = @AgentDealerId)
                  AND (ISNULL(ci.{column}, -1) <> ISNULL(@UserId, -1))";

            var logSql = $@"
                INSERT INTO AccountAssignmentLog (AccountNo, ContractId, AssignmentRole, OldUserId, NewUserId, ChangedByUserId)
                SELECT ci.ID, ci.ContractID, @Role, ci.{column}, @UserId, @ChangedBy
                {targetSql}";

            var updateSql = $@"
                UPDATE ci SET ci.{column} = @UserId
                {targetSql}";

            var changed = 0;
            foreach (var chunk in accountNos.Distinct().Chunk(1000))
            {
                var args = new { Ids = chunk, UserId = userId, AgentDealerId = agentDealerId, Role = role, ChangedBy = changedByUserId };
                await _db.ExecuteAsync(logSql, args, transaction);
                changed += await _db.ExecuteAsync(updateSql, args, transaction);
            }

            transaction.Commit();
            return changed;
        }

        public async Task<int> AssignAgentSalesAsync()
        {
            // The agent must still work for the dealer the phone was sold
            // for, and the account must belong to that dealer.
            const string targets = @"
                SELECT DISTINCT ci.ID AS AccountNo, ci.ContractID, e.EnrolledByUserId AS AgentUserId
                FROM Enrolments e
                JOIN Contract_Info ci ON ci.ID = e.AccountId AND ci.StartDate IS NOT NULL AND ci.EndDate IS NULL
                JOIN Users u ON u.UserId = e.EnrolledByUserId AND u.RoleId = 7 AND u.DealerId = e.DealerId
                JOIN Devices d ON d.Id = ci.ID
                JOIN Dealers dl ON dl.DealerReference = d.DeviceGroupId AND dl.DealerId = e.DealerId
                WHERE e.EnrolledByRole = 7 AND e.AccountId > 0
                  AND ci.AssignedAgentId IS NULL
                  AND NOT EXISTS (SELECT 1 FROM AccountAssignmentLog l
                                  WHERE l.AccountNo = ci.ID AND l.AssignmentRole = 'Agent')";

            const string sql = @"
                DECLARE @Sales TABLE (AccountNo BIGINT, ContractId BIGINT, AgentUserId INT);
                INSERT INTO @Sales " + targets + @";

                INSERT INTO AccountAssignmentLog (AccountNo, ContractId, AssignmentRole, OldUserId, NewUserId, ChangedByUserId)
                SELECT DISTINCT s.AccountNo, s.ContractId, 'Agent', NULL, s.AgentUserId, s.AgentUserId FROM @Sales s;

                UPDATE ci SET ci.AssignedAgentId = s.AgentUserId
                FROM Contract_Info ci
                JOIN @Sales s ON s.AccountNo = ci.ID AND s.ContractId = ci.ContractID
                WHERE ci.AssignedAgentId IS NULL AND ci.EndDate IS NULL;
                SELECT @@ROWCOUNT;";

            if (_db.State != ConnectionState.Open)
            {
                _db.Open();
            }
            using var transaction = _db.BeginTransaction();
            var changed = await _db.ExecuteScalarAsync<int>(sql, transaction: transaction);
            transaction.Commit();
            return changed;
        }
    }
}
