namespace Ranalo.Models
{
    public class DealerDashboardViewModel
    {
        public string DealerName { get; set; } = "";

        public decimal RevenueThisMonth { get; set; }
        public decimal RevenueGrowthPct { get; set; }
        public decimal AvgPerAccount { get; set; }

        // Expected revenue for the period if every account paid exactly on
        // schedule -- see DashboardRevenuePeriodRow.TargetRevenue. Live-computed,
        // not rollup-backed.
        public decimal RevenueTarget { get; set; }

        public int TotalAccounts { get; set; }
        public decimal ActivePct { get; set; }
        public int NewThisMonth { get; set; }
        public int InDefault { get; set; }
        public decimal DefaultRatePct { get; set; }
        public int NonPayingChange { get; set; }

        public decimal ArrearsTotal { get; set; }
        public decimal ArrearsChangePct { get; set; }

        public decimal CommissionReceived { get; set; }
        public decimal CommissionPaidToAgents { get; set; }

        // Still owed TO the dealer's agents -- lifetime, floored at 0 (an
        // agent whose arrears wiped out their earned commission contributes
        // 0, not a negative offset against other agents).
        public decimal CommissionOutstanding { get; set; }

        // Still owed TO the dealer BY Ranalo: CommissionReceived (earned)
        // minus DealerCommissionPayments actually paid out, floored at 0.
        // Distinct from CommissionOutstanding above (agent-facing).
        public decimal DealerCommissionOutstanding { get; set; }

        public decimal CommissionsChangePct { get; set; }

        public decimal BadDebtThisMonth { get; set; }
        public decimal BadDebtChangePct { get; set; }

        public decimal ActiveRateVsTargetPct { get; set; }

        public List<string> GrowthMonths { get; set; } = new();
        public List<decimal> RevenueByMonth { get; set; } = new();
        public List<int> AccountsByMonth { get; set; } = new();

        public decimal PortfolioGoodPct { get; set; }
        public decimal PortfolioSlowPct { get; set; }
        public decimal PortfolioArrearsPct { get; set; }
        public decimal PortfolioNonPayingPct { get; set; }
        public decimal PortfolioGoodPctChange { get; set; }

        public decimal CollectionRatePct { get; set; }
        public decimal CollectionRateChangePct { get; set; }
        public decimal PortfolioAtRiskPct { get; set; }
        public decimal PortfolioAtRiskChangePct { get; set; }

        public List<DealerWatchlistEntry> NonPayers { get; set; } = new();
        public List<DealerWatchlistEntry> SlowPayers { get; set; } = new();
        public List<DealerWatchlistEntry> GoodPayers { get; set; } = new();

        public List<DealerContract> Contracts { get; set; } = new();
        public List<DealerAgentPerformance> AgentPerformance { get; set; } = new();
        public List<DealerContract> ContractsEndingSoon { get; set; } = new();

        public List<DealerCommissionReceived> CommissionsReceived { get; set; } = new();
        public List<DealerCommissionPaid> CommissionsPaid { get; set; } = new();
        public List<DealerDeviceStock> DeviceStock { get; set; } = new();

        // Customer Performance card. "Customers" here is this dealer's own
        // TotalAccounts/NewThisMonth above (its own accounts), plus these.
        public decimal RepeatCustomerRatePct { get; set; }
        public decimal AvgCustomerLifetimeValue { get; set; }
        public decimal ChurnRatePct { get; set; }

        public List<DealerCompletedContract> CompletedContracts { get; set; } = new();

        public int CompletedContractsThisMonth { get; set; }
        public decimal CompletedContractsChangePct { get; set; }
        public decimal ContractCompletionRatePct { get; set; }
        public decimal ContractCompletionRateChangePct { get; set; }
        public decimal AvgTimeToCompletionMonths { get; set; }
        public decimal TotalValueCompletedThisMonth { get; set; }
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

    public class DealerCompletedContract
    {
        public string CustomerName { get; set; } = "";
        public string ProductName { get; set; } = "";
        public string CompletedDate { get; set; } = "";
        public decimal TotalPaid { get; set; }
        public int DurationMonths { get; set; }

        // "Completed" (fully paid off) or "UpsellTarget" (80%+ paid, not yet
        // done -- a renewal/upsell candidate). See DashboardCompletedContractStatus.
        public string Status { get; set; } = "Completed";
        public decimal? PctComplete { get; set; }
    }
}
