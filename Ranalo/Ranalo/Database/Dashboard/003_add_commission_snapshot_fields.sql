-- Adds the Dealer dashboard's commission KPI fields to DashboardSnapshot.
-- Dealer-only (no Admin equivalent exists on AdminDashboardViewModel), but
-- kept generic/nullable like every other column on this table.
--
-- CommissionReceived: the dealer's own earned commission (0.30 x
-- (lifetime TotalPaid - BuyingPrice - AgentGrossCommission) per account,
-- summed) -- NOT netted against DealerCommissionPayments; this is what
-- they've *earned*, not what's left to collect.
-- CommissionPaidToAgents / CommissionOutstanding: aggregated from each of the
-- dealer's agents' net commission (gross deposit-vesting minus pooled
-- current arrears) and AgentCommissionPayments.
--
-- Safe to re-run: guarded with existence checks.

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('DashboardSnapshot') AND name = 'CommissionReceived')
BEGIN
    ALTER TABLE DashboardSnapshot ADD CommissionReceived DECIMAL(18,2) NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('DashboardSnapshot') AND name = 'CommissionPaidToAgents')
BEGIN
    ALTER TABLE DashboardSnapshot ADD CommissionPaidToAgents DECIMAL(18,2) NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('DashboardSnapshot') AND name = 'CommissionOutstanding')
BEGIN
    ALTER TABLE DashboardSnapshot ADD CommissionOutstanding DECIMAL(18,2) NULL;
END
GO
