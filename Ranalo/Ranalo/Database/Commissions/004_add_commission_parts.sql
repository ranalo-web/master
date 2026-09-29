-- Agent commission is paid in two parts: the 50% upfront (at sale) and the
-- 25% bonus. Each payment now records its type (Upfront / Bonus / Auto; a
-- dealer payment is "Commission"), and each agent payment line records the
-- part it paid, so every account keeps its own upfront-paid and bonus-paid
-- totals (needed when a device is moved, repossessed or written off).
--
-- Existing payout lines are all upfronts (confirmed by the business,
-- 29 Sep 2026: no bonus has been paid yet). Older lines with no PayoutId
-- are left without a part; the app counts those toward the upfront first.
--
-- Safe to re-run: every step is guarded.

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CommissionPayouts') AND name = 'PaymentType')
BEGIN
    ALTER TABLE CommissionPayouts ADD PaymentType VARCHAR(10) NULL;   -- Upfront | Bonus | Auto | Commission
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AgentCommissionPayments') AND name = 'CommissionPart')
BEGIN
    ALTER TABLE AgentCommissionPayments ADD CommissionPart VARCHAR(10) NULL;   -- Upfront | Bonus
END
GO

UPDATE CommissionPayouts SET PaymentType = 'Upfront' WHERE PayeeType = 'Agent' AND PaymentType IS NULL;
UPDATE CommissionPayouts SET PaymentType = 'Commission' WHERE PayeeType = 'Dealer' AND PaymentType IS NULL;
UPDATE AgentCommissionPayments SET CommissionPart = 'Upfront' WHERE PayoutId IS NOT NULL AND CommissionPart IS NULL;
GO
