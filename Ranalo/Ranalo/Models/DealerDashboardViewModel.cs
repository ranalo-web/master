namespace Ranalo.Models
{
    public class DealerDashboardViewModel
    {
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
        public decimal CommissionPaidToAgents { get; set; }
        public decimal CommissionOutstanding { get; set; }

        public decimal ActiveRateVsTargetPct { get; set; }

        public List<string> GrowthMonths { get; set; } = new();
        public List<decimal> RevenueByMonth { get; set; } = new();

        public decimal PortfolioGoodPct { get; set; }
        public decimal PortfolioSlowPct { get; set; }
        public decimal PortfolioArrearsPct { get; set; }
        public decimal PortfolioNonPayingPct { get; set; }

        public List<DealerWatchlistEntry> NonPayers { get; set; } = new();
        public List<DealerWatchlistEntry> SlowPayers { get; set; } = new();
        public List<DealerWatchlistEntry> GoodPayers { get; set; } = new();

        public List<DealerContract> Contracts { get; set; } = new();
        public List<DealerAgentPerformance> AgentPerformance { get; set; } = new();
        public List<DealerContract> ContractsEndingSoon { get; set; } = new();

        public List<DealerCommissionReceived> CommissionsReceived { get; set; } = new();
        public List<DealerCommissionPaid> CommissionsPaid { get; set; } = new();
        public List<DealerDeviceStock> DeviceStock { get; set; } = new();
    }

    public class DealerWatchlistEntry
    {
        public string CustomerName { get; set; } = "";
        public string AgentName { get; set; } = "";
        public string Detail { get; set; } = "";
    }

    public class DealerContract
    {
        public string CustomerName { get; set; } = "";
        public string AgentName { get; set; } = "";
        public string Device { get; set; } = "";
        public decimal MonthlyPayment { get; set; }
        public string Status { get; set; } = "";
        public string NextDue { get; set; } = "";
        public string DaysLeft { get; set; } = "";
    }

    public class DealerAgentPerformance
    {
        public int Rank { get; set; }
        public string AgentName { get; set; } = "";
        public int Accounts { get; set; }
        public decimal ActivePct { get; set; }
        public decimal PctOfTarget { get; set; }
    }

    public class DealerCommissionReceived
    {
        public string Date { get; set; } = "";
        public string CustomerName { get; set; } = "";
        public decimal Amount { get; set; }
        public string Status { get; set; } = "";
    }

    public class DealerCommissionPaid
    {
        public string AgentName { get; set; } = "";
        public int Accounts { get; set; }
        public decimal Due { get; set; }
        public decimal Paid { get; set; }
        public decimal Outstanding { get; set; }
        public string Status { get; set; } = "";
    }

    public class DealerDeviceStock
    {
        public string Device { get; set; } = "";
        public int Units { get; set; }
        public decimal AvgValue { get; set; }
        public decimal GoodPct { get; set; }
        public decimal ArrearsPct { get; set; }
    }
}
