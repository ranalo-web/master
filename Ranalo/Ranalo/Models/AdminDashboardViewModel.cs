namespace Ranalo.Models
{
    public class AdminDashboardViewModel
    {
        // Reporting period (week | month | ytd | year) and what it's compared
        // with -- see AdminDashboardRules.ResolvePeriod.
        public string Period { get; set; } = "month";
        public string PeriodLabel { get; set; } = "this month";
        public string PriorLabel { get; set; } = "same days last month";

        // Portfolio Snapshot: profit this period and the comparison period.
        public AdminProfit Profit { get; set; } = new();
        public AdminProfit PriorProfit { get; set; } = new();

        // Month only: days gone and days in the month, for the month-end pace.
        public int MonthDaysElapsed { get; set; }
        public int MonthDays { get; set; }

        // Revenue vs costs, last 12 months (same rows as the Financials chart).
        public List<FinancialsMonthRow> ProfitTrend { get; set; } = new();

        // What to do, most urgent first.
        public List<AdminAction> Actions { get; set; } = new();

        // Book health, live. Arrears here leaves restructured accounts out;
        // they have their own card.
        public int ActiveAccounts { get; set; }
        public int NewInPeriod { get; set; }
        public int NewPriorPeriod { get; set; }
        public int BadDebtCount { get; set; }
        public int RestructuredCount { get; set; }
        public decimal RestructuredBehind { get; set; }
        public decimal RestructuredBalance { get; set; }
        public int RestructuredPayingCount { get; set; }

        // Loan book still to collect (approved write-offs excluded), set by
        // AdminDashboardController from the age-of-debt figures.
        public decimal LoanBookOutstanding { get; set; }
        public int LoanBookAccounts { get; set; }

        // Paid as a share of what was due to date, across the book; and the
        // share of a contract that has to be collected to cover the phone.
        public decimal RepaymentRatePct { get; set; }
        public decimal BreakEvenCollectionPct { get; set; }

        // Loss given default across all defaulted accounts (Services/LossGivenDefault.cs).
        public Ranalo.Services.LossGivenDefault.Summary Lgd { get; set; } = Ranalo.Services.LossGivenDefault.Summary.None;
        public Ranalo.Services.LossGivenDefault.LeftOut LgdLeftOut { get; set; } = Ranalo.Services.LossGivenDefault.LeftOut.None;
        public int MissingDeviceCostCount { get; set; }

        // Write-offs dated in the period, and waiting for review.
        public WriteOffPeriodSummary WriteOffsPeriod { get; set; } = new();
        public int WriteOffsPendingCount { get; set; }
        public decimal WriteOffsPendingBalance { get; set; }

        // Portfolio split for the doughnut, active accounts only.
        public int PortfolioRestructuredCount { get; set; }
        public int PortfolioGoodCount { get; set; }
        public int PortfolioSlowCount { get; set; }
        public int PortfolioArrearsCount { get; set; }
        public int PortfolioBadCount { get; set; }

        // Write-offs card -- the WriteOffs register (approved write-offs
        // dated this / last calendar month), same definition as Financials.
        // Set by AdminDashboardController.
        public WriteOffPeriodSummary WriteOffsThisMonth { get; set; } = new();
        public WriteOffPeriodSummary WriteOffsLastMonth { get; set; } = new();

        public decimal RevenueThisMonth { get; set; }
        public decimal RevenueGrowthPct { get; set; }
        public decimal RevenueTargetThisMonth { get; set; }

        public int TotalAccounts { get; set; }
        public int GoodAccounts { get; set; }
        public int BadAccounts { get; set; }

        // Live "new accounts this month" count and its growth vs. the prior
        // month -- same source (GetDealerRevenueForPeriodAsync) as the
        // period-filter dropdown's AJAX response, so the initial page load
        // and a manual "Month" selection always agree instead of showing
        // two different numbers.
        public int NewThisMonth { get; set; }
        public decimal? NewThisMonthChangePct { get; set; }

        public int PayingAccounts { get; set; }
        public int NonPayingAccounts { get; set; }
        public int? NonPayingAccountsChange { get; set; }
        public decimal ArrearsTotal { get; set; }
        public decimal ArrearsChangePct { get; set; }

        // Accounts contributing to ArrearsTotal (live, from
        // GetDealerArrearsClassificationAsync -- same source as ArrearsTotal
        // itself) -- replaces the "vs last month" trend on the Total
        // Arrears card, which has no live recompute path (ArrearsChangePct
        // stays rollup-backed/stale; comparing it against the now-live
        // ArrearsTotal would be a meaningless mismatched baseline, same
        // reasoning the Dealer/Approver Dashboards already applied to their
        // own Total Arrears card).
        public int ArrearsTrueCount { get; set; }

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
        public decimal? PortfolioGoodPctChange { get; set; }
        public decimal? CollectionRateChangePct { get; set; }
        public decimal? PortfolioAtRiskChangePct { get; set; }

        // Profitability. Net profit and its margin are derived in the view
        // from RevenueThisMonth, DealerPerformance commissions,
        // CostOfDevicesThisMonth, and BadDebtThisMonth so they can never
        // drift out of sync with those.
        public decimal CostOfDevicesThisMonth { get; set; }
        public decimal BadDebtThisMonth { get; set; }
        public decimal? NetProfitChangePct { get; set; }
        public decimal? ProfitMarginChangePct { get; set; }
        public decimal? ProfitMarginTargetPct { get; set; }
        public decimal? CommissionsChangePct { get; set; }
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
        public decimal? RepeatCustomerRatePct { get; set; }
        public decimal AvgCustomerLifetimeValue { get; set; }
        public decimal? ChurnRatePct { get; set; }

        // Completed Contracts (fully paid off).
        public int CompletedContractsThisMonth { get; set; }
        public decimal? CompletedContractsChangePct { get; set; }
        public decimal? ContractCompletionRatePct { get; set; }
        public decimal? ContractCompletionRateChangePct { get; set; }
        public decimal AvgTimeToCompletionMonths { get; set; }
        public decimal TotalValueCompletedThisMonth { get; set; }

        public List<AdminWatchlistEntry> NonPayers { get; set; } = new();
        public List<AdminWatchlistEntry> SlowPayers { get; set; } = new();
        public List<AdminWatchlistEntry> GoodPayers { get; set; } = new();

        public List<AdminDealerPerformance> DealerPerformance { get; set; } = new();
        public List<AdminAgentPerformance> AgentPerformance { get; set; } = new();

        // Agent commission paid out this month (AgentCommissionPayments); the
        // Commissions Paid card adds it to the dealer payouts.
        public decimal AgentCommissionPaidThisMonth { get; set; }

        // Dealer commission payable now across every dealer, and what's held
        // for suspended dealers (CommissionPayees).
        public decimal DealerCommissionPayable { get; set; }
        public decimal DealerCommissionHeld { get; set; }
        public int DealersSuspended { get; set; }

        // Commissions section across every dealer: totals plus account-by-account detail.
        public DashboardCommissionSummary CommissionSummary { get; set; } = new();
        public List<DashboardCommissionAccount> CommissionAccounts { get; set; } = new();

        // Every dealer's accounts 30+ days past lock, most overdue first.
        public List<DashboardCollectionEntry> Collections { get; set; } = new();
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

        // Accounts split: still paying vs paid off in full. Late/Bad %s are
        // shares of OpenCount.
        public int OpenCount { get; set; }
        public int CompletedCount { get; set; }

        // More than 7 days past lock, restructured left out.
        public int BehindCount { get; set; }
        public decimal BehindPct { get; set; }
        public decimal BehindAmount { get; set; }

        // Bad customers: more than 90 days past lock, restructured left out
        // -- a share of the dealer's open accounts.
        public int BadCount { get; set; }
        public decimal BadPct { get; set; }
        public int RestructuredCount { get; set; }
        public int NewInPeriod { get; set; }

        // Loss given default on this dealer's defaulted accounts.
        public Ranalo.Services.LossGivenDefault.Summary Lgd { get; set; } = Ranalo.Services.LossGivenDefault.Summary.None;
    }

    // Same metrics as the Dealer Dashboard's agent rows, plus the dealer.
    public class AdminAgentPerformance : DealerAgentPerformance
    {
        public string DealerName { get; set; } = "";
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

        // "Completed" (fully paid off) or "UpsellTarget" (80%+ paid, not yet
        // done -- a renewal/upsell candidate). See DashboardCompletedContractStatus.
        public string Status { get; set; } = "Completed";
        public decimal? PctComplete { get; set; }
    }
}
