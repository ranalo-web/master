namespace Ranalo.Models
{
    public class AdminDashboardViewModel
    {
        public decimal RevenueThisMonth { get; set; }
        public decimal RevenueGrowthPct { get; set; }
        public decimal RevenueTargetThisMonth { get; set; }

        public int TotalAccounts { get; set; }
        public int GoodAccounts { get; set; }
        public int BadAccounts { get; set; }

        public int PayingAccounts { get; set; }
        public int NonPayingAccounts { get; set; }
        public int NonPayingAccountsChange { get; set; }
        public decimal ArrearsTotal { get; set; }
        public decimal ArrearsChangePct { get; set; }

        public List<string> GrowthMonths { get; set; } = new();
        public List<decimal> RevenueByMonth { get; set; } = new();
        public List<int> AccountsByMonth { get; set; } = new();

        public decimal PortfolioGoodPct { get; set; }
        public decimal PortfolioSlowPct { get; set; }
        public decimal PortfolioArrearsPct { get; set; }
        public decimal PortfolioNonPayingPct { get; set; }

        public decimal CollectionRatePct { get; set; }
        public decimal PortfolioAtRiskPct { get; set; }

        // Percentage-point change vs. the prior month.
        public decimal PortfolioGoodPctChange { get; set; }
        public decimal CollectionRateChangePct { get; set; }
        public decimal PortfolioAtRiskChangePct { get; set; }

        // Profitability. Net profit and its margin are derived in the view
        // from RevenueThisMonth, DealerPerformance commissions,
        // CostOfDevicesThisMonth, and BadDebtThisMonth so they can never
        // drift out of sync with those.
        public decimal CostOfDevicesThisMonth { get; set; }
        public decimal BadDebtThisMonth { get; set; }
        public decimal NetProfitChangePct { get; set; }
        public decimal ProfitMarginChangePct { get; set; }
        public decimal ProfitMarginTargetPct { get; set; }
        public decimal CommissionsChangePct { get; set; }
        public decimal BadDebtChangePct { get; set; }

        // Full income-statement walk (Revenue -> Retained Earnings). Gross
        // profit, operating profit etc. are derived in the view from these
        // plus RevenueThisMonth/commissions/BadDebtThisMonth above, so the
        // "Net Profit" KPI card and this statement never disagree.
        public decimal OperatingExpensesThisMonth { get; set; }
        public decimal TaxRatePct { get; set; }
        public decimal DividendsPaidThisMonth { get; set; }

        // Customer Performance card.
        public int TotalCustomers { get; set; }
        public int NewCustomersThisMonth { get; set; }
        public decimal RepeatCustomerRatePct { get; set; }
        public decimal AvgCustomerLifetimeValue { get; set; }
        public decimal ChurnRatePct { get; set; }

        // Completed Contracts (fully paid off).
        public int CompletedContractsThisMonth { get; set; }
        public decimal CompletedContractsChangePct { get; set; }
        public decimal ContractCompletionRatePct { get; set; }
        public decimal ContractCompletionRateChangePct { get; set; }
        public decimal AvgTimeToCompletionMonths { get; set; }
        public decimal TotalValueCompletedThisMonth { get; set; }

        public List<AdminWatchlistEntry> NonPayers { get; set; } = new();
        public List<AdminWatchlistEntry> SlowPayers { get; set; } = new();
        public List<AdminWatchlistEntry> GoodPayers { get; set; } = new();

        public List<AdminDealerPerformance> DealerPerformance { get; set; } = new();
        public List<AdminAgentPerformance> AgentPerformance { get; set; } = new();
        public List<AdminProductPerformance> ProductPerformance { get; set; } = new();
        public List<AdminCompletedContract> CompletedContracts { get; set; } = new();
    }

    public class AdminWatchlistEntry
    {
        public string CustomerName { get; set; } = "";
        public string DealerName { get; set; } = "";
        public string Phone { get; set; } = "";
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

    public class AdminProductPerformance
    {
        public int Rank { get; set; }
        public string ProductName { get; set; } = "";
        public int UnitsFinanced { get; set; }
        public decimal AvgValue { get; set; }
        public decimal Revenue { get; set; }
        public decimal DefaultRatePct { get; set; }
    }

    public class AdminCompletedContract
    {
        public string CustomerName { get; set; } = "";
        public string DealerName { get; set; } = "";
        public string ProductName { get; set; } = "";
        public string CompletedDate { get; set; } = "";
        public decimal TotalPaid { get; set; }
        public int DurationMonths { get; set; }
    }
}
