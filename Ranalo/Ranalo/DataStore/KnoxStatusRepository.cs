using System.Data;
using Dapper;
using Ranalo.Services;

namespace Ranalo.DataStore
{
    public class KnoxCheckRow
    {
        public long AccountNo { get; set; }
        public string? Imei { get; set; }
        public string? Model { get; set; }
        public string? FirstName { get; set; }
        public int? LockGroup { get; set; }
        public bool? Locked { get; set; }
        public string? KnoxStatus { get; set; }
        public DateTime? KnoxStatusAtUtc { get; set; }
        public DateTime? KnoxLastSeenUtc { get; set; }
        public DateTime? KnoxRelockUtc { get; set; }
        public bool? KnoxOffline { get; set; }
        public int? OpenRecoveryId { get; set; }
        public bool OpenContract { get; set; }

        public KnoxStatusRules.Problem Problem =>
            KnoxStatusRules.Check(LockGroup, KnoxStatus, OpenRecoveryId.HasValue);
    }

    public interface IKnoxStatusRepository
    {
        Task<int> SaveAsync(IReadOnlyList<KnoxStatusRules.KnoxDeviceState> states, DateTime readAtUtc);
        Task<List<KnoxCheckRow>> GetCheckListAsync();
        Task<KnoxCheckRow?> GetAsync(long accountNo);
    }

    // Knox's own status on Devices (Database/DeviceLock/002).
    public class KnoxStatusRepository : IKnoxStatusRepository
    {
        private readonly IDbConnection _db;

        public KnoxStatusRepository(IDbConnection db)
        {
            _db = db;
        }

        // Matched on either IMEI. Our Knox phones (LockGroup 2) that Knox's
        // full list no longer has are marked "Not in Knox". Returns how many
        // of our phones Knox has.
        public async Task<int> SaveAsync(IReadOnlyList<KnoxStatusRules.KnoxDeviceState> states, DateTime readAtUtc)
        {
            var matched = 0;
            foreach (var s in states.Where(s => !string.IsNullOrWhiteSpace(s.Imei)))
            {
                matched += await _db.ExecuteAsync(@"
                    UPDATE Devices
                    SET KnoxStatus = @Status, KnoxStatusAtUtc = @ReadAt, KnoxLastSeenUtc = @LastSeen,
                        KnoxRelockUtc = @Relock, KnoxOffline = @Offline
                    WHERE ImeiNo IN (@Imei, @Imei2) OR ImeiNo2 IN (@Imei, @Imei2)",
                    new { Status = s.Status.Length > 40 ? s.Status[..40] : s.Status, ReadAt = readAtUtc,
                          LastSeen = s.LastSeenUtc, Relock = s.RelockUtc, s.Offline, s.Imei, Imei2 = s.Imei2 ?? s.Imei }) > 0 ? 1 : 0;
            }

            await _db.ExecuteAsync(@"
                UPDATE Devices
                SET KnoxStatus = @NotInKnox, KnoxStatusAtUtc = @ReadAt, KnoxOffline = NULL
                WHERE LockGroup = 2 AND (KnoxStatusAtUtc IS NULL OR KnoxStatusAtUtc < @ReadAt)",
                new { NotInKnox = KnoxStatusRules.NotInKnox, ReadAt = readAtUtc });
            return matched;
        }

        private const string Select = @"
            SELECT d.Id AS AccountNo, d.ImeiNo AS Imei, d.Model, ci.First_Name AS FirstName,
                   d.LockGroup, d.Locked, d.KnoxStatus, d.KnoxStatusAtUtc, d.KnoxLastSeenUtc, d.KnoxRelockUtc, d.KnoxOffline,
                   (SELECT TOP 1 r.Id FROM LockRecoveries r
                    WHERE r.AccountNo = d.Id AND r.Status NOT IN ('MovedToKnox', 'MovedToTranssion', 'NotCatchable', 'Closed')
                    ORDER BY r.Id DESC) AS OpenRecoveryId,
                   CAST(CASE WHEN ci.ID IS NULL THEN 0 ELSE 1 END AS BIT) AS OpenContract
            FROM Devices d
            OUTER APPLY (SELECT TOP 1 c.ID, c.First_Name FROM Contract_Info c
                         WHERE c.ID = d.Id AND c.EndDate IS NULL ORDER BY c.ContractID DESC) ci";

        // Every phone Knox has, or that we send to Knox.
        public async Task<List<KnoxCheckRow>> GetCheckListAsync() =>
            (await _db.QueryAsync<KnoxCheckRow>(Select + @"
                WHERE d.LockGroup = 2 OR (d.KnoxStatus IS NOT NULL AND d.KnoxStatus <> @NotInKnox)
                ORDER BY d.Id", new { NotInKnox = KnoxStatusRules.NotInKnox })).ToList();

        public async Task<KnoxCheckRow?> GetAsync(long accountNo) =>
            await _db.QueryFirstOrDefaultAsync<KnoxCheckRow>(Select + " WHERE d.Id = @AccountNo", new { AccountNo = accountNo });
    }
}
