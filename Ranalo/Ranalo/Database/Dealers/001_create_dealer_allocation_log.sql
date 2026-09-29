-- Dealer Allocation audit trail. An account's dealer is its device's group
-- (Devices.DeviceGroupId = Dealers.DealerReference); the Dealer Allocation
-- page sets it for accounts with no dealer and moves accounts between
-- dealers. Every change is logged here: which account, from which group to
-- which dealer, who did it and when.
--
-- Safe to re-run: guarded with an existence check.

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'DealerAllocationLog')
BEGIN
    CREATE TABLE DealerAllocationLog
    (
        Id               INT IDENTITY(1,1) PRIMARY KEY,
        AccountNo        BIGINT NOT NULL,        -- Devices.Id / Contract_Info.ID
        OldDeviceGroupId BIGINT NULL,            -- NULL = had no dealer
        NewDeviceGroupId BIGINT NOT NULL,
        NewDealerId      INT NOT NULL,           -- Dealers.DealerId
        Note             NVARCHAR(300) NULL,
        ChangedByUserId  INT NOT NULL,
        ChangedAtUtc     DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );

    CREATE INDEX IX_DealerAllocationLog_AccountNo ON DealerAllocationLog (AccountNo);
END
GO
