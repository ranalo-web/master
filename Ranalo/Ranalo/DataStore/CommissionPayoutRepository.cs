using System.Data;
using Dapper;
using Ranalo.Models;

namespace Ranalo.DataStore
{
    public class CommissionPayoutRepository : ICommissionPayoutRepository
    {
        private readonly IDbConnection _db;

        public CommissionPayoutRepository(IDbConnection db)
        {
            _db = db;
        }

        public async Task<int> RecordAsync(NewCommissionPayout payout)
        {
            if (_db.State != ConnectionState.Open)
            {
                _db.Open();
            }

            using var transaction = _db.BeginTransaction(IsolationLevel.Serializable);

            const string headerSql = @"
                INSERT INTO CommissionPayouts
                    (PayeeType, PaymentType, DealerId, AgentUserId, Amount, PaidDate, Method, Reference, Notes, RecordedByUserId)
                OUTPUT INSERTED.Id
                VALUES (@PayeeType, @PaymentType, @DealerId, @AgentUserId, @Amount, @PaidDate, @Method, @Reference, @Notes, @RecordedByUserId)";

            var payoutId = await _db.QuerySingleAsync<int>(headerSql, new
            {
                payout.PayeeType,
                payout.PaymentType,
                payout.DealerId,
                payout.AgentUserId,
                payout.Amount,
                PaidDate = payout.PaidDate.Date,
                payout.Method,
                payout.Reference,
                payout.Notes,
                payout.RecordedByUserId,
            }, transaction);

            // Neither payments table has an IDENTITY Id, so take the next Ids
            // under a range lock held until commit.
            var isAgent = payout.PayeeType == CommissionPayeeType.Agent;
            var nextIdSql = isAgent
                ? "SELECT ISNULL(MAX(Id), 0) FROM AgentCommissionPayments WITH (UPDLOCK, HOLDLOCK)"
                : "SELECT ISNULL(MAX(Id), 0) FROM DealerCommissionPayments WITH (UPDLOCK, HOLDLOCK)";
            var nextId = await _db.QuerySingleAsync<long>(nextIdSql, transaction: transaction);

            // Both amount/date column pairs are filled: dashboards read
            // AmountPaid with PaymentDate (agents) or PaidDate (dealers);
            // CommissionAmount/PaidDate are the tables' original columns.
            var lineSql = isAgent
                ? @"INSERT INTO AgentCommissionPayments
                        (Id, ContractId, AgentId, CommissionAmount, PaidDate, AmountPaid, PaymentDate, PayoutId, CommissionPart)
                    VALUES (@Id, @ContractId, @PayeeId, @Amount, @PaidDate, @Amount, @PaidDate, @PayoutId, @Part)"
                : @"INSERT INTO DealerCommissionPayments
                        (Id, ContractId, DealerId, CommissionAmount, PaidDate, PaymentReference, Notes, AmountPaid, PayoutId)
                    VALUES (@Id, @ContractId, @PayeeId, @Amount, @PaidDate, @Reference, @Notes, @Amount, @PayoutId)";

            var payeeId = isAgent ? payout.AgentUserId : payout.DealerId;
            foreach (var line in payout.Lines)
            {
                await _db.ExecuteAsync(lineSql, new
                {
                    Id = ++nextId,
                    line.ContractId,
                    PayeeId = payeeId,
                    line.Amount,
                    line.Part,
                    PaidDate = payout.PaidDate.Date,
                    payout.Reference,
                    payout.Notes,
                    PayoutId = payoutId,
                }, transaction);
            }

            transaction.Commit();
            return payoutId;
        }

        public async Task<List<CommissionPayoutRecord>> GetPayoutsAsync(string? payeeType = null, int? dealerId = null, int? agentUserId = null, int? top = null)
        {
            const string sql = @"
                SELECT TOP (@Top)
                    p.Id, p.PayeeType, p.PaymentType, p.DealerId, p.AgentUserId, p.Amount, p.PaidDate, p.Method, p.Reference, p.Notes,
                    p.RecordedAtUtc, p.ReceiptConfirmedAtUtc,
                    CASE WHEN p.PayeeType = 'Dealer' THEN dl.CompanyName ELSE au.[Name] + ' ' + au.[LastName] END AS PayeeName,
                    rb.[Name] + ' ' + rb.[LastName] AS RecordedByName,
                    cb.[Name] + ' ' + cb.[LastName] AS ReceiptConfirmedByName,
                    (SELECT COUNT(DISTINCT a.ContractId) FROM AgentCommissionPayments a WHERE a.PayoutId = p.Id)
                        + (SELECT COUNT(DISTINCT d.ContractId) FROM DealerCommissionPayments d WHERE d.PayoutId = p.Id) AS AccountCount
                FROM CommissionPayouts p
                LEFT JOIN Dealers dl ON dl.DealerId = p.DealerId
                LEFT JOIN Users au ON au.UserId = p.AgentUserId
                LEFT JOIN Users rb ON rb.UserId = p.RecordedByUserId
                LEFT JOIN Users cb ON cb.UserId = p.ReceiptConfirmedByUserId
                WHERE (@PayeeType IS NULL OR p.PayeeType = @PayeeType)
                  AND (@DealerId IS NULL OR p.DealerId = @DealerId)
                  AND (@AgentUserId IS NULL OR p.AgentUserId = @AgentUserId)
                ORDER BY p.PaidDate DESC, p.Id DESC";

            var rows = await _db.QueryAsync<CommissionPayoutRecord>(sql, new
            {
                Top = top ?? int.MaxValue,
                PayeeType = payeeType,
                DealerId = dealerId,
                AgentUserId = agentUserId,
            });
            return rows.ToList();
        }

        public async Task<List<CommissionAccountPayment>> GetAccountPaymentsAsync(string payeeType, IReadOnlyCollection<long> contractIds)
        {
            var sql = payeeType == CommissionPayeeType.Agent
                ? @"SELECT a.ContractId, COALESCE(p.PaidDate, a.PaymentDate, a.PaidDate) AS PaidDate,
                           ISNULL(a.AmountPaid, 0) AS Amount, p.Method, p.Reference, a.CommissionPart AS Part
                    FROM AgentCommissionPayments a
                    LEFT JOIN CommissionPayouts p ON p.Id = a.PayoutId
                    WHERE a.ContractId IN @Ids"
                : @"SELECT d.ContractId, COALESCE(p.PaidDate, d.PaidDate, d.Created) AS PaidDate,
                           ISNULL(d.AmountPaid, 0) AS Amount, p.Method, COALESCE(p.Reference, d.PaymentReference) AS Reference, NULL AS Part
                    FROM DealerCommissionPayments d
                    LEFT JOIN CommissionPayouts p ON p.Id = d.PayoutId
                    WHERE d.ContractId IN @Ids";

            // Chunked: SQL Server allows ~2,100 parameters per query.
            var result = new List<CommissionAccountPayment>();
            foreach (var chunk in contractIds.Chunk(1000))
            {
                result.AddRange(await _db.QueryAsync<CommissionAccountPayment>(sql, new { Ids = chunk }));
            }
            return result;
        }

        public async Task<bool> ConfirmReceiptAsync(int payoutId, string payeeType, int payeeId, int confirmedByUserId)
        {
            const string sql = @"
                UPDATE CommissionPayouts
                SET ReceiptConfirmedAtUtc = SYSUTCDATETIME(), ReceiptConfirmedByUserId = @UserId
                WHERE Id = @Id
                  AND ReceiptConfirmedAtUtc IS NULL
                  AND PayeeType = @PayeeType
                  AND ((@PayeeType = 'Dealer' AND DealerId = @PayeeId) OR (@PayeeType = 'Agent' AND AgentUserId = @PayeeId))";

            var updated = await _db.ExecuteAsync(sql, new { Id = payoutId, PayeeType = payeeType, PayeeId = payeeId, UserId = confirmedByUserId });
            return updated == 1;
        }
    }
}
