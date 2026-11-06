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
        Task<bool> SendToCollectionsAsync(int id, int userId);
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

        public async Task<bool> NoteAsync(int id, int userId, string note, DateTime? promised) =>
            await _db.ExecuteAsync(@"
                UPDATE ApproverFollowUps
                SET LastNote = LEFT(@Note, 500), LastNoteAtUtc = SYSUTCDATETIME(), LastNoteByUserId = @UserId, PromisedDate = @Promised,
                    TakenByUserId = COALESCE(TakenByUserId, @UserId), TakenAtUtc = COALESCE(TakenAtUtc, SYSUTCDATETIME())
                WHERE Id = @Id AND Status = 'Open'", new { Id = id, UserId = userId, Note = note, Promised = promised }) == 1;

        public async Task<bool> SendToCollectionsAsync(int id, int userId) =>
            await _db.ExecuteAsync(@"
                UPDATE ApproverFollowUps SET Status = 'ToCollections', SentToCollectionsAtUtc = SYSUTCDATETIME(), SentByUserId = @UserId
                WHERE Id = @Id AND Status = 'Open'", new { Id = id, UserId = userId }) == 1;
    }
}
