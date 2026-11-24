using Ranalo.DataStore.DataModels;
using Ranalo.Services;

namespace Ranalo.Models
{
    // The Agents page (/agent-list): every agent, how they're selling and
    // what they're owed, in Active and Suspended tabs.
    public class AgentListViewModel
    {
        public List<AgentListRow> Rows { get; set; } = new();
        public bool Suspended { get; set; }
        public int ActiveCount { get; set; }
        public int SuspendedCount { get; set; }
        public int? DealerFilter { get; set; }
        public string? Search { get; set; }
        public List<(int Id, string Name)> Dealers { get; set; } = new();
        public bool ShowDealerFilter { get; set; }
    }

    public class AgentListRow
    {
        public User User { get; set; } = new();
        public string DealerName { get; set; } = "";
        public SalesTeamStats Stats { get; set; } = new();
    }
}
