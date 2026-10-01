using System.Data;
using Dapper;
using Ranalo.Models;

namespace Ranalo.DataStore
{
    // Collections cases, collector stints, flags and collector payouts
    // (Database/Collections/001). The rules live in Services.CollectionsRules;
    // Services.CollectionsService decides, this only reads and writes.
    public interface ICollectionsRepository
    {
        // Open contracts with today's shortfall and last payment. accountNos
        // null = every open contract.
        Task<List<CollectionContractStanding>> GetStandingsAsync(IReadOnlyCollection<long>? accountNos = null);

        Task<List<CollectionCaseRow>> GetCasesAsync();

        // Every collector stint, with the device payments made during it.
        Task<List<CollectionPeriodRow>> GetPeriodsAsync(int? collectorUserId = null, int? caseId = null);

        // (CaseId, CollectorUserId) -> paid to that collector on that case.
        Task<Dictionary<(int CaseId, int CollectorUserId), decimal>> GetCollectorPaidAsync(int? collectorUserId = null);

        Task<List<CollectionFlagRow>> GetFlagsAsync(bool openOnly);

        Task<List<CollectorOption>> GetCollectorsAsync();

        // Opens a case and hands it to the collector. supersede: the device's
        // open case on an older contract, closed with its recovery snapshot.
        Task<int> OpenCaseAsync(NewCollectionCase newCase, CaseClosure? supersede);

        Task ReassignAsync(int caseId, long accountNo, long contractId, int collectorUserId, int changedByUserId);

        Task ReturnAsync(CaseClosure closure);

        Task AddFlagAsync(int caseId, int collectorUserId, string note);

        Task<bool> ResolveFlagAsync(int flagId, int resolvedByUserId);
    }

    public class NewCollectionCase
    {
        public long AccountNo { get; set; }
        public long ContractId { get; set; }
        public int? DealerId { get; set; }
        public int? AgentUserId { get; set; }
        public decimal ShortfallAtHandover { get; set; }
        public decimal FrozenDeduction { get; set; }
        public decimal TotalPaidAtHandover { get; set; }
        public decimal CommissionPaidBasis { get; set; }
        public int CollectorUserId { get; set; }
        public int OpenedByUserId { get; set; }
    }

    // Closing a case: returned to the agent/dealer, or superseded by the
    // device's newer contract going to collections.
    public class CaseClosure
    {
        public int CaseId { get; set; }
        public long AccountNo { get; set; }
        public long ContractId { get; set; }
        public string Status { get; set; } = CollectionCaseStatus.Returned;
        public decimal Recovered { get; set; }
        public decimal CollectorEarned { get; set; }
        public decimal? ShortfallAtReturn { get; set; }
        public decimal? TotalPaidAtReturn { get; set; }
        public int ClosedByUserId { get; set; }
    }

    public class CollectionsRepository : ICollectionsRepository
    {
        private readonly IDbConnection _db;

        public CollectionsRepository(IDbConnection db)
        {
            _db = db;
        }

        internal const string StandingsSql = @"
                ;WITH ValidPayments AS (
                    SELECT COALESCE(op.AccountNoBigint, kp.AccountNoBigint) AS AccountNo, kp.AmountValue, kp.PaymentDateValue
                    FROM KosePayments kp
                    LEFT JOIN OrphanedPayments op ON op.MpesaCode = kp.MpesaCode
                ),
                PaymentTotals AS (
                    SELECT AccountNo, SUM(AmountValue) AS TotalPaid, MAX(PaymentDateValue) AS LastPaymentDate
                    FROM ValidPayments
                    GROUP BY AccountNo
                )
                SELECT
                    ci.ID AS AccountNo,
                    ci.ContractID AS ContractId,
                    ci.First_Name AS CustomerName,
                    dl.DealerId,
                    dl.CompanyName AS DealerName,
                    ci.AssignedAgentId AS AgentUserId,
                    au.[Name] + ' ' + ISNULL(au.[LastName], '') AS AgentName,
                    NULLIF(LTRIM(RTRIM(ISNULL(d.Make, '') + ' ' + ISNULL(d.Model, ''))), '') AS ProductName,
                    ISNULL(pt.TotalPaid, 0) AS TotalPaid,
                    CAST(CASE WHEN x.Arrears < 0 THEN -x.Arrears ELSE 0 END AS DECIMAL(18,2)) AS Shortfall,
                    pt.LastPaymentDate,
                    DATEDIFF(DAY, COALESCE(pt.LastPaymentDate, ci.StartDate), GETDATE()) AS DaysSinceLastPayment,
                    ci.DebtCollectorUserId AS LegacyCollectorUserId,
                    oc.Id AS OpenCaseId
                FROM Contract_Info ci
                LEFT JOIN Devices d ON d.Id = ci.ID
                LEFT JOIN Dealers dl ON dl.DealerReference = d.DeviceGroupId
                LEFT JOIN Users au ON au.UserId = ci.AssignedAgentId
                LEFT JOIN PaymentTotals pt ON pt.AccountNo = ci.ID
                LEFT JOIN CollectionCases oc ON oc.AccountNo = ci.ID AND oc.Status = 'Open'
                CROSS APPLY (
                    SELECT CASE
                        WHEN DATEDIFF(DAY, ci.StartDate, GETDATE()) < CAST(ci.Term_in_Months * 30 AS INT)
                            THEN DATEDIFF(DAY, ci.StartDate, GETDATE())
                        ELSE CAST(ci.Term_in_Months * 30 AS INT)
                    END AS Days
                ) DaysAccrued
                CROSS APPLY (
                    SELECT ISNULL(pt.TotalPaid, 0)
                        - (ci.Deposit
                           + ci.Daily * DaysAccrued.Days
                           + ci.Weekly * (DaysAccrued.Days / 7.0)
                           + ci.Monthly * (DaysAccrued.Days / 30.0)) AS Arrears
                ) x
                WHERE ci.StartDate IS NOT NULL AND ci.EndDate IS NULL
                  AND (@All = 1 OR ci.ID IN @Ids)
                OPTION (RECOMPILE)";

        internal const string PeriodsSql = @"
                ;WITH P AS (
                    SELECT a.Id AS AssignmentId, a.CaseId, a.CollectorUserId, a.StartAt, a.EndAt, c.AccountNo,
                           u.[Name] + ' ' + ISNULL(u.[LastName], '') AS CollectorName
                    FROM CollectionAssignments a
                    INNER JOIN CollectionCases c ON c.Id = a.CaseId
                    LEFT JOIN Users u ON u.UserId = a.CollectorUserId
                    WHERE (@CollectorUserId IS NULL OR a.CollectorUserId = @CollectorUserId)
                      AND (@CaseId IS NULL OR a.CaseId = @CaseId)
                ),
                Dev AS (
                    SELECT DISTINCT AccountNo, CAST(AccountNo AS VARCHAR(30)) + '_R' AS Renamed FROM P
                ),
                DevPay AS (
                    SELECT dv.AccountNo, kp.MpesaCode, kp.AmountValue, kp.PaymentDateValue
                    FROM Dev dv
                    INNER JOIN KosePayments kp ON kp.AccountNoBigint = dv.AccountNo
                    WHERE NOT EXISTS (SELECT 1 FROM OrphanedPayments op
                                      WHERE op.MpesaCode = kp.MpesaCode
                                        AND op.AccountNoBigint IS NOT NULL AND op.AccountNoBigint <> dv.AccountNo)
                    UNION
                    SELECT dv.AccountNo, kp.MpesaCode, kp.AmountValue, kp.PaymentDateValue
                    FROM Dev dv
                    INNER JOIN KosePayments kp ON kp.AccountNo = dv.Renamed
                    UNION
                    SELECT dv.AccountNo, kp.MpesaCode, kp.AmountValue, kp.PaymentDateValue
                    FROM Dev dv
                    INNER JOIN OrphanedPayments op ON op.AccountNoBigint = dv.AccountNo OR op.AccountNo = dv.Renamed
                    INNER JOIN KosePayments kp ON kp.MpesaCode = op.MpesaCode
                )
                SELECT
                    p.AssignmentId, p.CaseId, p.CollectorUserId, p.CollectorName, p.StartAt, p.EndAt,
                    CAST(ISNULL(SUM(dp.AmountValue), 0) AS DECIMAL(18,2)) AS Recovered,
                    CAST(ISNULL(SUM(CASE WHEN dp.PaymentDateValue >= @MonthStart THEN dp.AmountValue END), 0) AS DECIMAL(18,2)) AS RecoveredThisMonth,
                    MAX(dp.PaymentDateValue) AS LastPaymentAt
                FROM P p
                LEFT JOIN DevPay dp ON dp.AccountNo = p.AccountNo
                    AND dp.PaymentDateValue >= p.StartAt
                    AND (p.EndAt IS NULL OR dp.PaymentDateValue < p.EndAt)
                GROUP BY p.AssignmentId, p.CaseId, p.CollectorUserId, p.CollectorName, p.StartAt, p.EndAt
                OPTION (RECOMPILE)";

        // Shortfall uses the commission calculator's formula
        // (DashboardReportRepository.FetchAgentCommissionAccountRowsAsync),
        // so the frozen amount is exactly what was being deducted.
        public async Task<List<CollectionContractStanding>> GetStandingsAsync(IReadOnlyCollection<long>? accountNos = null)
        {
            var sql = StandingsSql;

            if (accountNos == null)
            {
                return (await _db.QueryAsync<CollectionContractStanding>(sql, new { All = 1, Ids = new long[] { 0 } }, commandTimeout: 90)).ToList();
            }

            var result = new List<CollectionContractStanding>();
            foreach (var chunk in accountNos.Distinct().Chunk(1000))
            {
                result.AddRange(await _db.QueryAsync<CollectionContractStanding>(sql, new { All = 0, Ids = chunk }, commandTimeout: 90));
            }
            return result;
        }

        public async Task<List<CollectionCaseRow>> GetCasesAsync()
        {
            const string sql = @"
                SELECT
                    c.Id AS CaseId, c.AccountNo, c.ContractId,
                    ISNULL(ci.First_Name, '') AS CustomerName,
                    c.DealerId, dl.CompanyName AS DealerName,
                    c.AgentUserId, au.[Name] + ' ' + ISNULL(au.[LastName], '') AS AgentName,
                    NULLIF(LTRIM(RTRIM(ISNULL(d.Make, '') + ' ' + ISNULL(d.Model, ''))), '') AS ProductName,
                    c.HandoverAt, c.ShortfallAtHandover, c.FrozenDeduction, c.Status, c.ClosedAt, c.IsGoLiveBackfill,
                    CAST(CASE WHEN ci.EndDate IS NOT NULL THEN 1 ELSE 0 END AS BIT) AS ContractEnded,
                    c.ShortfallAtReturn, c.CollectorEarnedOnCase
                FROM CollectionCases c
                LEFT JOIN Contract_Info ci ON ci.ContractID = c.ContractId AND ci.ID = c.AccountNo
                LEFT JOIN Dealers dl ON dl.DealerId = c.DealerId
                LEFT JOIN Users au ON au.UserId = c.AgentUserId
                LEFT JOIN Devices d ON d.Id = c.AccountNo
                ORDER BY c.HandoverAt DESC, c.Id DESC";

            return (await _db.QueryAsync<CollectionCaseRow>(sql, commandTimeout: 60)).ToList();
        }

        // Device payments: those on the account number, those renamed
        // "<device>_R" when the device was repossessed and resold, and
        // orphaned payments assigned to it. A payment counts for the stint
        // it falls in (StartAt inclusive, EndAt exclusive).
        public async Task<List<CollectionPeriodRow>> GetPeriodsAsync(int? collectorUserId = null, int? caseId = null)
        {
            var sql = PeriodsSql;

            var now = DateTime.Now;
            var rows = await _db.QueryAsync<CollectionPeriodRow>(sql, new
            {
                CollectorUserId = collectorUserId,
                CaseId = caseId,
                MonthStart = new DateTime(now.Year, now.Month, 1),
            }, commandTimeout: 90);
            return rows.ToList();
        }

        public async Task<Dictionary<(int CaseId, int CollectorUserId), decimal>> GetCollectorPaidAsync(int? collectorUserId = null)
        {
            const string sql = @"
                SELECT CaseId, CollectorUserId, SUM(Amount) AS Paid
                FROM CollectorCommissionPayments
                WHERE (@CollectorUserId IS NULL OR CollectorUserId = @CollectorUserId)
                GROUP BY CaseId, CollectorUserId";

            var rows = await _db.QueryAsync<(int CaseId, int CollectorUserId, decimal Paid)>(sql, new { CollectorUserId = collectorUserId });
            return rows.ToDictionary(r => (r.CaseId, r.CollectorUserId), r => r.Paid);
        }

        public async Task<List<CollectionFlagRow>> GetFlagsAsync(bool openOnly)
        {
            const string sql = @"
                SELECT f.Id, f.CaseId, c.AccountNo, ISNULL(ci.First_Name, '') AS CustomerName,
                       f.CollectorUserId, u.[Name] + ' ' + ISNULL(u.[LastName], '') AS CollectorName,
                       f.Note, f.CreatedAt, f.ResolvedAt
                FROM CollectionFlags f
                INNER JOIN CollectionCases c ON c.Id = f.CaseId
                LEFT JOIN Contract_Info ci ON ci.ContractID = c.ContractId AND ci.ID = c.AccountNo
                LEFT JOIN Users u ON u.UserId = f.CollectorUserId
                WHERE (@OpenOnly = 0 OR f.ResolvedAt IS NULL)
                ORDER BY f.CreatedAt DESC";

            return (await _db.QueryAsync<CollectionFlagRow>(sql, new { OpenOnly = openOnly ? 1 : 0 })).ToList();
        }

        // Collector logins, and other logins with Collector as an extra role.
        public async Task<List<CollectorOption>> GetCollectorsAsync()
        {
            const string sql = @"
                SELECT u.UserId, u.[Name] + ' ' + ISNULL(u.[LastName], '') AS Name, ISNULL(u.DealerId, 0) AS DealerId,
                       CAST(CASE WHEN u.RoleId = 2 THEN 1 ELSE 0 END AS BIT) AS IsDealerUser
                FROM Users u
                WHERE ISNULL(u.IsActive, 1) = 1
                  AND (u.RoleId = 6 OR u.OtherSelectedRoles LIKE '%""Collector""%')
                ORDER BY u.[Name]";
            return (await _db.QueryAsync<CollectorOption>(sql)).ToList();
        }

        public async Task<int> OpenCaseAsync(NewCollectionCase n, CaseClosure? supersede)
        {
            EnsureOpen();
            using var tx = _db.BeginTransaction(IsolationLevel.Serializable);

            if (supersede != null)
            {
                await CloseAsync(supersede, "Superseded", tx);
            }

            var caseId = await _db.QuerySingleAsync<int>(@"
                INSERT INTO CollectionCases
                    (AccountNo, ContractId, DealerId, AgentUserId, HandoverAt, ShortfallAtHandover, FrozenDeduction,
                     TotalPaidAtHandover, CommissionPaidBasis, Status, OpenedByUserId)
                OUTPUT INSERTED.Id
                VALUES (@AccountNo, @ContractId, @DealerId, @AgentUserId, GETDATE(), @ShortfallAtHandover, @FrozenDeduction,
                        @TotalPaidAtHandover, @CommissionPaidBasis, 'Open', @OpenedByUserId)", n, tx);

            await _db.ExecuteAsync(@"
                INSERT INTO CollectionAssignments (CaseId, CollectorUserId, StartAt, AssignedByUserId)
                SELECT Id, @CollectorUserId, HandoverAt, @OpenedByUserId FROM CollectionCases WHERE Id = @CaseId",
                new { CaseId = caseId, n.CollectorUserId, n.OpenedByUserId }, tx);

            await SetLegacyCollectorAsync(n.AccountNo, n.ContractId, n.CollectorUserId, n.OpenedByUserId, tx);

            tx.Commit();
            return caseId;
        }

        public async Task ReassignAsync(int caseId, long accountNo, long contractId, int collectorUserId, int changedByUserId)
        {
            EnsureOpen();
            using var tx = _db.BeginTransaction(IsolationLevel.Serializable);

            var now = await _db.QuerySingleAsync<DateTime>("SELECT CAST(GETDATE() AS DATETIME2)", transaction: tx);
            await _db.ExecuteAsync(@"
                UPDATE CollectionAssignments
                SET EndAt = @Now, EndedByUserId = @By, EndReason = 'Reassigned'
                WHERE CaseId = @CaseId AND EndAt IS NULL",
                new { CaseId = caseId, Now = now, By = changedByUserId }, tx);

            await _db.ExecuteAsync(@"
                INSERT INTO CollectionAssignments (CaseId, CollectorUserId, StartAt, AssignedByUserId)
                VALUES (@CaseId, @CollectorUserId, @Now, @By)",
                new { CaseId = caseId, CollectorUserId = collectorUserId, Now = now, By = changedByUserId }, tx);

            await SetLegacyCollectorAsync(accountNo, contractId, collectorUserId, changedByUserId, tx);

            tx.Commit();
        }

        public async Task ReturnAsync(CaseClosure closure)
        {
            EnsureOpen();
            using var tx = _db.BeginTransaction(IsolationLevel.Serializable);
            await CloseAsync(closure, "Returned", tx);
            await SetLegacyCollectorAsync(closure.AccountNo, closure.ContractId, null, closure.ClosedByUserId, tx);
            tx.Commit();
        }

        public async Task AddFlagAsync(int caseId, int collectorUserId, string note)
        {
            await _db.ExecuteAsync(
                "INSERT INTO CollectionFlags (CaseId, CollectorUserId, Note) VALUES (@CaseId, @CollectorUserId, @Note)",
                new { CaseId = caseId, CollectorUserId = collectorUserId, Note = note });
        }

        public async Task<bool> ResolveFlagAsync(int flagId, int resolvedByUserId)
        {
            var n = await _db.ExecuteAsync(
                "UPDATE CollectionFlags SET ResolvedAt = GETDATE(), ResolvedByUserId = @By WHERE Id = @Id AND ResolvedAt IS NULL",
                new { Id = flagId, By = resolvedByUserId });
            return n == 1;
        }

        private async Task CloseAsync(CaseClosure c, string endReason, IDbTransaction tx)
        {
            var now = await _db.QuerySingleAsync<DateTime>("SELECT CAST(GETDATE() AS DATETIME2)", transaction: tx);
            await _db.ExecuteAsync(@"
                UPDATE CollectionCases
                SET Status = @Status, ClosedAt = @Now, ClosedByUserId = @By,
                    RecoveredOnCase = @Recovered, CollectorEarnedOnCase = @CollectorEarned,
                    ShortfallAtReturn = @ShortfallAtReturn, TotalPaidAtReturn = @TotalPaidAtReturn
                WHERE Id = @CaseId AND Status = 'Open'",
                new
                {
                    c.CaseId, c.Status, Now = now, By = c.ClosedByUserId, c.Recovered, c.CollectorEarned,
                    c.ShortfallAtReturn, c.TotalPaidAtReturn,
                }, tx);

            await _db.ExecuteAsync(@"
                UPDATE CollectionAssignments
                SET EndAt = @Now, EndedByUserId = @By, EndReason = @Reason
                WHERE CaseId = @CaseId AND EndAt IS NULL",
                new { c.CaseId, Now = now, By = c.ClosedByUserId, Reason = endReason }, tx);
        }

        // Keeps Contract_Info.DebtCollectorUserId (read by the older
        // collections screens) in step, logged like Account Assignment.
        private async Task SetLegacyCollectorAsync(long accountNo, long contractId, int? collectorUserId, int changedByUserId, IDbTransaction tx)
        {
            const string sql = @"
                IF OBJECT_ID('dbo.AccountAssignmentLog') IS NOT NULL
                    INSERT INTO AccountAssignmentLog (AccountNo, ContractId, AssignmentRole, OldUserId, NewUserId, ChangedByUserId)
                    SELECT ci.ID, ci.ContractID, 'Collector', ci.DebtCollectorUserId, @UserId, @By
                    FROM Contract_Info ci
                    WHERE ci.ID = @AccountNo AND ci.ContractID = @ContractId AND ci.EndDate IS NULL
                      AND ISNULL(ci.DebtCollectorUserId, -1) <> ISNULL(@UserId, -1);

                UPDATE Contract_Info SET DebtCollectorUserId = @UserId
                WHERE ID = @AccountNo AND ContractID = @ContractId AND EndDate IS NULL;";

            await _db.ExecuteAsync(sql, new { AccountNo = accountNo, ContractId = contractId, UserId = collectorUserId, By = changedByUserId }, tx);
        }

        private void EnsureOpen()
        {
            if (_db.State != ConnectionState.Open)
            {
                _db.Open();
            }
        }
    }
}
