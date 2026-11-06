using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Ranalo.Services;

namespace Ranalo.DataStore
{
    public class ApproverFollowUp
    {
        public int Id { get; set; }
        public long AccountNo { get; set; }
        public long ContractId { get; set; }
        public string Status { get; set; } = ApproverFollowUpRules.Open;
        public string? CloseReason { get; set; }
        public DateTime AddedAtUtc { get; set; }
        public decimal ShortfallAtAdd { get; set; }
        public int DaysWithoutPaymentAtAdd { get; set; }
        public int? TakenByUserId { get; set; }
        public string? TakenBy { get; set; }
        public DateTime? TakenAtUtc { get; set; }
        public string? LastNote { get; set; }
        public DateTime? LastNoteAtUtc { get; set; }
        public string? LastNoteBy { get; set; }
        public DateTime? PromisedDate { get; set; }
        public DateTime? SentToCollectionsAtUtc { get; set; }
        public string? SentBy { get; set; }
        public DateTime? ClosedAtUtc { get; set; }
    }

    // Database/Collections/003 (ApproverFollowUpRules).
    public interface IApproverFollowUpRepository
    {
        Task<List<ApproverFollowUp>> GetActiveAsync();
        Task<List<ApproverFollowUp>> ListAsync(string status);
        Task<bool> AddAsync(long accountNo, long contractId, decimal shortfall, int daysWithoutPayment);
        Task CloseAsync(int id, string reason);
        Task<bool> TakeAsync(int id, int userId);
        Task<bool> NoteAsync(int id, int userId, string note, DateTime? promised);
        Task<bool> SendToCollectionsAsync(int id, int userId, string? note);
        Task<Dictionary<int, List<FollowUpNote>>> GetNotesAsync(IReadOnlyCollection<int> followUpIds);
        Task<int> CopyNotesToCaseAsync(int followUpId, int caseId);
    }

    public class ApproverFollowUpRepository : IApproverFollowUpRepository
    {
        private readonly IDbConnection _db;

        public ApproverFollowUpRepository(IDbConnection db)
        {
            _db = db;
        }

        private const string Select = @"
            SELECT f.*, tu.KnownAs AS TakenBy, nu.KnownAs AS LastNoteBy, su.KnownAs AS SentBy
            FROM ApproverFollowUps f
            LEFT JOIN Users tu ON tu.UserId = f.TakenByUserId
            LEFT JOIN Users nu ON nu.UserId = f.LastNoteByUserId
            LEFT JOIN Users su ON su.UserId = f.SentByUserId";

        public async Task<List<ApproverFollowUp>> GetActiveAsync() =>
            (await _db.QueryAsync<ApproverFollowUp>(Select + " WHERE f.Status IN ('Open', 'ToCollections')")).ToList();

        public async Task<List<ApproverFollowUp>> ListAsync(string status) =>
            (await _db.QueryAsync<ApproverFollowUp>(Select + @"
                WHERE f.Status = @Status AND (@Status <> 'Closed' OR f.ClosedAtUtc >= DATEADD(DAY, -30, SYSUTCDATETIME()))
                ORDER BY f.AddedAtUtc", new { Status = status })).ToList();

        // False if the contract already has an active one (the index holds it).
        public async Task<bool> AddAsync(long accountNo, long contractId, decimal shortfall, int daysWithoutPayment)
        {
            try
            {
                await _db.ExecuteAsync(@"
                    INSERT INTO ApproverFollowUps (AccountNo, ContractId, Status, AddedAtUtc, ShortfallAtAdd, DaysWithoutPaymentAtAdd)
                    VALUES (@AccountNo, @ContractId, 'Open', SYSUTCDATETIME(), @Shortfall, @Days)",
                    new { AccountNo = accountNo, ContractId = contractId, Shortfall = shortfall, Days = daysWithoutPayment });
                return true;
            }
            catch (SqlException ex) when (ex.Number is 2601 or 2627)
            {
                return false;
            }
        }

        public async Task CloseAsync(int id, string reason) =>
            await _db.ExecuteAsync(@"
                UPDATE ApproverFollowUps SET Status = 'Closed', CloseReason = @Reason, ClosedAtUtc = SYSUTCDATETIME()
                WHERE Id = @Id AND Status IN ('Open', 'ToCollections')", new { Id = id, Reason = reason });

        public async Task<bool> TakeAsync(int id, int userId) =>
            await _db.ExecuteAsync(@"
                UPDATE ApproverFollowUps SET TakenByUserId = @UserId, TakenAtUtc = SYSUTCDATETIME()
                WHERE Id = @Id AND Status = 'Open'", new { Id = id, UserId = userId }) == 1;

        // Latest note on the row, and every note in the history
        // (Collections/004) -- the history is what the collector gets.
        public async Task<bool> NoteAsync(int id, int userId, string note, DateTime? promised) =>
            await _db.ExecuteScalarAsync<int>(@"
                UPDATE ApproverFollowUps
                SET LastNote = LEFT(@Note, 500), LastNoteAtUtc = SYSUTCDATETIME(), LastNoteByUserId = @UserId, PromisedDate = @Promised,
                    TakenByUserId = COALESCE(TakenByUserId, @UserId), TakenAtUtc = COALESCE(TakenAtUtc, SYSUTCDATETIME())
                WHERE Id = @Id AND Status = 'Open';
                IF @@ROWCOUNT = 1
                BEGIN
                    INSERT INTO ApproverFollowUpNotes (FollowUpId, Note, PromisedDate, ByUserId, AtUtc)
                    VALUES (@Id, LEFT(@Note, 500), @Promised, @UserId, SYSUTCDATETIME());
                    SELECT 1;
                END
                ELSE SELECT 0;", new { Id = id, UserId = userId, Note = note, Promised = promised }) == 1;

        // The note is optional; when given it goes into the history too.
        public async Task<bool> SendToCollectionsAsync(int id, int userId, string? note) =>
            await _db.ExecuteScalarAsync<int>(@"
                UPDATE ApproverFollowUps SET Status = 'ToCollections', SentToCollectionsAtUtc = SYSUTCDATETIME(), SentByUserId = @UserId,
                       LastNote = COALESCE(LEFT(@Note, 500), LastNote),
                       LastNoteAtUtc = CASE WHEN @Note IS NULL THEN LastNoteAtUtc ELSE SYSUTCDATETIME() END,
                       LastNoteByUserId = CASE WHEN @Note IS NULL THEN LastNoteByUserId ELSE @UserId END
                WHERE Id = @Id AND Status = 'Open';
                IF @@ROWCOUNT = 1
                BEGIN
                    IF @Note IS NOT NULL
                        INSERT INTO ApproverFollowUpNotes (FollowUpId, Note, ByUserId, AtUtc)
                        VALUES (@Id, LEFT('Sent to collections: ' + @Note, 500), @UserId, SYSUTCDATETIME());
                    SELECT 1;
                END
                ELSE SELECT 0;", new { Id = id, UserId = userId, Note = note }) == 1;

        public async Task<Dictionary<int, List<FollowUpNote>>> GetNotesAsync(IReadOnlyCollection<int> followUpIds) =>
            followUpIds.Count == 0 ? new()
            : (await _db.QueryAsync<FollowUpNote>(@"
                SELECT n.FollowUpId, n.Note, CAST(n.PromisedDate AS DATETIME2) AS PromisedDate, n.AtUtc, u.KnownAs AS By
                FROM ApproverFollowUpNotes n LEFT JOIN Users u ON u.UserId = n.ByUserId
                WHERE n.FollowUpId IN @Ids ORDER BY n.AtUtc", new { Ids = followUpIds }))
                .GroupBy(n => n.FollowUpId).ToDictionary(g => g.Key, g => g.ToList());

        // Every approver note goes onto the collections case, by its author
        // and time, marked as the approvers'. Once only (CopiedToCaseId).
        public async Task<int> CopyNotesToCaseAsync(int followUpId, int caseId) =>
            await _db.ExecuteScalarAsync<int>(@"
                UPDATE ApproverFollowUps SET CopiedToCaseId = @CaseId WHERE Id = @Id AND CopiedToCaseId IS NULL;
                IF @@ROWCOUNT = 1 AND OBJECT_ID('dbo.CollectionCallNotes') IS NOT NULL
                BEGIN
                    INSERT INTO CollectionCallNotes (CaseId, CollectorUserId, Note, PromisedDate, CreatedAt)
                    SELECT @CaseId, n.ByUserId, LEFT('[Approvers] ' + n.Note, 500), n.PromisedDate, n.AtUtc
                    FROM ApproverFollowUpNotes n WHERE n.FollowUpId = @Id ORDER BY n.AtUtc;
                    SELECT @@ROWCOUNT;
                END
                ELSE SELECT 0;", new { Id = followUpId, CaseId = caseId });
    }

    public class FollowUpNote
    {
        public int FollowUpId { get; set; }
        public string Note { get; set; } = "";
        public DateTime? PromisedDate { get; set; }
        public DateTime AtUtc { get; set; }
        public string? By { get; set; }
    }
}
