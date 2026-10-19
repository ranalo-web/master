-- Stock phones: an admin locks a phone with Knox or Transsion by its IMEI
-- alone (no order, no M-Pesa) so it can go out to the field locked. When it's
-- sold, the normal enrolment with the WooCommerce order picks it up.
-- Rules live in Services/DeviceLock/StockPhoneRules.cs.
--
-- RUN THIS BEFORE DEPLOYING THE APP: Enrolments is mapped by Entity
-- Framework, so the app fails to read enrolments until these columns exist.
--
-- Safe to re-run: every change is guarded with an existence check.
-- Enrolments.Status 7 = Stock (no schema change needed for the status).

IF COL_LENGTH('dbo.Enrolments', 'StockLockedAt') IS NULL
    ALTER TABLE dbo.Enrolments ADD StockLockedAt DATETIME2 NULL;          -- sent to the locker as stock
IF COL_LENGTH('dbo.Enrolments', 'StockLockedByUserId') IS NULL
    ALTER TABLE dbo.Enrolments ADD StockLockedByUserId INT NULL;          -- admin who did it
IF COL_LENGTH('dbo.Enrolments', 'StockKnoxApprovedAt') IS NULL
    ALTER TABLE dbo.Enrolments ADD StockKnoxApprovedAt DATETIME2 NULL;    -- Knox: approved and lock sent
IF COL_LENGTH('dbo.Enrolments', 'StockVerifiedAt') IS NULL
    ALTER TABLE dbo.Enrolments ADD StockVerifiedAt DATETIME2 NULL;        -- locker confirmed the phone is locked
IF COL_LENGTH('dbo.Enrolments', 'StockCheck') IS NULL
    ALTER TABLE dbo.Enrolments ADD StockCheck NVARCHAR(500) NULL;         -- last check's answer
GO
