-- CollectionCallNotes: a collector's note after speaking to the customer
-- ("Spoke to customer" on My Collections and Customer Details), with an
-- optional date the customer promised to pay. Can't-reach notes stay in
-- CollectionFlags, where the admin team resolves them.
--
-- Until this has run the pages still open; they just can't save or show
-- call notes.
--
-- Safe to re-run: every step is guarded.

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CollectionCallNotes')
BEGIN
    CREATE TABLE CollectionCallNotes
    (
        Id              INT IDENTITY(1,1) PRIMARY KEY,
        CaseId          INT NOT NULL,
        CollectorUserId INT NOT NULL,
        Note            NVARCHAR(500) NOT NULL,
        PromisedDate    DATE NULL,
        CreatedAt       DATETIME2 NOT NULL DEFAULT GETDATE()
    );

    CREATE INDEX IX_CollectionCallNotes_Case ON CollectionCallNotes (CaseId, CreatedAt);
END
GO
