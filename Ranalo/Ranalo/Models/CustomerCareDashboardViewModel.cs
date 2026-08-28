namespace Ranalo.Models
{
    public class CustomerCareDashboardViewModel
    {
        public string StaffName { get; set; } = "";

        public int CallsDueToday { get; set; }
        public int CallsCritical { get; set; }
        public int CallsBehind { get; set; }

        public int OverdueAccounts { get; set; }
        public decimal TotalArrears { get; set; }

        public int DevicesLocked { get; set; }
        public int UnlockedToday { get; set; }

        public int TicketsResolvedToday { get; set; }
        public int TicketsOpenToday { get; set; }

        public CareAccountLookup ExampleLookup { get; set; } = new();
        public List<CareQueueEntry> CallQueue { get; set; } = new();
        public List<CareLockedDevice> LockedDevices { get; set; } = new();
        public List<CareTicket> RecentTickets { get; set; } = new();
    }

    public class CareAccountLookup
    {
        public string CustomerName { get; set; } = "";
        public string Status { get; set; } = "";
        public string Device { get; set; } = "";
        public string ContractNumber { get; set; } = "";
        public decimal Balance { get; set; }
        public string NextDue { get; set; } = "";
        public string AgentName { get; set; } = "";
        public string DealerName { get; set; } = "";
    }

    public class CareQueueEntry
    {
        public string Priority { get; set; } = "";
        public string CustomerName { get; set; } = "";
        public string Phone { get; set; } = "";
        public string DaysLate { get; set; } = "";
        public decimal Arrears { get; set; }
        public decimal Balance { get; set; }
        public string AgentName { get; set; } = "";
        public string DealerName { get; set; } = "";
    }

    public class CareLockedDevice
    {
        public string CustomerName { get; set; } = "";
        public string Device { get; set; } = "";
        public string LockedAgo { get; set; } = "";
        public decimal Balance { get; set; }
    }

    public class CareTicket
    {
        public string CustomerName { get; set; } = "";
        public string Type { get; set; } = "";
        public string Note { get; set; } = "";
        public string StaffName { get; set; } = "";
        public string Time { get; set; } = "";
    }
}
