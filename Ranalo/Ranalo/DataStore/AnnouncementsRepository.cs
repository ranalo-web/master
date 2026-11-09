using System.Data;
using Dapper;

namespace Ranalo.DataStore
{
    public class AnnouncementRecipientCounts
    {
        public int Nuovo { get; set; }
        public int Knox { get; set; }
        public int Transsion { get; set; }
        public int TvsLeftOut { get; set; }
        public int Total => Nuovo + Knox + Transsion;
    }

    public class AnnouncementHistory
    {
        public string MessageType { get; set; } = "";
        public int Sent { get; set; }
        public int Failed { get; set; }
        public DateTime First { get; set; }
        public DateTime Last { get; set; }
        public string? Sample { get; set; }
    }

    public interface IAnnouncementsRepository
    {
        Task<AnnouncementRecipientCounts> CountRecipientsAsync();
        Task<List<MilestoneAccount>> GetRecipientsAsync(string messageType);
        Task<MilestoneAccount?> GetAccountAsync(long accountNo);
        Task<List<AnnouncementHistory>> HistoryAsync();
        Task<List<MilestoneAccount>> GetFailedRecipientsAsync(string messageType);
    }

    // Who an announcement goes to (AnnouncementRules): every open contract
    // on an enrolled device that isn't a TV -- the same "enrolled" test as
    // the welcome message. Knox logs carry the IMEI, the others the account.
    public class AnnouncementsRepository : IAnnouncementsRepository
    {
        private readonly IDbConnection _db;

        public AnnouncementsRepository(IDbConnection db)
        {
            _db = db;
        }

        private const string Open = @"
                FROM Contract_Info ci
                JOIN Devices d ON d.Id = ci.ID
                WHERE ci.EndDate IS NULL AND d.Status = 'enrolled'";

        public async Task<AnnouncementRecipientCounts> CountRecipientsAsync() =>
            await _db.QueryFirstAsync<AnnouncementRecipientCounts>(@"
                SELECT SUM(CASE WHEN ISNULL(d.IsTv, 0) = 0 AND ISNULL(d.LockGroup, 1) NOT IN (2, 3) THEN 1 ELSE 0 END) AS Nuovo,
                       SUM(CASE WHEN ISNULL(d.IsTv, 0) = 0 AND d.LockGroup = 2 THEN 1 ELSE 0 END) AS Knox,
                       SUM(CASE WHEN ISNULL(d.IsTv, 0) = 0 AND d.LockGroup = 3 THEN 1 ELSE 0 END) AS Transsion,
                       SUM(CASE WHEN ISNULL(d.IsTv, 0) = 1 THEN 1 ELSE 0 END) AS TvsLeftOut" + Open);

        public async Task<List<MilestoneAccount>> GetRecipientsAsync(string messageType) =>
            (await _db.QueryAsync<MilestoneAccount>(@"
                SELECT ci.ID AS AccountId, ci.First_Name AS FirstName, d.ImeiNo AS Imei,
                       ISNULL(d.LockGroup, 1) AS LockGroup, ISNULL(ci.Daily, 0) AS Daily" + Open + @"
                  AND ISNULL(d.IsTv, 0) = 0
                  AND NOT EXISTS (SELECT 1 FROM MessageLogs m
                                  WHERE m.MessageType = @MessageType AND m.MessageStatus = 'sent'
                                    AND m.AccountNo IN (CAST(ci.ID AS NVARCHAR(50)), d.ImeiNo))
                ORDER BY ci.ID", new { MessageType = messageType })).ToList();

        public async Task<MilestoneAccount?> GetAccountAsync(long accountNo) =>
            await _db.QueryFirstOrDefaultAsync<MilestoneAccount>(@"
                SELECT ci.ID AS AccountId, ci.First_Name AS FirstName, d.ImeiNo AS Imei,
                       ISNULL(d.LockGroup, 1) AS LockGroup, ISNULL(ci.Daily, 0) AS Daily
                FROM Contract_Info ci
                JOIN Devices d ON d.Id = ci.ID
                WHERE ci.ID = @AccountNo", new { AccountNo = accountNo });

        // Phones, not log rows: a phone that failed and then got it on a
        // retry counts as sent, not failed.
        public async Task<List<AnnouncementHistory>> HistoryAsync() =>
            (await _db.QueryAsync<AnnouncementHistory>(@"
                SELECT TOP 20 m.MessageType,
                       COUNT(DISTINCT CASE WHEN m.MessageStatus = 'sent' THEN m.AccountNo END) AS Sent,
                       COUNT(DISTINCT CASE WHEN m.MessageStatus = 'failed'
                                            AND NOT EXISTS (SELECT 1 FROM MessageLogs s
                                                            WHERE s.MessageType = m.MessageType AND s.MessageStatus = 'sent'
                                                              AND s.AccountNo = m.AccountNo)
                                           THEN m.AccountNo END) AS Failed,
                       MIN(m.DateSent) AS First, MAX(m.DateSent) AS Last,
                       MAX(LEFT(m.Message, 120)) AS Sample
                FROM MessageLogs m
                WHERE m.MessageType LIKE 'Announcement:%'
                GROUP BY m.MessageType
                ORDER BY MIN(m.DateSent) DESC")).ToList();

        // A retry: only phones this announcement failed for, and still
        // haven't got it -- not customers who joined since.
        public async Task<List<MilestoneAccount>> GetFailedRecipientsAsync(string messageType) =>
            (await _db.QueryAsync<MilestoneAccount>(@"
                SELECT ci.ID AS AccountId, ci.First_Name AS FirstName, d.ImeiNo AS Imei,
                       ISNULL(d.LockGroup, 1) AS LockGroup, ISNULL(ci.Daily, 0) AS Daily" + Open + @"
                  AND ISNULL(d.IsTv, 0) = 0
                  AND EXISTS (SELECT 1 FROM MessageLogs m
                              WHERE m.MessageType = @MessageType AND m.MessageStatus = 'failed'
                                AND m.AccountNo IN (CAST(ci.ID AS NVARCHAR(50)), d.ImeiNo))
                  AND NOT EXISTS (SELECT 1 FROM MessageLogs m
                                  WHERE m.MessageType = @MessageType AND m.MessageStatus = 'sent'
                                    AND m.AccountNo IN (CAST(ci.ID AS NVARCHAR(50)), d.ImeiNo))
                ORDER BY ci.ID", new { MessageType = messageType })).ToList();
    }
}
