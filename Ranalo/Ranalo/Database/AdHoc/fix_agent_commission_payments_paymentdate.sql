-- AgentCommissionPayments already existed (with a different shape) before
-- 001_create_agent_commission_payments.sql ran, so its IF NOT EXISTS guard
-- skipped creating it correctly -- this adds the missing PaymentDate column,
-- same issue already hit once for AmountPaid (see
-- fix_agent_commission_payments_amountpaid.sql). Safe to re-run.

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AgentCommissionPayments') AND name = 'PaymentDate')
BEGIN
    ALTER TABLE AgentCommissionPayments ADD PaymentDate DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME();
END
GO
