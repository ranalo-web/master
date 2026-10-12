using System.Data;
using System.Data.SqlClient;
using Dapper;
using Ranalo.Models;

namespace Ranalo.DataStore
{
    // Lost & Stolen reports and their evidence files
    // (Database/LostStolen/001). The rules live in Services.LostStolenRules.
    public interface ILostStolenRepository
    {
        // open = Pending and Confirmed; otherwise Rejected and Closed.
        Task<List<LostStolenReport>> GetReportsAsync(bool open);
        Task<LostStolenReport?> GetReportAsync(int reportId);
        Task<Dictionary<long, LostStolenAccountScope>> GetScopesAsync(IReadOnlyCollection<long> accountNos);

        // Null when the account already has an open report.
        Task<int?> CreateAsync(NewLostStolenReport report, int reportedByUserId, string reportedByRole);
        Task AddEvidenceAsync(int reportId, string fileName, string contentType, byte[] content, int uploadedByUserId);
        Task<LostStolenEvidenceFile?> GetEvidenceFileAsync(int evidenceId);

        // Each moves the report only from the state it expects; false when it
        // had already moved (double click, two admins).
        Task<bool> ConfirmAsync(int reportId, int adminUserId, string? note);
        Task<bool> RejectAsync(int reportId, int adminUserId, string? note);
        Task<bool> CloseAsync(int reportId, string outcome, int adminUserId, string? note);
        Task RecordLockAsync(int reportId, string result);

        // Accounts the nightly lock jobs must leave locked. Empty until
        // Database/LostStolen/001 has run.
        Task<HashSet<long>> GetLockedAccountNosAsync();
    }

    public class LostStolenRepository : ILostStolenRepository
    {
        private readonly IDbConnection _db;

        public LostStolenRepository(IDbConnection db)
        {
            _db = db;
        }

        private const string ReportSelect = @"
                SELECT r.Id, r.AccountNo, ci.First_Name AS CustomerName,
                       NULLIF(LTRIM(RTRIM(ISNULL(d.Make, '') + ' ' + ISNULL(d.Model, ''))), '') AS ProductName,
                       dl.DealerId, dl.CompanyName AS DealerName,
                       r.Kind, CAST(r.IncidentDate AS DATETIME2) AS IncidentDate, r.PoliceRef, r.Description,
                       r.ReportedByUserId, LTRIM(RTRIM(ISNULL(ru.[Name], '') + ' ' + ISNULL(ru.[LastName], ''))) AS ReportedByName,
                       r.ReportedByRole, r.ReportedAtUtc, r.Status,
                       LTRIM(RTRIM(ISNULL(du.[Name], '') + ' ' + ISNULL(du.[LastName], ''))) AS DecidedByName,
                       r.DecidedAtUtc, r.DecisionNote, r.LockSentAtUtc, r.LockResult, r.Outcome,
                       LTRIM(RTRIM(ISNULL(cu.[Name], '') + ' ' + ISNULL(cu.[LastName], ''))) AS ClosedByName,
                       r.ClosedAtUtc, r.ClosedNote,
                       CAST(d.LastConnectedAt AS NVARCHAR(50)) AS LastConnectedAt
                FROM LostStolenReports r
                LEFT JOIN Devices d ON d.Id = r.AccountNo
                LEFT JOIN Dealers dl ON dl.DealerReference = d.DeviceGroupId
                OUTER APPLY (SELECT TOP 1 c.First_Name FROM Contract_Info c WHERE c.ID = r.AccountNo
                             ORDER BY CASE WHEN c.EndDate IS NULL THEN 0 ELSE 1 END, c.StartDate DESC) ci
                LEFT JOIN Users ru ON ru.UserId = r.ReportedByUserId
                LEFT JOIN Users du ON du.UserId = r.DecidedByUserId
                LEFT JOIN Users cu ON cu.UserId = r.ClosedByUserId";

        private const string EvidenceSelect = @"
                SELECT Id, ReportId, FileName, ContentType, SizeBytes, UploadedAtUtc
                FROM LostStolenEvidence";

        public async Task<List<LostStolenReport>> GetReportsAsync(bool open)
        {
            var sql = ReportSelect + @"
                WHERE (@Open = 1 AND r.Status IN ('Pending', 'Confirmed'))
                   OR (@Open = 0 AND r.Status IN ('Rejected', 'Closed'))
                ORDER BY CASE r.Status WHEN 'Pending' THEN 0 WHEN 'Confirmed' THEN 1 ELSE 2 END, r.ReportedAtUtc DESC";

            var reports = (await _db.QueryAsync<LostStolenReport>(sql, new { Open = open ? 1 : 0 })).ToList();
            if (reports.Count == 0) return reports;

            var evidence = (await _db.QueryAsync<LostStolenEvidence>(EvidenceSelect + " WHERE ReportId IN @Ids ORDER BY UploadedAtUtc",
                new { Ids = reports.Select(r => r.Id).ToArray() })).ToLookup(e => e.ReportId);
            foreach (var r in reports) r.Evidence = evidence[r.Id].ToList();
            return reports;
        }

        public async Task<LostStolenReport?> GetReportAsync(int reportId)
        {
            var report = await _db.QueryFirstOrDefaultAsync<LostStolenReport>(ReportSelect + " WHERE r.Id = @Id", new { Id = reportId });
            if (report != null)
            {
                report.Evidence = (await _db.QueryAsync<LostStolenEvidence>(EvidenceSelect + " WHERE ReportId = @Id ORDER BY UploadedAtUtc",
                    new { Id = reportId })).ToList();
            }
            return report;
        }

        public async Task<Dictionary<long, LostStolenAccountScope>> GetScopesAsync(IReadOnlyCollection<long> accountNos)
        {
            const string sql = @"
                SELECT a.AccountNo, CAST(CASE WHEN d.Id IS NULL THEN 0 ELSE 1 END AS BIT) AS HasDevice,
                       dl.DealerId, ci.AssignedAgentId, h.CollectorUserId, d.LockGroup
                FROM (SELECT DISTINCT CAST(value AS BIGINT) AS AccountNo FROM STRING_SPLIT(@Ids, ',')) a
                LEFT JOIN Devices d ON d.Id = a.AccountNo
                LEFT JOIN Dealers dl ON dl.DealerReference = d.DeviceGroupId
                OUTER APPLY (SELECT TOP 1 c.AssignedAgentId FROM Contract_Info c
                             WHERE c.ID = a.AccountNo AND c.EndDate IS NULL ORDER BY c.StartDate DESC) ci
                OUTER APPLY (SELECT TOP 1 x.CollectorUserId
                             FROM CollectionAssignments x
                             INNER JOIN CollectionCases cc ON cc.Id = x.CaseId
                             WHERE cc.AccountNo = a.AccountNo AND cc.Status = 'Open' AND x.EndAt IS NULL) h";

            var result = new Dictionary<long, LostStolenAccountScope>();
            foreach (var chunk in accountNos.Distinct().Chunk(500))
            {
                foreach (var s in await _db.QueryAsync<LostStolenAccountScope>(sql, new { Ids = string.Join(",", chunk) }))
                {
                    result[s.AccountNo] = s;
                }
            }
            return result;
        }

        public async Task<int?> CreateAsync(NewLostStolenReport report, int reportedByUserId, string reportedByRole)
        {
            const string sql = @"
                INSERT INTO LostStolenReports (AccountNo, Kind, IncidentDate, PoliceRef, Description, ReportedByUserId, ReportedByRole)
                OUTPUT INSERTED.Id
                VALUES (@AccountNo, @Kind, @IncidentDate, @PoliceRef, @Description, @ReportedByUserId, @ReportedByRole)";

            try
            {
                return await _db.ExecuteScalarAsync<int>(sql, new
                {
                    report.AccountNo,
                    report.Kind,
                    IncidentDate = report.IncidentDate!.Value.Date,
                    PoliceRef = string.IsNullOrWhiteSpace(report.PoliceRef) ? null : report.PoliceRef.Trim(),
                    Description = report.Description!.Trim(),
                    ReportedByUserId = reportedByUserId,
                    ReportedByRole = reportedByRole,
                });
            }
            catch (SqlException ex) when (ex.Number is 2601 or 2627)
            {
                return null;
            }
        }

        public async Task AddEvidenceAsync(int reportId, string fileName, string contentType, byte[] content, int uploadedByUserId)
        {
            const string sql = @"
                INSERT INTO LostStolenEvidence (ReportId, FileName, ContentType, SizeBytes, Content, UploadedByUserId)
                VALUES (@ReportId, @FileName, @ContentType, @SizeBytes, @Content, @UploadedByUserId)";

            await _db.ExecuteAsync(sql, new
            {
                ReportId = reportId,
                FileName = fileName,
                ContentType = contentType,
                SizeBytes = content.Length,
                Content = content,
                UploadedByUserId = uploadedByUserId,
            });
        }

        public async Task<LostStolenEvidenceFile?> GetEvidenceFileAsync(int evidenceId)
        {
            const string sql = @"
                SELECT Id, ReportId, FileName, ContentType, SizeBytes, UploadedAtUtc, Content
                FROM LostStolenEvidence WHERE Id = @Id";
            return await _db.QueryFirstOrDefaultAsync<LostStolenEvidenceFile>(sql, new { Id = evidenceId });
        }

        public async Task<bool> ConfirmAsync(int reportId, int adminUserId, string? note) =>
            await _db.ExecuteAsync(@"
                UPDATE LostStolenReports
                SET Status = 'Confirmed', DecidedByUserId = @By, DecidedAtUtc = SYSUTCDATETIME(), DecisionNote = @Note
                WHERE Id = @Id AND Status = 'Pending'",
                new { Id = reportId, By = adminUserId, Note = note }) == 1;

        public async Task<bool> RejectAsync(int reportId, int adminUserId, string? note) =>
            await _db.ExecuteAsync(@"
                UPDATE LostStolenReports
                SET Status = 'Rejected', DecidedByUserId = @By, DecidedAtUtc = SYSUTCDATETIME(), DecisionNote = @Note
                WHERE Id = @Id AND Status = 'Pending'",
                new { Id = reportId, By = adminUserId, Note = note }) == 1;

        public async Task<bool> CloseAsync(int reportId, string outcome, int adminUserId, string? note) =>
            await _db.ExecuteAsync(@"
                UPDATE LostStolenReports
                SET Status = 'Closed', Outcome = @Outcome, ClosedByUserId = @By, ClosedAtUtc = SYSUTCDATETIME(), ClosedNote = @Note
                WHERE Id = @Id AND Status = 'Confirmed'",
                new { Id = reportId, Outcome = outcome, By = adminUserId, Note = note }) == 1;

        public async Task RecordLockAsync(int reportId, string result) =>
            await _db.ExecuteAsync(@"
                UPDATE LostStolenReports SET LockSentAtUtc = SYSUTCDATETIME(), LockResult = @Result WHERE Id = @Id",
                new { Id = reportId, Result = result.Length > 500 ? result[..500] : result });

        public async Task<HashSet<long>> GetLockedAccountNosAsync()
        {
            const string sql = @"
                IF OBJECT_ID('dbo.LostStolenReports') IS NOT NULL
                    SELECT AccountNo FROM LostStolenReports WHERE Status = 'Confirmed'
                ELSE
                    SELECT CAST(NULL AS BIGINT) AS AccountNo WHERE 1 = 0";
            return (await _db.QueryAsync<long>(sql)).ToHashSet();
        }
    }
}
