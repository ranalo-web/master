namespace Ranalo.Models.Reports
{
    public class AgentsTotalSummaryReport
    {
        public int? AgentId { get; set; }

        public int TotalContracts { get; set; }

        public decimal TotalDeposits { get; set; }

        public decimal TotalAgentCommission { get; set; }
    }
}
