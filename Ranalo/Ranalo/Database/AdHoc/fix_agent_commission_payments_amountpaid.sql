-- AgentCommissionPayments already existed (with a different shape) before
-- 001_create_agent_commission_payments.sql ran, so its IF NOT EXISTS guard
-- skipped creating it correctly -- this adds just the missing column.
-- Safe to re-run.

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AgentCommissionPayments') AND name = 'AmountPaid')
BEGIN
    ALTER TABLE AgentCommissionPayments ADD AmountPaid DECIMAL(18,2) NOT NULL DEFAULT 0;
END
GO
