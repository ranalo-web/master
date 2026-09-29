namespace Ranalo.Models
{
    public static class AssignmentRole
    {
        public const string Agent = "Agent";
        public const string Collector = "Collector";
    }

    // One open contract on the Account Assignment page.
    public class AssignmentAccount
    {
        public long AccountNo { get; set; }
        public long ContractId { get; set; }
        public string? CustomerName { get; set; }
        public DateTime? StartDate { get; set; }
        public int? DealerId { get; set; }
        public string? DealerName { get; set; }
        public long? AgentUserId { get; set; }
        public string? AgentName { get; set; }
        public int? CollectorUserId { get; set; }
        public string? CollectorName { get; set; }
        public string? ProductName { get; set; }
        public string? NextLockDateRaw { get; set; }
    }

    // An agent or collector to assign.
    public class AssignmentPerson
    {
        public int UserId { get; set; }
        public string Name { get; set; } = "";
        public int DealerId { get; set; }
        public string? DealerName { get; set; }
    }

    public class AccountAssignmentViewModel
    {
        public int? DealerId { get; set; }
        // "noagent" (default), "nocollector" or "all".
        public string Show { get; set; } = "noagent";
        public string? Search { get; set; }
        public List<AssignmentAccount> Accounts { get; set; } = new();
        public List<DealerOption> Dealers { get; set; } = new();
        public List<AssignmentPerson> Agents { get; set; } = new();
        public List<AssignmentPerson> Collectors { get; set; } = new();
    }
}
