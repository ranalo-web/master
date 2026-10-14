-- Adds TermInMonths and DepositAmount to Woo_Orders: the order's
-- "Repayment Period" (WooCommerce attribute pa_repayment-period, 12 or 14
-- months) and the deposit the website quoted (_rc_deposit). Contract
-- creation reads both -- see RepaymentTerms. NULL on older orders, which
-- stay 12-month contracts.
--
-- Safe to re-run: guarded with an existence check.

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Woo_Orders') AND name = 'TermInMonths')
BEGIN
    ALTER TABLE Woo_Orders ADD TermInMonths DECIMAL(9,5) NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Woo_Orders') AND name = 'DepositAmount')
BEGIN
    ALTER TABLE Woo_Orders ADD DepositAmount DECIMAL(18,2) NULL;
END
GO
