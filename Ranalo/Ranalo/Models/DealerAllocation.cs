namespace Ranalo.Models
{
    // One account on the Dealer Allocation page.
    public class DealerAllocationAccount
    {
        public long AccountNo { get; set; }
        public long? ContractId { get; set; }
        public string? CustomerName { get; set; }
        public DateTime? StartDate { get; set; }
        public bool HasDevice { get; set; }
        public string? ProductName { get; set; }
        public string? Imei { get; set; }
        public long? DeviceGroupId { get; set; }
        public int? DealerId { get; set; }
        public string? DealerName { get; set; }
        public string? AgentName { get; set; }
        public decimal TotalPaid { get; set; }
    }

    public class DealerOption
    {
        public int DealerId { get; set; }
        public string CompanyName { get; set; } = "";
        public long? GroupId { get; set; }
    }

    public class DealerAllocationLogEntry
    {
        public long AccountNo { get; set; }
        public long? OldDeviceGroupId { get; set; }
        public string? OldDealerName { get; set; }
        public string NewDealerName { get; set; } = "";
        public string? Note { get; set; }
        public string? ChangedByName { get; set; }
        public DateTime ChangedAtUtc { get; set; }
    }

    public class DealerAllocationViewModel
    {
        // "unassigned" (default) or "all".
        public string Show { get; set; } = "unassigned";
        public string? Search { get; set; }
        public int UnassignedCount { get; set; }
        public int NoDeviceCount { get; set; }
        public List<DealerAllocationAccount> Accounts { get; set; } = new();
        public List<DealerOption> Dealers { get; set; } = new();
        public List<DealerAllocationLogEntry> RecentChanges { get; set; } = new();
        public bool LogUnavailable { get; set; }
    }
}
