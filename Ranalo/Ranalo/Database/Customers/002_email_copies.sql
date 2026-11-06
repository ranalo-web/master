-- Email copies of customer messages (ScheduledCustomerEmailCopies): every
-- reminder, receipt and update sent to the phone is also emailed to a
-- customer who has given an email in the app, from the no-reply address.
-- EmailedUpToUtc: messages up to this time have been emailed (or were sent
-- before the customer joined), so nothing goes twice.
--
-- Run after 001. Needs an admin login. Safe to re-run.

IF COL_LENGTH('dbo.CustomerAccess', 'EmailedUpToUtc') IS NULL
    ALTER TABLE dbo.CustomerAccess ADD EmailedUpToUtc DATETIME2 NULL;
GO
