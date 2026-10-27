using System.Data;
using Dapper;

namespace Ranalo.DataStore
{
    public class MilestoneAccount
    {
        public long AccountId { get; set; }
        public string? FirstName { get; set; }
        public string? Imei { get; set; }
        public int LockGroup { get; set; }
        public decimal Daily { get; set; }
    }

    public interface IMilestoneMessagesRepository
    {
        Task<List<MilestoneAccount>> GetDueWelcomesAsync(DateTime createdSince, int maxFailures);
        Task<List<MilestoneAccount>> GetDueFullyPaidAsync(DateTime endedSince, int maxFailures);
    }

    // The accounts owed a one-off message (ScheduledMilestoneMessages).
    // "Owed" = no MessageLogs row of that type marked sent, and fewer than
    // maxFailures failed tries. Knox messages are logged by IMEI, the others
    // by account number, so both are checked.
    public class MilestoneMessagesRepository : IMilestoneMessagesRepository
    {
        public const string WelcomeType = "Welcome";
        public const string FullyPaidType = "FullyPaid";

        private readonly IDbConnection _db;

        public MilestoneMessagesRepository(IDbConnection db)
        {
            _db = db;
        }

        private const string NotYetSent = @"
                  AND NOT EXISTS (SELECT 1 FROM MessageLogs m
                                  WHERE m.MessageType = @MessageType AND m.MessageStatus = 'sent'
                                    AND m.AccountNo IN (CAST(ci.ID AS NVARCHAR(50)), d.ImeiNo))
                  AND (SELECT COUNT(*) FROM MessageLogs m
                        WHERE m.MessageType = @MessageType AND m.MessageStatus = 'failed'
                          AND m.AccountNo IN (CAST(ci.ID AS NVARCHAR(50)), d.ImeiNo)) < @MaxFailures";

        // A contract that has started, on a phone that is enrolled (for
        // Transsion: activated and unlocked).
        public async Task<List<MilestoneAccount>> GetDueWelcomesAsync(DateTime createdSince, int maxFailures)
        {
            var rows = await _db.QueryAsync<MilestoneAccount>(@"
                SELECT ci.ID AS AccountId, ci.First_Name AS FirstName, d.ImeiNo AS Imei,
                       ISNULL(d.LockGroup, 1) AS LockGroup, ISNULL(ci.Daily, 0) AS Daily
                FROM Contract_Info ci
                JOIN Devices d ON d.Id = ci.ID
                WHERE ci.Created >= @Since AND ci.EndDate IS NULL AND d.Status = 'enrolled'" + NotYetSent,
                new { Since = createdSince, MessageType = WelcomeType, MaxFailures = maxFailures });
            return rows.ToList();
        }

        // A contract ended because it was paid off: what's been paid on the
        // account covers deposit plus the whole term, less LockCheckRules'
        // tolerance -- the same test as LockCheckRepository's end-contract SQL.
        public async Task<List<MilestoneAccount>> GetDueFullyPaidAsync(DateTime endedSince, int maxFailures)
        {
            var rows = await _db.QueryAsync<MilestoneAccount>(@"
                SELECT ci.ID AS AccountId, ci.First_Name AS FirstName, d.ImeiNo AS Imei,
                       ISNULL(d.LockGroup, 1) AS LockGroup, ISNULL(ci.Daily, 0) AS Daily
                FROM Contract_Info ci
                JOIN Devices d ON d.Id = ci.ID
                WHERE ci.EndDate >= @Since
                  AND (SELECT ISNULL(SUM(kp.AmountValue), 0)
                       FROM KosePayments kp
                       LEFT JOIN OrphanedPayments op ON op.MpesaCode = kp.MpesaCode
                       WHERE COALESCE(op.AccountNoBigint, kp.AccountNoBigint) = ci.ID)
                      >= ci.Deposit + ci.Daily * 30 * ci.Term_in_Months
                         + ci.Weekly * (30.0 / 7.0) * ci.Term_in_Months + ci.Monthly * ci.Term_in_Months - @Tolerance" + NotYetSent,
                new { Since = endedSince, MessageType = FullyPaidType, MaxFailures = maxFailures,
                      Tolerance = Ranalo.Services.LockCheckRules.PaidTolerance });
            return rows.ToList();
        }
    }
}
