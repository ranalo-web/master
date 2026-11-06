using System.Data;
using System.Text.Json;
using Dapper;
using Ranalo.Models;
using Ranalo.Services;

namespace Ranalo.DataStore
{
    // Database/StatementUploads/001 and 002.
    public interface IStatementUploadRepository
    {
        Task<int> AddAsync(StatementUpload upload);
        Task SaveReceiptsAsync(int uploadId, IEnumerable<string> receiptNos);
        Task<List<StatementUpload>> GetAllAsync();
        Task<List<int>> GetUploadIdsWithReceiptsAsync();
        Task<List<EarlierImport>> GetEarlierImportsAsync(DateTime? beforeUtc);
        Task<List<NotOnStatementPayment>> GetNotOnStatementAsync();
    }

    public class StatementUploadRepository : IStatementUploadRepository
    {
        private readonly IDbConnection _db;

        public StatementUploadRepository(IDbConnection db)
        {
            _db = db;
        }

        public async Task<int> AddAsync(StatementUpload upload) =>
            await _db.ExecuteScalarAsync<int>(@"
                INSERT INTO PaymentStatementUploads
                    (FileName, UploadedAtUtc, UploadedByUserId, UploadedByName, PeriodFrom, PeriodTo,
                     RowsRead, NewPayments, AlreadyRecorded, Skipped, Error)
                OUTPUT INSERTED.Id
                VALUES
                    (@FileName, @UploadedAtUtc, @UploadedByUserId, @UploadedByName, @PeriodFrom, @PeriodTo,
                     @RowsRead, @NewPayments, @AlreadyRecorded, @Skipped, @Error)", upload);

        // A statement has thousands of rows: sent as one JSON list, one round trip.
        public async Task SaveReceiptsAsync(int uploadId, IEnumerable<string> receiptNos)
        {
            var codes = receiptNos
                .Select(StatementUploadRules.CleanReceipt)
                .Where(c => c.Length > 0)
                .Distinct()
                .ToList();
            if (codes.Count == 0) return;

            await _db.ExecuteAsync(@"
                INSERT INTO PaymentStatementReceipts (UploadId, ReceiptNo)
                SELECT @UploadId, value FROM OPENJSON(@Codes)",
                new { UploadId = uploadId, Codes = JsonSerializer.Serialize(codes) });
        }

        public async Task<List<StatementUpload>> GetAllAsync() =>
            (await _db.QueryAsync<StatementUpload>("SELECT * FROM PaymentStatementUploads ORDER BY UploadedAtUtc DESC")).ToList();

        public async Task<List<int>> GetUploadIdsWithReceiptsAsync() =>
            (await _db.QueryAsync<int>("SELECT DISTINCT UploadId FROM PaymentStatementReceipts")).ToList();

        // Imports before uploads were recorded only left the payments they
        // added (KosePayments.Imported), so this shows those by day -- not
        // the statement's full period.
        public async Task<List<EarlierImport>> GetEarlierImportsAsync(DateTime? beforeUtc) =>
            (await _db.QueryAsync<EarlierImport>(@"
                SELECT CAST(Created AS date) AS ImportedOn, COUNT(*) AS Added,
                       MIN(PaymentDateValue) AS FirstPayment, MAX(PaymentDateValue) AS LastPayment
                FROM KosePayments
                WHERE Imported = 1 AND (@Before IS NULL OR Created < @Before)
                GROUP BY CAST(Created AS date)
                ORDER BY ImportedOn DESC", new { Before = beforeUtc })).ToList();

        // Payments dated inside an uploaded statement's period (both M-Pesa
        // time) whose receipt isn't on any uploaded statement. Only uploads
        // that kept their receipts count.
        public async Task<List<NotOnStatementPayment>> GetNotOnStatementAsync() =>
            (await _db.QueryAsync<NotOnStatementPayment>(@"
                SELECT kp.MpesaCode, kp.AccountNoBigint AS AccountNo, kp.FirstName,
                       kp.AmountValue AS Amount, kp.PaymentDateValue AS PaidAt, kp.Created AS AddedAtUtc,
                       CASE WHEN kp.Imported = 1 THEN 'Statement import'
                            WHEN kp.BusinessShortCode IS NOT NULL THEN 'Safaricom'
                            ELSE 'IONOS sync' END AS Source
                FROM KosePayments kp
                WHERE EXISTS (SELECT 1 FROM PaymentStatementUploads u
                              WHERE u.Error IS NULL
                                AND kp.PaymentDateValue BETWEEN u.PeriodFrom AND u.PeriodTo
                                AND EXISTS (SELECT 1 FROM PaymentStatementReceipts r WHERE r.UploadId = u.Id))
                  AND NOT EXISTS (SELECT 1 FROM PaymentStatementReceipts r
                                  WHERE r.ReceiptNo = UPPER(LTRIM(RTRIM(kp.MpesaCode))))
                ORDER BY kp.PaymentDateValue DESC")).ToList();
    }
}
