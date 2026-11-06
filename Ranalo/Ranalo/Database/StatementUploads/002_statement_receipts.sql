-- The M-Pesa receipt numbers in each uploaded statement, so the Statement
-- Uploads page can list the other way round too: payments in KosePayments,
-- dated inside an uploaded statement's period, that the statement doesn't
-- have (entered by hand, a test, a duplicate, another paybill...).
--
-- Only uploads from now on have their receipts kept; statements uploaded
-- before can be uploaded again (their payments are all "already recorded").
--
-- Run before deploying: the import saves the receipts after every upload
-- (it carries on without them if this table is missing, and just logs it).
-- Safe to re-run.

IF OBJECT_ID('dbo.PaymentStatementReceipts') IS NULL
BEGIN
    CREATE TABLE dbo.PaymentStatementReceipts
    (
        UploadId  INT          NOT NULL
            CONSTRAINT FK_PaymentStatementReceipts_Upload REFERENCES dbo.PaymentStatementUploads (Id),
        ReceiptNo NVARCHAR(50) NOT NULL,
        CONSTRAINT PK_PaymentStatementReceipts PRIMARY KEY (UploadId, ReceiptNo)
    );
    CREATE INDEX IX_PaymentStatementReceipts_ReceiptNo ON dbo.PaymentStatementReceipts (ReceiptNo);
END
GO
