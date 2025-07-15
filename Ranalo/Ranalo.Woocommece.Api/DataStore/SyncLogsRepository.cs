using Dapper;
using Ranalo.Woocommece.Api.Models;
using System.Data;

namespace Ranalo.Woocommece.Api.DataStore
{
    public class SyncLogsRepository : ISyncLogsRepository
    {
        private readonly IDbConnection _db;

        public SyncLogsRepository(IDbConnection db)
        {
            _db = db;
        }

        public async Task<DataSyncLog?> GetLastSyncLogAsync()
        {
            var sql = "SELECT TOP 1 * FROM [dbo].[SyncLogs] ORDER BY [LastOrderCreatedDate] desc";
            return await _db.QueryFirstOrDefaultAsync<DataSyncLog>(sql);
        }

        public async Task<int> InsertAsync(DataSyncLog log)
        {
            var sql = @"
            INSERT INTO [dbo].[SyncLogs] (
                [LastSyncedOrderId]
                ,[LastOrderCreatedDate]
                ,[Status]
                ,[Type]
            )
            VALUES (
                @LastSyncedOrderId, @LastOrderCreatedDate, @Status, @Type
            );

            SELECT CAST(SCOPE_IDENTITY() as bigint);
        ";

            return await _db.ExecuteScalarAsync<int>(sql, log);
        }

        public async Task<DataSyncLog?> GetByIdAsync(int id)
        {
            var sql = "SELECT * FROM [dbo].[SyncLogs] WHERE Id = @Id";
            return await _db.QueryFirstOrDefaultAsync<DataSyncLog>(sql, new { Id = id });
        }

        public async Task<IEnumerable<DataSyncLog>> GetAllAsync()
        {
            var sql = "SELECT * FROM [dbo].[SyncLogs] ORDER BY [SyncDate] desc";
            return await _db.QueryAsync<DataSyncLog>(sql);
        }

        public async Task<SyncPaymentsLog?> GetLastPaymentLog()
        {
            var sql = "SELECT TOP 1 * FROM [dbo].[SyncPaymentsLog] ORDER BY [LastPaymentDate] desc";
            return await _db.QueryFirstOrDefaultAsync<SyncPaymentsLog>(sql);
        }

        public async Task InsertPaymentLogAsync(SyncPaymentsLog log)
        {
            var sql = @"
            INSERT INTO [dbo].[SyncPaymentsLog]
           ([LastPaymentId]
           ,[LastPaymentDate]
           ,[CreatedDate]
            )
            VALUES (
                @LastPaymentId, @LastPaymentDate, GETDATE()
            );

            SELECT CAST(SCOPE_IDENTITY() as bigint);
        ";

            await _db.ExecuteScalarAsync<int>(sql, log);
        }

        public async Task<Device?> GetLatestDeviceAsync()
        {
            var sql = "SELECT TOP 1 * FROM [dbo].[Devices] ORDER BY [id] desc";
            return await _db.QueryFirstOrDefaultAsync<Device>(sql);
        }
    }
}
