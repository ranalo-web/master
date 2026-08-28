namespace Ranalo.Models
{
    public class AdminDashboardViewModel
    {
        public decimal RevenueThisMonth { get; set; }
        public decimal RevenueGrowthPct { get; set; }

        public int TotalAccounts { get; set; }
        public int GoodAccounts { get; set; }
        public int BadAccounts { get; set; }

        public int PayingAccounts { get; set; }
        public int NonPayingAccounts { get; set; }
        public decimal ArrearsTotal { get; set; }

        public List<string> GrowthMonths { get; set; } = new();
        public List<decimal> RevenueByMonth { get; set; } = new();
        public List<int> AccountsByMonth { get; set; } = new();

        public decimal PortfolioGoodPct { get; set; }
        public decimal PortfolioSlowPct { get; set; }
        public decimal PortfolioArrearsPct { get; set; }
        public decimal PortfolioNonPayingPct { get; set; }

        public List<AdminWatchlistEntry> NonPayers { get; set; } = new();
        public List<AdminWatchlistEntry> SlowPayers { get; set; } = new();
        public List<AdminWatchlistEntry> GoodPayers { get; set; } = new();

        public List<AdminDealerPerformance> DealerPerformance { get; set; } = new();
        public List<AdminAgentPerformance> AgentPerformance { get; set; } = new();
    }

    public class AdminWatchlistEntry
    {
        public string CustomerName { get; set; } = "";
        public string DealerName { get; set; } = "";
        public string Detail { get; set; } = "";
    }

    public class AdminDealerPerformance
    {
        public int Rank { get; set; }
        public string DealerName { get; set; } = "";
        public int Accounts { get; set; }
        public decimal ActivePct { get; set; }
        public decimal Revenue { get; set; }
        public decimal CommissionPaid { get; set; }
        public decimal CommissionDue { get; set; }
        public decimal PctOfTarget { get; set; }
    }

    public class AdminAgentPerformance
    {
        public int Rank { get; set; }
        public string AgentName { get; set; } = "";
        public string DealerName { get; set; } = "";
        public int Accounts { get; set; }
        public decimal ActivePct { get; set; }
        public decimal PctOfTarget { get; set; }
    }
}
