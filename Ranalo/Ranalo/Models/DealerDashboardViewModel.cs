namespace Ranalo.Models
{
    public class DealerDashboardViewModel
    {
        public string DealerName { get; set; } = "";

        // Accounts of this dealer (or agent) that went to collections.
        public InCollectionsSection InCollections { get; set; } = new();

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

        // Performance Bonus Tracker card (Agent Dashboard only): lifetime
        // commission actually paid to this one agent (AgentCommissionPayments,
        // no period filter -- distinct from CommissionPaidThisPeriod above and
        // from the dealer-wide CommissionPaidToAgents), plus a breakdown of
        // this agent's accounts against the 25%-of-deposit performance bonus
        // (StartDate 90+ days ago) -- see GetAgentCommissionSummaryAsync's
        // AgentGrossCommission formula. An account only counts as "earned"
        // once it's past 90 days AND currently performing (not past its
        // NextLockDate); past 90 days but in arrears is "at risk" (the bonus
        // portion is being withheld, same accounts contributing to
        // CommissionWithheldForArrears); under 90 days but within 30 days of
        // the milestone is "upcoming".
        public decimal CommissionPaidLifetime { get; set; }
        public int BonusEarnedAccountCount { get; set; }
        public int BonusAtRiskAccountCount { get; set; }
        public int BonusUpcomingAccountCount { get; set; }

        // Performance Bonus card: the 25% bonus held (count by reason, and
        // amount), and the value of bonuses due to be reached in the next 30 days.
        public decimal BonusHeldAmount { get; set; }
        public int BonusHeldPastLockCount { get; set; }
        public decimal BonusUpcomingValue { get; set; }

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

        // Suspension (CommissionPayoutRules): more than 30% of the payee's
        // accounts 7+ days past their lock date. While suspended nothing is
        // payable; the commission is held until the rate drops.
        // Dealer Commissions card: the dealer's own position.
        public bool DealerCommissionSuspended { get; set; }
        public decimal DealerDefaultRatePct { get; set; }
        public decimal DealerCommissionHeld { get; set; }

        // Agent Commissions card, dealer view: agents suspended and what
        // they're owed but can't be paid yet. Agent view: this agent's own.
        public int AgentsSuspendedCount { get; set; }
        public decimal AgentCommissionHeld { get; set; }
        public bool AgentCommissionSuspended { get; set; }
        public decimal AgentDefaultRatePct { get; set; }

        // Agent commission still due (before arrears), split by part.
        public decimal AgentUpfrontDue { get; set; }
        public decimal AgentBonusDue { get; set; }
        public int BonusHeldNoWooOrderCount { get; set; }

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
        public decimal? PortfolioGoodPctChange { get; set; }

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

        // Approver Dashboard only: replaces My Contracts there (a flat,
        // system-wide contract listing has no natural owner for an Approver
        // overseeing every dealer) with a per-dealer ranking, same Active%/
        // arrears shape as Agent Performance one level up the hierarchy.
        public List<DealerPerformance> DealerPerformance { get; set; } = new();

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
        public decimal? CompletedContractsChangePct { get; set; }
        public decimal? ContractCompletionRatePct { get; set; }
        public decimal? ContractCompletionRateChangePct { get; set; }
        public decimal AvgTimeToCompletionMonths { get; set; }
        public decimal TotalValueCompletedThisMonth { get; set; }

        // Approver Dashboard only (system-wide, all dealers). Orders
        // Awaiting Approval card -- count and how long the oldest one has
        // sat unreviewed, from Woo_Orders.Status = 'pending'/'processing'
        // (see GetOrdersAwaitingApprovalSummaryAsync).
        public int OrdersAwaitingApprovalCount { get; set; }
        public int OldestPendingOrderDays { get; set; }

        // Approver Dashboard only: accounts most needing arrears follow-up,
        // system-wide, sorted by dollar shortfall (see
        // GetCustomersToContactAsync) -- Detail carries the formatted
        // arrears amount, Phone/DealerName above carry the rest.
        public List<DealerWatchlistEntry> CustomersToContact { get; set; } = new();

        // Truly overdue accounts: 30+ days past lock, not restructured, and
        // no payment in the last 7 days -- the same population as Agent
        // Performance's Needs Collection count. Most overdue first.
        public List<DashboardCollectionEntry> Collections { get; set; } = new();

        // Commissions section: totals plus account-by-account detail.
        public DashboardCommissionSummary CommissionSummary { get; set; } = new();
        public List<DashboardCommissionAccount> CommissionAccounts { get; set; } = new();
    }

    public class DashboardCollectionEntry
    {
        public long AccountId { get; set; }
        public string CustomerName { get; set; } = "";
        public string AgentName { get; set; } = "";
        public string? DealerName { get; set; }
        public string DeviceName { get; set; } = "";
        public int DaysPastLock { get; set; }
        public decimal OverdueAmount { get; set; }
        public DateTime? LockDate { get; set; }
        public DateTime? LastPaymentDate { get; set; }
        public string? Phone { get; set; }
        // Primary and second next of kin from the customer's latest WooCommerce
        // order (Woo_Orders_NextOfKin). Null when none was captured.
        public string? NextOfKinName { get; set; }
        public string? NextOfKinPhone { get; set; }
        public string? NextOfKinIdNumber { get; set; }
        public string? NextOfKin2Name { get; set; }
        public string? NextOfKin2Phone { get; set; }
        public string? NextOfKin2IdNumber { get; set; }
    }

    public class DealerWatchlistEntry
    {
        public long AccountId { get; set; }
        public string CustomerName { get; set; } = "";
        public string AgentName { get; set; } = "";
        public string Detail { get; set; } = "";

        // Populated for the Approver Dashboard's "Customers to Contact" list
        // (system-wide, all dealers) -- unused by Dealer's own Non-Payers/
        // Slow-Payers/Good-Payers tables, which don't render these columns.
        public string? Phone { get; set; }
        public string? DealerName { get; set; }
        // Primary and second next of kin from the customer's latest WooCommerce
        // order (Woo_Orders_NextOfKin). Null when none was captured.
        public string? NextOfKinName { get; set; }
        public string? NextOfKinPhone { get; set; }
        public string? NextOfKinIdNumber { get; set; }
        public string? NextOfKin2Name { get; set; }
        public string? NextOfKin2Phone { get; set; }
        public string? NextOfKin2IdNumber { get; set; }
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

        // Contracts Ending Soon only (not in arrears, 80%+ paid) -- how much
        // of the contract's full value has been paid off. Null for "My
        // Contracts" rows, which show DaysLeft/NextDue instead.
        public decimal? PctComplete { get; set; }
    }

    public class DealerAgentPerformance
    {
        public int Rank { get; set; }
        public string AgentName { get; set; } = "";
        public int Accounts { get; set; }
        public decimal ActivePct { get; set; }
        public decimal PctOfTarget { get; set; }

        // Account mix by days past lock date -- the same exclusive buckets as
        // DashboardReportRepository.ClassifyLock plus its >90 NonPaying split:
        // Good <= 0, Slow 1-7, Arrears 8-90, Bad > 90. These four sum to Accounts.
        public int GoodCount { get; set; }

        // Manually restructured, or in arrears but still before the lock
        // date (on a new plan). Taken out of the lock buckets, so Good, Restructured,
        // Slow, Arrears and Bad together sum to Accounts.
        public int RestructuredCount { get; set; }
        public int SlowCount { get; set; }
        public int ArrearsCount { get; set; }
        public int BadCount { get; set; }

        // Same rule as the Collections table: 30+ days past lock, not
        // restructured, and no payment in the last 7 days. Overlaps Arrears
        // and Bad -- it is a call-list size, not a bucket.
        public int NeedsCollectionCount { get; set; }

        // Total paid to date as a share of what is due to date.
        public decimal RepaymentPct { get; set; }
    }

    public class DealerPerformance
    {
        public int Rank { get; set; }
        public string DealerName { get; set; } = "";
        public int Accounts { get; set; }
        public decimal ActivePct { get; set; }
        public decimal ArrearsTotal { get; set; }
        public decimal RevenueThisMonth { get; set; }
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

    // Raw per-account figures from GetAccountCommissionsAsync.
    public class DashboardAccountCommissionRow
    {
        public long AccountId { get; set; }
        public int AgentId { get; set; }
        public decimal Earned { get; set; }
        public decimal ArrearsDeducted { get; set; }
        public decimal Paid { get; set; }
    }

    // Commissions section totals. Pooled per agent exactly like the Agent
    // Commissions card, so Owed here equals the card's figure.
    public class DashboardCommissionSummary
    {
        public int Agents { get; set; }
        public int Accounts { get; set; }
        public decimal Earned { get; set; }
        public decimal Withheld { get; set; }
        public decimal Paid { get; set; }
        public decimal Owed { get; set; }

        // Owed that can be paid now (suspended agents' share is held).
        public decimal Payable { get; set; }
        public decimal HeldSuspended { get; set; }
        public int SuspendedAgents { get; set; }
        public decimal UpfrontDue { get; set; }
        public decimal BonusDue { get; set; }
    }

    // One Commissions table row per agent-assigned account. Net = Earned -
    // ArrearsDeducted - Paid for this account alone; it can be negative when
    // the account's arrears exceed its commission, which reduces what the
    // agent is owed on their other accounts.
    public class DashboardCommissionAccount
    {
        public long AccountId { get; set; }
        public string CustomerName { get; set; } = "";
        public string AgentName { get; set; } = "";
        public string? DealerName { get; set; }
        public decimal Earned { get; set; }
        public decimal ArrearsDeducted { get; set; }
        public decimal Paid { get; set; }
        public decimal Net { get; set; }
        public string Status { get; set; } = "";

        // Shown instead of Earned/Net, which mixed the two parts and set the
        // customer's whole shortfall against one account's commission.
        public decimal Upfront { get; set; }
        public decimal UpfrontPaid { get; set; }
        public decimal Bonus { get; set; }
        public decimal BonusPaid { get; set; }
        // "earned", "held-woo", "held-lock" or "not-yet"
        public string BonusState { get; set; } = "";
        public int DaysToBonus { get; set; }
        public decimal CustomerBehindBy { get; set; }
    }

    public class DealerDeviceStock
    {
        public string Device { get; set; } = "";
        public int Units { get; set; }
        public decimal AvgValue { get; set; }

        // Money received / money due to date, capped at 100%; and accounts
        // more than a week of instalments behind. GoodPct/ArrearsPct are the
        // not-behind / behind shares of Units.
        public decimal CollectedPct { get; set; }
        public int BehindCount { get; set; }
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
