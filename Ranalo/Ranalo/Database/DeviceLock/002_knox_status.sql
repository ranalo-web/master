-- Knox's own status for each phone (KnoxStatusRules, the Knox check page).
-- Filled every 2 hours by ScheduledTaskKnoxDevicesPull from Knox's device
-- list, every status (Accepted, Activating, Enrolled, Locked, Completed...),
-- so our pages show what Knox says, not only what we sent.
--
-- RUN BEFORE PUBLISHING: the Recovery and Knox check pages read these columns.
-- Needs an admin login. Safe to re-run.

IF COL_LENGTH('dbo.Devices', 'KnoxStatus') IS NULL
    ALTER TABLE dbo.Devices ADD KnoxStatus NVARCHAR(40) NULL;
IF COL_LENGTH('dbo.Devices', 'KnoxStatusAtUtc') IS NULL
    ALTER TABLE dbo.Devices ADD KnoxStatusAtUtc DATETIME2 NULL;      -- when we last read it
IF COL_LENGTH('dbo.Devices', 'KnoxLastSeenUtc') IS NULL
    ALTER TABLE dbo.Devices ADD KnoxLastSeenUtc DATETIME2 NULL;      -- Knox's last contact with the phone
IF COL_LENGTH('dbo.Devices', 'KnoxRelockUtc') IS NULL
    ALTER TABLE dbo.Devices ADD KnoxRelockUtc DATETIME2 NULL;        -- Knox's next lock time
IF COL_LENGTH('dbo.Devices', 'KnoxOffline') IS NULL
    ALTER TABLE dbo.Devices ADD KnoxOffline BIT NULL;
GO
