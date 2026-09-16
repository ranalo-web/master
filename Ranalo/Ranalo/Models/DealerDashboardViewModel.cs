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

        // Paying vs Non-Paying card only -- classified from
        // Devices.NextLockDateIsoFormat (more than 7 days past it = "true
        // arrears"), not the accrual-based formula behind InDefault/
        // PortfolioGoodPct above, because that formula doesn't know about a
        // restructuring. See IDashboardReportRepository.GetDealerLockClassificationAsync.
        public int LockGoodCount { get; set; }
        public int LockNonPayingCount { get; set; }

        // Overridden by the live NextLockDate-based classification (see
        // GetDealerArrearsClassificationAsync) -- "true" arrears only
        // (locked accounts), not every account's accrual shortfall.
        // ArrearsChangePct stays rollup-backed but is no longer shown on the
        // Dealer Dashboard -- NextLockDate has no history, so there's no way
        // to recompute "which accounts were locked a month ago" the way
        // ArrearsChangePct's rollup does for the accrual $ total (same
        // tradeoff as the Paying vs Non-Paying card's dropped comparison).
        public decimal ArrearsTotal { get; set; }
        public decimal ArrearsChangePct { get; set; }

        // In arrears on paper (accrual shortfall) but NextLockDate is still
        // in the future or unset -- being paid down on a restructured plan,
        // not truly delinquent. See DashboardArrearsClassificationRow.
        public int ArrearsTrueCount { get; set; }
        public decimal ArrearsAvgDaysLocked { get; set; }
        public decimal RestructuredArrearsTotal { get; set; }
        public int RestructuredArrearsCount { get; set; }

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

        // Agent Commissions card: distinct agent-assigned accounts
        // contributing to CommissionOutstanding, and how much of the gross
        // commission earned is being withheld because of accounts in true
        // (locked) arrears -- see DashboardCommissionRollupRow.
        public int CommissionAccountCount { get; set; }
        public decimal CommissionWithheldForArrears { get; set; }

        // Agent Commissions card: commission paid to agents within the
        // top-of-page filter's selected period, live from AgentCommissionPayments
        // (distinct from the lifetime CommissionPaidToAgents above).
        public decimal CommissionPaidThisPeriod { get; set; }

        // Dealer Commissions card: independent of Agent Commissions --
        // every paid account contributes here, including direct dealer
        // sales with no assigned agent (wider population than
        // CommissionAccountCount above, which is agent-only). PaidThisPeriod
        // is live from DealerCommissionPayments.PaidDate, same top-of-page
        // filter as everything else.
        public int DealerCommissionAccountCount { get; set; }
        public decimal DealerCommissionPaidThisPeriod { get; set; }

        // Accounts excluded from CommissionReceived/DealerCommissionAccountCount
        // because Contract_Info.BuyingPrice was never recorded -- flagged so
        // it can be fixed at the source, not silently treated as free.
        public int DealerCommissionMissingCostCount { get; set; }

        // Incentive figure: how much more dealer commission would be
        // unlocked if every locked/true-arrears account paid off its
        // shortfall today.
        public decimal DealerCommissionWithheldForArrears { get; set; }

        // Bad Debt card: subset of "true" arrears more than 90 days past
        // NextLockDate. BadDebtChangePct is never populated (no rollup
        // write path ever wrote it) and there's no way to recompute it live
        // (NextLockDate has no history) -- not shown on the Dealer Dashboard.
        // See DashboardArrearsClassificationRow.
        public decimal BadDebtThisMonth { get; set; }
        public decimal BadDebtChangePct { get; set; }
        public int BadDebtCount { get; set; }
        public decimal BadDebtAvgDaysLocked { get; set; }

        // Write-offs & Collections card: contract's full term elapsed with a
        // shortfall still outstanding, independent of lock-date status.
        public decimal WriteOffTotal { get; set; }
        public int WriteOffCount { get; set; }

        // Recoveries specifically: payments received this month from
        // accounts currently classified as write-off, not the dealer's
        // overall monthly revenue (that's RevenueThisMonth, shown on the
        // Revenue card already). WriteOffRecoveryRatePct = recovered this
        // month / total outstanding write-off balance.
        public decimal WriteOffRecoveredThisMonth { get; set; }
        public decimal WriteOffRecoveryRatePct { get; set; }

        public decimal ActiveRateVsTargetPct { get; set; }

        public List<string> GrowthMonths { get; set; } = new();
        public List<decimal> RevenueByMonth { get; set; } = new();
        public List<int> AccountsByMonth { get; set; } = new();

        public decimal PortfolioGoodPct { get; set; }
        public decimal PortfolioSlowPct { get; set; }
        public decimal PortfolioArrearsPct { get; set; }
        public decimal PortfolioNonPayingPct { get; set; }
        public decimal PortfolioGoodPctChange { get; set; }

        // Live-computed in DashboardReportService (RevenueThisMonth /
        // RevenueTarget) -- was previously a dead field (no rollup ever wrote
        // it, always read as 0). CollectionRateChangePct is still dead; no
        // historical "amount due a month ago" is computed anywhere.
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
