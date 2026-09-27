namespace Ranalo.Models.Reports
{
    public class CommissionsFilter
    {
        // Dealers.DealerId; null for every dealer.
        public int? DealerId { get; set; }

        // Assigned agent's user id; null for every agent.
        public int? AgentId { get; set; }

        public bool? DealerEligible { get; set; }

        public int PageNumber { get; set; } = 1;

        public int PageSize { get; set; } = 1000;
    }
}
