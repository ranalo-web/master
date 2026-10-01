using System.Data;
using System.Data.SqlClient;
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

            if (payout.PayeeType == CommissionPayeeType.Collector)
            {
                var collectorPayoutId = await RecordCollectorAsync(payout, transaction);
                transaction.Commit();
                return collectorPayoutId;
            }

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

        // Collector payouts (Database/Collections/001): header with
        // CollectorUserId, one CollectorCommissionPayments line per case.
        private async Task<int> RecordCollectorAsync(NewCommissionPayout payout, IDbTransaction transaction)
        {
            const string headerSql = @"
                INSERT INTO CommissionPayouts
                    (PayeeType, PaymentType, CollectorUserId, Amount, PaidDate, Method, Reference, Notes, RecordedByUserId)
                OUTPUT INSERTED.Id
                VALUES (@PayeeType, @PaymentType, @CollectorUserId, @Amount, @PaidDate, @Method, @Reference, @Notes, @RecordedByUserId)";

            var payoutId = await _db.QuerySingleAsync<int>(headerSql, new
            {
                payout.PayeeType,
                payout.PaymentType,
                payout.CollectorUserId,
                payout.Amount,
                PaidDate = payout.PaidDate.Date,
                payout.Method,
                payout.Reference,
                payout.Notes,
                payout.RecordedByUserId,
            }, transaction);

            foreach (var line in payout.CollectorLines)
            {
                await _db.ExecuteAsync(@"
                    INSERT INTO CollectorCommissionPayments (PayoutId, CaseId, CollectorUserId, Amount, PaidDate)
                    VALUES (@PayoutId, @CaseId, @CollectorUserId, @Amount, @PaidDate)",
                    new { PayoutId = payoutId, line.CaseId, payout.CollectorUserId, line.Amount, PaidDate = payout.PaidDate.Date }, transaction);
            }

            return payoutId;
        }

        public async Task<List<CommissionPayoutRecord>> GetPayoutsAsync(string? payeeType = null, int? dealerId = null, int? agentUserId = null, int? top = null,
            int? collectorUserId = null)
        {
            // With collector payouts (Database/Collections/001).
            const string sql = @"
                SELECT TOP (@Top)
                    p.Id, p.PayeeType, p.PaymentType, p.DealerId, p.AgentUserId, p.CollectorUserId, p.Amount, p.PaidDate, p.Method, p.Reference, p.Notes,
                    p.RecordedAtUtc, p.ReceiptConfirmedAtUtc,
                    CASE WHEN p.PayeeType = 'Dealer' THEN dl.CompanyName
                         WHEN p.PayeeType = 'Collector' THEN cu.[Name] + ' ' + ISNULL(cu.[LastName], '')
                         ELSE au.[Name] + ' ' + au.[LastName] END AS PayeeName,
                    rb.[Name] + ' ' + rb.[LastName] AS RecordedByName,
                    cb.[Name] + ' ' + cb.[LastName] AS ReceiptConfirmedByName,
                    (SELECT COUNT(DISTINCT a.ContractId) FROM AgentCommissionPayments a WHERE a.PayoutId = p.Id)
                        + (SELECT COUNT(DISTINCT d.ContractId) FROM DealerCommissionPayments d WHERE d.PayoutId = p.Id)
                        + (SELECT COUNT(DISTINCT x.CaseId) FROM CollectorCommissionPayments x WHERE x.PayoutId = p.Id) AS AccountCount
                FROM CommissionPayouts p
                LEFT JOIN Dealers dl ON dl.DealerId = p.DealerId
                LEFT JOIN Users au ON au.UserId = p.AgentUserId
                LEFT JOIN Users cu ON cu.UserId = p.CollectorUserId
                LEFT JOIN Users rb ON rb.UserId = p.RecordedByUserId
                LEFT JOIN Users cb ON cb.UserId = p.ReceiptConfirmedByUserId
                WHERE (@PayeeType IS NULL OR p.PayeeType = @PayeeType)
                  AND (@DealerId IS NULL OR p.DealerId = @DealerId)
                  AND (@AgentUserId IS NULL OR p.AgentUserId = @AgentUserId)
                  AND (@CollectorUserId IS NULL OR p.CollectorUserId = @CollectorUserId)
                ORDER BY p.PaidDate DESC, p.Id DESC";

            // Before Database/Collections/001 has been run.
            const string legacySql = @"
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

            var args = new
            {
                Top = top ?? int.MaxValue,
                PayeeType = payeeType,
                DealerId = dealerId,
                AgentUserId = agentUserId,
                CollectorUserId = collectorUserId,
            };
            try
            {
                return (await _db.QueryAsync<CommissionPayoutRecord>(sql, args)).ToList();
            }
            catch (SqlException ex) when (ex.Number is 207 or 208)
            {
                if (collectorUserId.HasValue || payeeType == CommissionPayeeType.Collector)
                {
                    return new List<CommissionPayoutRecord>();
                }
                return (await _db.QueryAsync<CommissionPayoutRecord>(legacySql, args)).ToList();
            }
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

            const string collectorSql = @"
                UPDATE CommissionPayouts
                SET ReceiptConfirmedAtUtc = SYSUTCDATETIME(), ReceiptConfirmedByUserId = @UserId
                WHERE Id = @Id
                  AND ReceiptConfirmedAtUtc IS NULL
                  AND PayeeType = 'Collector'
                  AND CollectorUserId = @PayeeId";

            var updated = await _db.ExecuteAsync(payeeType == CommissionPayeeType.Collector ? collectorSql : sql,
                new { Id = payoutId, PayeeType = payeeType, PayeeId = payeeId, UserId = confirmedByUserId });
            return updated == 1;
        }
    }
}
