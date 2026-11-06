-- M-Pesa Express (STK push) prompts sent from the customer app, and what
-- became of each (MpesaStkClient, SafaricomStkController). Safaricom's
-- result carries only the CheckoutRequestID, so this is how a payment is
-- matched back to its account. The payment itself goes into KosePayments
-- like any paybill payment (by M-Pesa code, so never twice).
--
-- Run before switching SafaricomStk:Enabled on. Safe to re-run.

IF OBJECT_ID('dbo.StkRequests') IS NULL
BEGIN
    CREATE TABLE dbo.StkRequests
    (
        Id                INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_StkRequests PRIMARY KEY,
        CheckoutRequestId NVARCHAR(100) NOT NULL,
        MerchantRequestId NVARCHAR(100) NULL,
        AccountNo         BIGINT        NOT NULL,
        Phone             NVARCHAR(20)  NOT NULL,
        Amount            DECIMAL(18,2) NOT NULL,
        Status            NVARCHAR(20)  NOT NULL CONSTRAINT DF_StkRequests_Status DEFAULT 'Sent', -- Sent, Paid, Failed
        ResultCode        INT           NULL,
        ResultDesc        NVARCHAR(400) NULL,
        MpesaReceipt      NVARCHAR(50)  NULL,
        CreatedAtUtc      DATETIME2     NOT NULL,
        CompletedAtUtc    DATETIME2     NULL
    );
    CREATE UNIQUE INDEX IX_StkRequests_Checkout ON dbo.StkRequests (CheckoutRequestId);
    CREATE INDEX IX_StkRequests_Account ON dbo.StkRequests (AccountNo, CreatedAtUtc DESC);
END
GO
