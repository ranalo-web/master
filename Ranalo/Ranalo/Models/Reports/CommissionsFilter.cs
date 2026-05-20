namespace Ranalo.Models.Reports
{
    public class CommissionsFilter
    {
        public int? DealerId { get; set; }
        public int? AgentId { get; set; }

        public bool? DealerEligible { get; set; }

        public int PageNumber { get; set; } = 1;

        public int PageSize { get; set; } = 1000;
    }
}
