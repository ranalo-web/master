namespace Ranalo.Models
{
    public static class CollectionCaseStatus
    {
        public const string Open = "Open";
        public const string Returned = "Returned";
        public const string Superseded = "Superseded";
    }

    // An open contract's standing today: what the pool and handover use.
    public class CollectionContractStanding
    {
        public long AccountNo { get; set; }
        public long ContractId { get; set; }
        public string CustomerName { get; set; } = "";
        public int? DealerId { get; set; }
        public string? DealerName { get; set; }
        public int? AgentUserId { get; set; }
        public string? AgentName { get; set; }
        public string? ProductName { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal Shortfall { get; set; }
        public DateTime? LastPaymentDate { get; set; }
        public int DaysSinceLastPayment { get; set; }

        // Contract_Info.DebtCollectorUserId (legacy screens read it).
        public int? LegacyCollectorUserId { get; set; }

        // Set when the device already has an open case (e.g. on the contract
        // before a resale).
        public int? OpenCaseId { get; set; }
    }

    // One spell of one contract in collections, with what was recovered.
    public class CollectionCaseRow
    {
        public int CaseId { get; set; }
        public long AccountNo { get; set; }
        public long ContractId { get; set; }
        public string CustomerName { get; set; } = "";
        public int? DealerId { get; set; }
        public string? DealerName { get; set; }
        public int? AgentUserId { get; set; }
        public string? AgentName { get; set; }
        public string? ProductName { get; set; }
        public DateTime HandoverAt { get; set; }
        public decimal ShortfallAtHandover { get; set; }
        public decimal FrozenDeduction { get; set; }
        public string Status { get; set; } = CollectionCaseStatus.Open;
        public DateTime? ClosedAt { get; set; }
        public bool IsGoLiveBackfill { get; set; }

        // The handed-over contract has ended: the device was repossessed and
        // the collector now earns on the new customer's payments.
        public bool ContractEnded { get; set; }

        public decimal? ShortfallAtReturn { get; set; }
        public decimal? CollectorEarnedOnCase { get; set; }

        // Filled from the assignment periods.
        public int? CollectorUserId { get; set; }
        public string? CollectorName { get; set; }
        public DateTime? HeldSince { get; set; }
        public decimal Recovered { get; set; }
        public decimal RecoveredThisMonth { get; set; }
        public decimal RecoveredByHolder { get; set; }
        public DateTime? LastPaymentAt { get; set; }

        // Days with no payment while the current collector holds it, counted
        // from their start or their last payment.
        public int DaysWithoutPaymentWhileHeld { get; set; }
        public bool DueForReassignment { get; set; }

        public int OpenFlagCount { get; set; }
        public string? LatestFlagNote { get; set; }

        // The agent/dealer deduction on the contract now (CollectionsRules).
        public decimal CurrentDeduction { get; set; }
        public decimal CollectionCosts { get; set; }
    }

    // One collector's stint on one case.
    public class CollectionPeriodRow
    {
        public int AssignmentId { get; set; }
        public int CaseId { get; set; }
        public int CollectorUserId { get; set; }
        public string? CollectorName { get; set; }
        public DateTime StartAt { get; set; }
        public DateTime? EndAt { get; set; }
        public decimal Recovered { get; set; }
        public decimal RecoveredThisMonth { get; set; }
        public DateTime? LastPaymentAt { get; set; }
    }

    // What one collector has earned and been paid on one case.
    public class CollectorCaseEarning
    {
        public int CaseId { get; set; }
        public long AccountNo { get; set; }
        public long ContractId { get; set; }
        public string CustomerName { get; set; } = "";
        public int CollectorUserId { get; set; }
        public bool HeldNow { get; set; }
        public DateTime FirstHeld { get; set; }
        public decimal Recovered { get; set; }
        public decimal RecoveredThisMonth { get; set; }
        public decimal Earned { get; set; }
        public decimal Paid { get; set; }
        public decimal Due => Math.Max(0, Earned - Paid);
    }

    public class CollectorSummary
    {
        public int CollectorUserId { get; set; }
        public string Name { get; set; } = "";
        public int CasesHeld { get; set; }
        public decimal ShortfallAtHandover { get; set; }
        public decimal Recovered { get; set; }
        public decimal RecoveredThisMonth { get; set; }

        // Recovered on the cases they hold / shortfall at handover on them.
        public decimal RecoveryRatePct { get; set; }
        public decimal Earned { get; set; }
        public decimal Paid { get; set; }
        public decimal Due { get; set; }
        public int DueForReassignment { get; set; }
        public int OpenFlags { get; set; }
    }

    public class CollectionFlagRow
    {
        public int Id { get; set; }
        public int CaseId { get; set; }
        public long AccountNo { get; set; }
        public string CustomerName { get; set; } = "";
        public int CollectorUserId { get; set; }
        public string? CollectorName { get; set; }
        public string Note { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public DateTime? ResolvedAt { get; set; }
    }

    public class CollectorOption
    {
        public int UserId { get; set; }
        public string Name { get; set; } = "";
        public int DealerId { get; set; }
        public bool IsDealerUser { get; set; }
    }

    // Collections arrears carried by one dealer or agent.
    public class CollectionsExposureRow
    {
        public string PayeeType { get; set; } = "";
        public int PayeeId { get; set; }
        public string Name { get; set; } = "";
        public int Accounts { get; set; }
        public decimal Deduction { get; set; }
    }

    public class CollectionsAdminViewModel
    {
        // "cases" (default), "pool", "flags" or "closed".
        public string Tab { get; set; } = "cases";
        public string? Search { get; set; }
        public int? DealerId { get; set; }
        public int? CollectorUserId { get; set; }
        public bool SetupMissing { get; set; }

        public List<CollectorSummary> Collectors { get; set; } = new();
        public List<CollectionCaseRow> Cases { get; set; } = new();
        public List<CollectionContractStanding> Pool { get; set; } = new();
        public List<CollectionFlagRow> Flags { get; set; } = new();
        public List<CollectionsExposureRow> DealerExposure { get; set; } = new();
        public List<CollectionsExposureRow> AgentExposure { get; set; } = new();
        public List<CollectorOption> CollectorOptions { get; set; } = new();
        public List<DealerOption> Dealers { get; set; } = new();

        public int OpenCaseCount { get; set; }
        public int PoolCount { get; set; }
        public int OpenFlagCount { get; set; }
        public int DueForReassignmentCount { get; set; }
    }

    public class CollectorDashboardViewModel
    {
        public bool SetupMissing { get; set; }
        public CollectorSummary Summary { get; set; } = new();
        public List<CollectionCaseRow> Cases { get; set; } = new();
        public List<CollectorCaseEarning> Earnings { get; set; } = new();
        public List<CommissionPayoutRecord> Payouts { get; set; } = new();
    }

    // Dealer / Agent dashboards: their accounts that went to collections.
    public class InCollectionsAccountRow
    {
        public long AccountNo { get; set; }
        public long ContractId { get; set; }
        public string CustomerName { get; set; } = "";
        public string? AgentName { get; set; }
        public DateTime HandoverAt { get; set; }
        public string Status { get; set; } = "";
        public decimal FrozenDeduction { get; set; }
        public decimal CollectionCosts { get; set; }
        public decimal CurrentDeduction { get; set; }
    }

    public class InCollectionsSection
    {
        public bool IsAgentView { get; set; }
        public List<InCollectionsAccountRow> Rows { get; set; } = new();
    }
}
