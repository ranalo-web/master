-- 002 first created the PayoutId indexes on AgentCommissionPayments and
-- DealerCommissionPayments as filtered indexes (WHERE PayoutId IS NOT NULL).
-- A filtered index makes SQL Server reject any INSERT/UPDATE on the table
-- from a connection with QUOTED_IDENTIFIER OFF (sqlcmd's default, and some
-- older tools/stored procedures), so anything else that writes commission
-- payments could start failing. This swaps them for plain indexes, which
-- have no such requirement. 002 now creates them plain.
--
-- Safe to re-run: only rebuilds an index that is still filtered.

IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('AgentCommissionPayments') AND name = 'IX_AgentCommissionPayments_PayoutId' AND has_filter = 1)
BEGIN
    DROP INDEX IX_AgentCommissionPayments_PayoutId ON AgentCommissionPayments;
    CREATE INDEX IX_AgentCommissionPayments_PayoutId ON AgentCommissionPayments (PayoutId);
END
GO

IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('DealerCommissionPayments') AND name = 'IX_DealerCommissionPayments_PayoutId' AND has_filter = 1)
BEGIN
    DROP INDEX IX_DealerCommissionPayments_PayoutId ON DealerCommissionPayments;
    CREATE INDEX IX_DealerCommissionPayments_PayoutId ON DealerCommissionPayments (PayoutId);
END
GO
