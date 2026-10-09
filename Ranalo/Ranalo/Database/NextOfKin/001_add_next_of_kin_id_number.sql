-- Next of kin ID numbers (WooCommerce form fields billing_next_of_kin_id_number
-- and billing_next_of_kin_id_number_2), shown under the next of kin contacts.
--
-- Also fills in second next of kin: the sync saved them with the first next
-- of kin's insert, which skipped them because a first one already existed,
-- so none was ever stored. They're rebuilt here from the order metadata.
--
-- RUN THIS BEFORE DEPLOYING THE APP: the order sync writes IdNumber.
-- Safe to re-run: every change is guarded.

IF COL_LENGTH('dbo.Woo_Orders_NextOfKin', 'IdNumber') IS NULL
    ALTER TABLE dbo.Woo_Orders_NextOfKin ADD IdNumber NVARCHAR(50) NULL;
GO

-- Second next of kin missing for orders whose form had one.
INSERT INTO dbo.Woo_Orders_NextOfKin (Id, OrderId, Name, Phone, Email, Address, IsPrimary)
SELECT NEWID(), wo.OrderId,
       (SELECT TOP 1 m.Value FROM dbo.Woo_Orders_MetaData m WHERE m.OrderId = wo.OrderId AND m.[Key] = 'billing_next_of_kin_2'),
       (SELECT TOP 1 m.Value FROM dbo.Woo_Orders_MetaData m WHERE m.OrderId = wo.OrderId AND m.[Key] = 'billing_next_of_kin_contacts_2'),
       (SELECT TOP 1 m.Value FROM dbo.Woo_Orders_MetaData m WHERE m.OrderId = wo.OrderId AND m.[Key] = 'billing_email_of_your_next_of_kin_2'),
       (SELECT TOP 1 m.Value FROM dbo.Woo_Orders_MetaData m WHERE m.OrderId = wo.OrderId AND m.[Key] = 'billing_next_of_kin_address_2'),
       0
FROM (SELECT DISTINCT OrderId FROM dbo.Woo_Orders) wo
WHERE EXISTS (SELECT 1 FROM dbo.Woo_Orders_MetaData m
              WHERE m.OrderId = wo.OrderId AND m.[Key] = 'billing_next_of_kin_2' AND LEN(LTRIM(m.Value)) > 0)
  AND NOT EXISTS (SELECT 1 FROM dbo.Woo_Orders_NextOfKin k WHERE k.OrderId = wo.OrderId AND k.IsPrimary = 0);

-- ID numbers already captured on orders.
UPDATE k
SET IdNumber = (SELECT TOP 1 LTRIM(RTRIM(m.Value)) FROM dbo.Woo_Orders_MetaData m
                WHERE m.OrderId = k.OrderId
                  AND m.[Key] = CASE WHEN k.IsPrimary = 1 THEN 'billing_next_of_kin_id_number'
                                     ELSE 'billing_next_of_kin_id_number_2' END)
FROM dbo.Woo_Orders_NextOfKin k
WHERE k.IdNumber IS NULL;
