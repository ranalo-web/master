-- The rest of what Safaricom sends with each paybill payment, saved by
-- SafaricomC2BController next to the usual KosePayments fields. NULL on
-- payments that came in any other way (IONOS sync, statement upload).
-- MSISDN arrives masked or hashed by Safaricom, never the full number.
--
-- Run before or after deploying: the app saves payments without these
-- columns too, and just logs a warning that the extra details were skipped.
--
-- Safe to re-run: each change is guarded with an existence check.

IF COL_LENGTH('dbo.KosePayments', 'MiddleName') IS NULL
    ALTER TABLE dbo.KosePayments ADD MiddleName NVARCHAR(100) NULL;

IF COL_LENGTH('dbo.KosePayments', 'LastName') IS NULL
    ALTER TABLE dbo.KosePayments ADD LastName NVARCHAR(100) NULL;

IF COL_LENGTH('dbo.KosePayments', 'Msisdn') IS NULL
    ALTER TABLE dbo.KosePayments ADD Msisdn NVARCHAR(100) NULL;

IF COL_LENGTH('dbo.KosePayments', 'TransactionType') IS NULL
    ALTER TABLE dbo.KosePayments ADD TransactionType NVARCHAR(50) NULL;

IF COL_LENGTH('dbo.KosePayments', 'BusinessShortCode') IS NULL
    ALTER TABLE dbo.KosePayments ADD BusinessShortCode NVARCHAR(20) NULL;

IF COL_LENGTH('dbo.KosePayments', 'InvoiceNumber') IS NULL
    ALTER TABLE dbo.KosePayments ADD InvoiceNumber NVARCHAR(100) NULL;

IF COL_LENGTH('dbo.KosePayments', 'OrgAccountBalance') IS NULL
    ALTER TABLE dbo.KosePayments ADD OrgAccountBalance NVARCHAR(30) NULL;

IF COL_LENGTH('dbo.KosePayments', 'ThirdPartyTransID') IS NULL
    ALTER TABLE dbo.KosePayments ADD ThirdPartyTransID NVARCHAR(100) NULL;
