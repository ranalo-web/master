-- Every note the Approvers write on a follow-up (ApproverFollowUps keeps
-- only the latest). When the account goes to a collector, the notes are
-- copied into the case's CollectionCallNotes, by their author and time, so
-- the collector sees what's been tried (CopiedToCaseId: done, never twice).
--
-- Run after 003. Needs an admin login. Safe to re-run.

IF OBJECT_ID('dbo.ApproverFollowUpNotes') IS NULL
BEGIN
    CREATE TABLE dbo.ApproverFollowUpNotes
    (
        Id           INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ApproverFollowUpNotes PRIMARY KEY,
        FollowUpId   INT           NOT NULL CONSTRAINT FK_ApproverFollowUpNotes_FollowUp REFERENCES dbo.ApproverFollowUps (Id),
        Note         NVARCHAR(500) NOT NULL,
        PromisedDate DATE          NULL,
        ByUserId     INT           NOT NULL,
        AtUtc        DATETIME2     NOT NULL
    );
    CREATE INDEX IX_ApproverFollowUpNotes_FollowUp ON dbo.ApproverFollowUpNotes (FollowUpId, AtUtc);
END

IF COL_LENGTH('dbo.ApproverFollowUps', 'CopiedToCaseId') IS NULL
    ALTER TABLE dbo.ApproverFollowUps ADD CopiedToCaseId INT NULL;
GO
