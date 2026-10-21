using System.Data;
using Dapper;
using Ranalo.Models;

namespace Ranalo.DataStore
{
    public interface ICustomerScreeningRepository
    {
        Task<DateTime?> GetScreeningStartAsync();
        Task<List<ScreeningCall>> GetScreeningCallsAsync(long orderId);
        Task<List<ScreeningCall>> GetScreeningCallsForOrdersAsync(IEnumerable<long> orderIds);
        Task AddScreeningCallAsync(ScreeningCall call);
        Task<List<ScreeningOrderRow>> GetOrdersAwaitingApprovalAsync(DateTime screeningStartsUtc);
        Task<List<WelfareCall>> GetWelfareCallsAsync(long accountNo);
        Task<List<WelfareCall>> GetWelfareCallsForAccountsAsync(IEnumerable<long> accountNos);
        Task AddWelfareCallAsync(WelfareCall call);
        Task<List<WelfareRow>> GetEnrolmentsSinceAsync(DateTime enrolledFromUtc);
        Task<int> CountOrdersToScreenAsync(DateTime screeningStartsUtc);
        Task<int> CountWelfareCallsDueAsync(DateTime enrolledFromUtc, int dueAfterDays);
    }

    // Screening and welfare calls (Database/CustomerScreening/001).
    public class CustomerScreeningRepository : ICustomerScreeningRepository
    {
        private const string ScreeningCallColumns = @"
            c.Id, c.OrderId, c.CallType, c.NextOfKinNo, c.Reached,
            c.IdentityConfirmed, c.TermsConfirmed, c.LockUnderstood,
            c.KnowsCustomer, c.RelationshipConfirmed, c.Relationship, c.Notes,
            c.CalledByUserId, u.Name AS CalledByName, c.CalledAtUtc";

        private const string WelfareCallColumns = @"
            c.Id, c.AccountNo, c.Outcome, c.Notes, c.CalledByUserId, u.Name AS CalledByName, c.CalledAtUtc";

        private readonly IDbConnection _db;

        public CustomerScreeningRepository(IDbConnection db)
        {
            _db = db;
        }

        // null until the migration has run: nothing needs screening yet.
        public async Task<DateTime?> GetScreeningStartAsync()
        {
            const string sql = @"
                IF OBJECT_ID('dbo.CustomerScreeningStart') IS NOT NULL
                    SELECT StartsAtUtc FROM CustomerScreeningStart WHERE Id = 1
                ELSE
                    SELECT CAST(NULL AS DATETIME2)";
            return await _db.QueryFirstOrDefaultAsync<DateTime?>(sql);
        }

        public async Task<List<ScreeningCall>> GetScreeningCallsAsync(long orderId)
        {
            var sql = $@"
                SELECT {ScreeningCallColumns}
                FROM CustomerScreeningCalls c
                LEFT JOIN Users u ON u.UserId = c.CalledByUserId
                WHERE c.OrderId = @OrderId
                ORDER BY c.CalledAtUtc DESC";
            return (await _db.QueryAsync<ScreeningCall>(sql, new { OrderId = orderId })).ToList();
        }

        public async Task<List<ScreeningCall>> GetScreeningCallsForOrdersAsync(IEnumerable<long> orderIds)
        {
            var ids = orderIds.Distinct().ToList();
            if (ids.Count == 0)
            {
                return new List<ScreeningCall>();
            }

            var sql = $@"
                SELECT {ScreeningCallColumns}
                FROM CustomerScreeningCalls c
                LEFT JOIN Users u ON u.UserId = c.CalledByUserId
                WHERE c.OrderId IN @OrderIds";
            return (await _db.QueryAsync<ScreeningCall>(sql, new { OrderIds = ids })).ToList();
        }

        public async Task AddScreeningCallAsync(ScreeningCall call)
        {
            const string sql = @"
                INSERT INTO CustomerScreeningCalls
                    (OrderId, CallType, NextOfKinNo, Reached, IdentityConfirmed, TermsConfirmed, LockUnderstood,
                     KnowsCustomer, RelationshipConfirmed, Relationship, Notes, CalledByUserId, CalledAtUtc)
                VALUES
                    (@OrderId, @CallType, @NextOfKinNo, @Reached, @IdentityConfirmed, @TermsConfirmed, @LockUnderstood,
                     @KnowsCustomer, @RelationshipConfirmed, @Relationship, @Notes, @CalledByUserId, SYSUTCDATETIME())";
            await _db.ExecuteAsync(sql, call);
        }

        // Orders waiting for approval that need screening (created from the start).
        public async Task<List<ScreeningOrderRow>> GetOrdersAwaitingApprovalAsync(DateTime screeningStartsUtc)
        {
            const string sql = @"
                SELECT wo.OrderID AS OrderId, MAX(wo.FirstName) AS FirstName, MAX(wo.LastName) AS LastName,
                       MAX(wo.Phone) AS Phone, MAX(wo.DealerRef) AS DealerRef, MIN(wo.DateCreated) AS DateCreated
                FROM Woo_Orders wo
                WHERE wo.[Status] = 'approval-waiting'
                  AND wo.DateCreated >= @StartsAt
                GROUP BY wo.OrderID
                ORDER BY MIN(wo.DateCreated)";
            return (await _db.QueryAsync<ScreeningOrderRow>(sql, new { StartsAt = screeningStartsUtc })).ToList();
        }

        public async Task<int> CountOrdersToScreenAsync(DateTime screeningStartsUtc)
        {
            const string sql = @"
                SELECT COUNT(DISTINCT wo.OrderID)
                FROM Woo_Orders wo
                WHERE wo.[Status] = 'approval-waiting'
                  AND wo.DateCreated >= @StartsAt";
            return await _db.ExecuteScalarAsync<int>(sql, new { StartsAt = screeningStartsUtc });
        }

        // Same accounts as GetEnrolmentsSinceAsync, enrolled at least
        // dueAfterDays ago, with no answered welfare call.
        public async Task<int> CountWelfareCallsDueAsync(DateTime enrolledFromUtc, int dueAfterDays)
        {
            const string sql = @"
                ;WITH Latest AS (
                    SELECT e.AccountId, e.ApprovedDate,
                           ROW_NUMBER() OVER (PARTITION BY e.AccountId ORDER BY e.ApprovedDate DESC) AS rn
                    FROM Enrolments e
                    WHERE e.Status IN (2, 3)
                      AND e.ApprovedDate >= @From
                      AND e.AccountId > 0
                )
                SELECT COUNT(*)
                FROM Latest l
                WHERE l.rn = 1
                  AND l.ApprovedDate <= DATEADD(DAY, -@Days, SYSUTCDATETIME())
                  AND NOT EXISTS (SELECT 1 FROM CustomerWelfareCalls c
                                  WHERE c.AccountNo = l.AccountId AND c.Outcome IN ('AllGood', 'Issue'))";
            return await _db.ExecuteScalarAsync<int>(sql, new { From = enrolledFromUtc, Days = dueAfterDays });
        }

        public async Task<List<WelfareCall>> GetWelfareCallsAsync(long accountNo)
        {
            var sql = $@"
                SELECT {WelfareCallColumns}
                FROM CustomerWelfareCalls c
                LEFT JOIN Users u ON u.UserId = c.CalledByUserId
                WHERE c.AccountNo = @AccountNo
                ORDER BY c.CalledAtUtc DESC";
            return (await _db.QueryAsync<WelfareCall>(sql, new { AccountNo = accountNo })).ToList();
        }

        public async Task<List<WelfareCall>> GetWelfareCallsForAccountsAsync(IEnumerable<long> accountNos)
        {
            var ids = accountNos.Distinct().ToList();
            if (ids.Count == 0)
            {
                return new List<WelfareCall>();
            }

            var sql = $@"
                SELECT {WelfareCallColumns}
                FROM CustomerWelfareCalls c
                LEFT JOIN Users u ON u.UserId = c.CalledByUserId
                WHERE c.AccountNo IN @AccountNos";
            return (await _db.QueryAsync<WelfareCall>(sql, new { AccountNos = ids })).ToList();
        }

        public async Task AddWelfareCallAsync(WelfareCall call)
        {
            const string sql = @"
                INSERT INTO CustomerWelfareCalls (AccountNo, Outcome, Notes, CalledByUserId, CalledAtUtc)
                VALUES (@AccountNo, @Outcome, @Notes, @CalledByUserId, SYSUTCDATETIME())";
            await _db.ExecuteAsync(sql, call);
        }

        // Enrolments approved from the start (Approved or Locked), one row per
        // account: the latest approval.
        public async Task<List<WelfareRow>> GetEnrolmentsSinceAsync(DateTime enrolledFromUtc)
        {
            const string sql = @"
                ;WITH Latest AS (
                    SELECT e.AccountId, e.OrderId, e.FirstName, e.LastName, e.ApprovedDate,
                           ROW_NUMBER() OVER (PARTITION BY e.AccountId ORDER BY e.ApprovedDate DESC) AS rn
                    FROM Enrolments e
                    WHERE e.Status IN (2, 3)
                      AND e.ApprovedDate >= @From
                      AND e.AccountId > 0
                )
                SELECT l.AccountId AS AccountNo, l.OrderId, l.FirstName, l.LastName, l.ApprovedDate AS EnrolledAtUtc,
                       (SELECT TOP 1 wo.Phone FROM Woo_Orders wo WHERE wo.OrderID = l.OrderId) AS Phone
                FROM Latest l
                WHERE l.rn = 1";
            return (await _db.QueryAsync<WelfareRow>(sql, new { From = enrolledFromUtc })).ToList();
        }
    }
}
