-- Approver follow-ups (Services/ApproverFollowUpRules.cs): accounts that
-- reach the collections pool go to the Approvers first. Added and closed
-- by ScheduledApproverFollowUps from the facts; Approvers take them, log
-- calls, and send them to collections when they can't sort them out.
--
-- At most one Open or ToCollections row per contract, held by the index.
-- Needs an admin login. Safe to re-run.

IF OBJECT_ID('dbo.ApproverFollowUps') IS NULL
BEGIN
    CREATE TABLE dbo.ApproverFollowUps
    (
        Id                       INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ApproverFollowUps PRIMARY KEY,
        AccountNo                BIGINT        NOT NULL,
        ContractId               BIGINT        NOT NULL,
        Status                   NVARCHAR(20)  NOT NULL,   -- Open, ToCollections, Closed
        CloseReason              NVARCHAR(40)  NULL,
        AddedAtUtc               DATETIME2     NOT NULL,
        ShortfallAtAdd           DECIMAL(18,2) NOT NULL,
        DaysWithoutPaymentAtAdd  INT           NOT NULL,
        TakenByUserId            INT           NULL,
        TakenAtUtc               DATETIME2     NULL,
        LastNote                 NVARCHAR(500) NULL,
        LastNoteAtUtc            DATETIME2     NULL,
        LastNoteByUserId         INT           NULL,
        PromisedDate             DATE          NULL,
        SentToCollectionsAtUtc   DATETIME2     NULL,
        SentByUserId             INT           NULL,
        ClosedAtUtc              DATETIME2     NULL
    );
    CREATE UNIQUE INDEX UX_ApproverFollowUps_Active ON dbo.ApproverFollowUps (ContractId) WHERE Status IN ('Open', 'ToCollections');
    CREATE INDEX IX_ApproverFollowUps_Status ON dbo.ApproverFollowUps (Status, AddedAtUtc);
END
GO
