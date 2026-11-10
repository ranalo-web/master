using System.Data;
using Dapper;

namespace Ranalo.DataStore
{
    public static class RestructureLogKind
    {
        public const string Agreed = "Agreed";
        public const string Changed = "Changed";
        public const string SelfService = "Self-service";
        public const string Undone = "Undone";
        public const string BeforeLog = "Before log";

        // What counts as "restructured" (an undo doesn't).
        public static readonly string[] Counted = { Agreed, Changed, SelfService, BeforeLog };
    }

    public class RestructureLogAccount
    {
        public long AccountNo { get; set; }
        public string? LastBy { get; set; }
        public string? LastKind { get; set; }
        public DateTime? LastAtUtc { get; set; }
        public int Times { get; set; }
    }

    public class RestructureLogStaff
    {
        public int? ByUserId { get; set; }
        public string? ByName { get; set; }
        public int Last30Days { get; set; }
        public int Total { get; set; }
    }

    public interface IRestructureLogRepository
    {
        Task AddAsync(long accountNo, string kind, DateTime? dateAgreed, decimal? amountRes, decimal? prevAmountRes,
            int? byUserId, string? byName, string? note = null);
        Task<Dictionary<long, RestructureLogAccount>> ByAccountAsync();
        Task<List<RestructureLogStaff>> ByStaffAsync(DateTime sinceUtc);
        Task<List<RestructureLogEntry>> HistoryAsync(long accountNo);
        Task<HashSet<long>> RestructuredManyTimesAsync(int moreThan, int recentDays);
    }

    public class RestructureLogEntry
    {
        public string Kind { get; set; } = "";
        public DateTime? DateAgreed { get; set; }
        public decimal? AmountRes { get; set; }
        public decimal? PrevAmountRes { get; set; }
        public string? ByName { get; set; }
        public string? Note { get; set; }
        public DateTime AtUtc { get; set; }

        public string Who => ByName ?? (Kind == RestructureLogKind.SelfService ? "Customer (in app)"
            : Kind == RestructureLogKind.BeforeLog ? "Not recorded" : "-");
    }

    // Database/Restructures/001. Every method tolerates the table not being
    // there yet: logging is skipped and the report shows "-", rather than a
    // restructure or the report failing.
    public class RestructureLogRepository : IRestructureLogRepository
    {
        private readonly IDbConnection _db;
        private readonly ILogger<RestructureLogRepository> _logger;

        public RestructureLogRepository(IDbConnection db, ILogger<RestructureLogRepository> logger)
        {
            _db = db;
            _logger = logger;
        }

        private static bool Missing(Exception ex) =>
            ex is System.Data.Common.DbException && ex.Message.Contains("RestructureLog");

        public async Task AddAsync(long accountNo, string kind, DateTime? dateAgreed, decimal? amountRes, decimal? prevAmountRes,
            int? byUserId, string? byName, string? note = null)
        {
            try
            {
                await _db.ExecuteAsync(@"
                    INSERT INTO RestructureLog (AccountNo, Kind, DateAgreed, AmountRes, PrevAmountRes, ByUserId, ByName, Note)
                    VALUES (@AccountNo, @Kind, @DateAgreed, @AmountRes, @PrevAmountRes, @ByUserId, LEFT(@ByName, 120), LEFT(@Note, 300))",
                    new { AccountNo = accountNo, Kind = kind, DateAgreed = dateAgreed, AmountRes = amountRes, PrevAmountRes = prevAmountRes,
                          ByUserId = byUserId, ByName = byName, Note = note });
            }
            catch (Exception ex)
            {
                // Never stop the restructure itself.
                _logger.LogWarning(ex, "Restructure log not saved for {Account} ({Kind}){Hint}", accountNo, kind,
                    Missing(ex) ? " -- has Database/Restructures/001 been run?" : "");
            }
        }

        // The latest entry and the count per account.
        public async Task<Dictionary<long, RestructureLogAccount>> ByAccountAsync()
        {
            try
            {
                var rows = await _db.QueryAsync<RestructureLogAccount>(@"
                    SELECT l.AccountNo,
                           SUM(CASE WHEN l.Kind IN @Counted THEN 1 ELSE 0 END) AS Times,
                           MAX(last.LastBy) AS LastBy, MAX(last.LastKind) AS LastKind, MAX(last.AtUtc) AS LastAtUtc
                    FROM RestructureLog l
                    OUTER APPLY (SELECT TOP 1 COALESCE(x.ByName, CASE x.Kind WHEN 'Self-service' THEN 'Customer (in app)'
                                                                          WHEN 'Before log' THEN 'Not recorded' END) AS LastBy,
                                        x.Kind AS LastKind, x.AtUtc
                                 FROM RestructureLog x
                                 WHERE x.AccountNo = l.AccountNo AND x.Kind IN @Counted
                                 ORDER BY x.AtUtc DESC, x.Id DESC) last
                    GROUP BY l.AccountNo", new { Counted = RestructureLogKind.Counted });
                return rows.ToDictionary(r => r.AccountNo);
            }
            catch (Exception ex) when (Missing(ex))
            {
                return new();
            }
        }

        // The dashboard alert's accounts (WaitingForYouViewComponent.ManyRestructures).
        public async Task<HashSet<long>> RestructuredManyTimesAsync(int moreThan, int recentDays)
        {
            try
            {
                return (await _db.QueryAsync<long>(@"
                    SELECT AccountNo FROM RestructureLog
                    WHERE Kind IN @Counted
                    GROUP BY AccountNo
                    HAVING COUNT(*) > @MoreThan AND MAX(AtUtc) >= DATEADD(DAY, -@Days, SYSUTCDATETIME())",
                    new { Counted = RestructureLogKind.Counted, MoreThan = moreThan, Days = recentDays })).ToHashSet();
            }
            catch (Exception ex) when (Missing(ex))
            {
                return new();
            }
        }

        // One account's restructures, newest first.
        public async Task<List<RestructureLogEntry>> HistoryAsync(long accountNo)
        {
            try
            {
                return (await _db.QueryAsync<RestructureLogEntry>(@"
                    SELECT Kind, DateAgreed, AmountRes, PrevAmountRes, ByName, Note, AtUtc
                    FROM RestructureLog WHERE AccountNo = @AccountNo
                    ORDER BY AtUtc DESC, Id DESC", new { AccountNo = accountNo })).ToList();
            }
            catch (Exception ex) when (Missing(ex))
            {
                return new();
            }
        }

        // Staff who agreed or changed plans: since `sinceUtc`, and in total.
        // Customers' own plans are one line; plans from before the log aren't
        // anyone's.
        public async Task<List<RestructureLogStaff>> ByStaffAsync(DateTime sinceUtc)
        {
            try
            {
                return (await _db.QueryAsync<RestructureLogStaff>(@"
                    SELECT ByUserId,
                           COALESCE(MAX(ByName), CASE WHEN ByUserId IS NULL THEN 'Customer (in app)' END) AS ByName,
                           SUM(CASE WHEN AtUtc >= @Since THEN 1 ELSE 0 END) AS Last30Days,
                           COUNT(*) AS Total
                    FROM RestructureLog
                    WHERE Kind IN (@Agreed, @Changed, @SelfService)
                    GROUP BY ByUserId
                    ORDER BY SUM(CASE WHEN AtUtc >= @Since THEN 1 ELSE 0 END) DESC, COUNT(*) DESC",
                    new { Since = sinceUtc, RestructureLogKind.Agreed, RestructureLogKind.Changed, RestructureLogKind.SelfService })).ToList();
            }
            catch (Exception ex) when (Missing(ex))
            {
                return new();
            }
        }
    }
}
