namespace Ranalo.Models
{
    public class AgentDashboardViewModel
    {
        public string AgentName { get; set; } = "";
        public string DealerName { get; set; } = "";

        public decimal RevenueThisMonth { get; set; }
        public decimal RevenueGrowthPct { get; set; }
        public decimal AvgPerAccount { get; set; }

        public int TotalAccounts { get; set; }
        public decimal ActivePct { get; set; }
        public int NewThisMonth { get; set; }
        public int InDefault { get; set; }
        public decimal DefaultRatePct { get; set; }

        public decimal CommissionReceived { get; set; }
        public decimal ActiveRateVsTargetPct { get; set; }

        public List<string> GrowthMonths { get; set; } = new();
        public List<decimal> RevenueByMonth { get; set; } = new();

        public decimal PortfolioGoodPct { get; set; }
        public decimal PortfolioSlowPct { get; set; }
        public decimal PortfolioArrearsPct { get; set; }
        public decimal PortfolioNonPayingPct { get; set; }

        public List<AgentWatchlistEntry> NonPayers { get; set; } = new();
        public List<AgentWatchlistEntry> SlowPayers { get; set; } = new();
        public List<AgentWatchlistEntry> GoodPayers { get; set; } = new();

        public List<AgentContract> Customers { get; set; } = new();
        public List<AgentContract> ContractsEndingSoon { get; set; } = new();
        public List<AgentCommission> Commissions { get; set; } = new();
        public List<AgentDeviceStock> DeviceStock { get; set; } = new();
    }

    public class AgentWatchlistEntry
    {
        public string CustomerName { get; set; } = "";
        public string Detail { get; set; } = "";
    }

    public class AgentContract
    {
        public string CustomerName { get; set; } = "";
        public string Device { get; set; } = "";
        public decimal MonthlyPayment { get; set; }
        public string Status { get; set; } = "";
        public string NextDue { get; set; } = "";
        public string DaysLeft { get; set; } = "";
    }

    public class AgentCommission
    {
        public string Date { get; set; } = "";
        public string CustomerName { get; set; } = "";
        public decimal Amount { get; set; }
        public string Status { get; set; } = "";
    }

    public class AgentDeviceStock
    {
        public string Device { get; set; } = "";
        public int Units { get; set; }
        public decimal AvgValue { get; set; }
        public decimal GoodPct { get; set; }
        public decimal ArrearsPct { get; set; }
    }
}
