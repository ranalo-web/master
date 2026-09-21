namespace Ranalo.Models
{
    // Identifies which rollup row(s) a dashboard reads.
    // DealerId = null, AgentId = null -> company-wide (Admin dashboard).
    // DealerId = X,    AgentId = null -> one dealer (Dealer dashboard).
    // DealerId = X,    AgentId = Y    -> one agent within a dealer (future Agent dashboard).
    public record DashboardScope(int? DealerId = null, int? AgentId = null)
    {
        public static readonly DashboardScope Admin = new();

        public static DashboardScope ForDealer(int dealerId) => new(dealerId);

        public static DashboardScope ForAgent(int dealerId, int agentId) => new(dealerId, agentId);
    }
}
