namespace Ranalo.Models.Reports
{
    // Per-agent totals by assigned agent, pooled like the Agent Commissions card.
    public class AgentsTotalSummaryReport
    {
        public int? AgentId { get; set; }
        public string AgentName { get; set; } = "";
        public string DealerName { get; set; } = "";
        public int TotalContracts { get; set; }
        public decimal TotalDeposits { get; set; }
        public decimal TotalAgentCommission { get; set; }
        public decimal Withheld { get; set; }
        public decimal Paid { get; set; }
        public decimal Owed { get; set; }
    }
}
