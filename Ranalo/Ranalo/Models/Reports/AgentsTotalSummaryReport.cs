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

        // Suspension and what can be paid now (CommissionPayees), and the
        // upfront / bonus still due before arrears.
        public decimal DefaultRatePct { get; set; }
        public bool IsSuspended { get; set; }
        public decimal Payable { get; set; }
        public decimal UpfrontDue { get; set; }
        public decimal BonusDue { get; set; }
    }
}
