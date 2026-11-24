using Ranalo.DataStore;
using Ranalo.DataStore.DataModels;
using Ranalo.Models;

namespace Ranalo.Services
{
    public interface ISalesTeamService
    {
        // dealerScope: a dealer sees only their own agents (null = everyone).
        Task<AgentListViewModel> GetAgentsAsync(int? dealerScope, int? dealerFilter, bool suspended, string? search);

        // Each dealer's figures, and when their dealer login was last seen.
        Task<(Dictionary<int, SalesTeamStats> Stats, Dictionary<int, DateTime?> LastSeen)> GetDealerStatsAsync();
    }

    public class SalesTeamService : ISalesTeamService
    {
        private readonly IDashboardReportRepository _reports;
        private readonly IUserService _users;

        public SalesTeamService(IDashboardReportRepository reports, IUserService users)
        {
            _reports = reports;
            _users = users;
        }

        private static DateTime KenyaToday => DateTime.UtcNow.AddHours(3).Date;

        public async Task<AgentListViewModel> GetAgentsAsync(int? dealerScope, int? dealerFilter, bool suspended, string? search)
        {
            var accounts = await _reports.GetCommissionAccountsAsync(null);
            var stats = SalesTeamRules.ByAgent(accounts, KenyaToday);
            var dealers = await _users.GetAllDealers() ?? new List<Ranalo.DataStore.Dealer>();
            var dealerNames = dealers.GroupBy(d => d.DealerId).ToDictionary(g => g.Key, g => g.First().CompanyName ?? $"Dealer {g.Key}");

            var agents = (await _users.GetAllUsersAsync())
                .Where(u => u.RoleId == UserRole.Agent && u.Status != UserStatus.Deleted)
                .Where(u => dealerScope == null || u.DealerId == dealerScope)
                .ToList();

            var term = search?.Trim();
            var inScope = agents
                .Where(u => dealerFilter == null || u.DealerId == dealerFilter)
                .Where(u => string.IsNullOrEmpty(term)
                    || $"{u.Name} {u.LastName}".Contains(term, StringComparison.OrdinalIgnoreCase)
                    || (u.Email ?? "").Contains(term, StringComparison.OrdinalIgnoreCase)
                    || (u.Phone ?? "").Contains(term, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var rows = inScope
                .Where(u => (u.Status == UserStatus.Suspended) == suspended)
                .Select(u => new AgentListRow
                {
                    User = u,
                    DealerName = u.DealerId == 0 ? "Ranalo (staff)" : dealerNames.TryGetValue(u.DealerId, out var n) ? n : $"Dealer {u.DealerId}",
                    Stats = stats.TryGetValue(u.UserId, out var s) ? s : new SalesTeamStats(),
                })
                .OrderByDescending(r => r.User.LastSeen ?? r.User.LastLogin)
                .ThenBy(r => r.User.Name)
                .ToList();

            return new AgentListViewModel
            {
                Rows = rows,
                Suspended = suspended,
                ActiveCount = inScope.Count(u => u.Status != UserStatus.Suspended),
                SuspendedCount = inScope.Count(u => u.Status == UserStatus.Suspended),
                DealerFilter = dealerFilter,
                Search = term,
                // Only dealers that have agents, open ones by name.
                Dealers = agents.Select(u => u.DealerId).Distinct()
                    .Select(id => (Id: id, Name: id == 0 ? "Ranalo (staff)" : dealerNames.TryGetValue(id, out var n) ? n : $"Dealer {id}"))
                    .OrderBy(d => d.Name).ToList(),
                ShowDealerFilter = dealerScope == null,
            };
        }

        public async Task<(Dictionary<int, SalesTeamStats> Stats, Dictionary<int, DateTime?> LastSeen)> GetDealerStatsAsync()
        {
            var accounts = await _reports.GetCommissionAccountsAsync(null);
            var stats = SalesTeamRules.ByDealer(accounts, KenyaToday);
            var lastSeen = (await _users.GetAllUsersAsync())
                .Where(u => u.RoleId == UserRole.Dealer && u.Status != UserStatus.Deleted)
                .GroupBy(u => u.DealerId)
                .ToDictionary(g => g.Key, g => g.Max(u => u.LastSeen ?? u.LastLogin));
            return (stats, lastSeen);
        }
    }
}
