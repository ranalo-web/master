using System.Data;
using Dapper;
using Ranalo.Models;

namespace Ranalo.DataStore
{
    // Database/StatementUploads/001.
    public interface IStatementUploadRepository
    {
        Task AddAsync(StatementUpload upload);
        Task<List<StatementUpload>> GetAllAsync();
        Task<List<EarlierImport>> GetEarlierImportsAsync(DateTime? beforeUtc);
    }

    public class StatementUploadRepository : IStatementUploadRepository
    {
        private readonly IDbConnection _db;

        public StatementUploadRepository(IDbConnection db)
        {
            _db = db;
        }

        public async Task AddAsync(StatementUpload upload) =>
            await _db.ExecuteAsync(@"
                INSERT INTO PaymentStatementUploads
                    (FileName, UploadedAtUtc, UploadedByUserId, UploadedByName, PeriodFrom, PeriodTo,
                     RowsRead, NewPayments, AlreadyRecorded, Skipped, Error)
                VALUES
                    (@FileName, @UploadedAtUtc, @UploadedByUserId, @UploadedByName, @PeriodFrom, @PeriodTo,
                     @RowsRead, @NewPayments, @AlreadyRecorded, @Skipped, @Error)", upload);

        public async Task<List<StatementUpload>> GetAllAsync() =>
            (await _db.QueryAsync<StatementUpload>("SELECT * FROM PaymentStatementUploads ORDER BY UploadedAtUtc DESC")).ToList();

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
    }
}
