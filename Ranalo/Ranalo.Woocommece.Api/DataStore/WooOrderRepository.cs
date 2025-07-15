using Ranalo.Woocommece.Api.Models;
using System.Data;
using Dapper;

namespace Ranalo.Woocommece.Api.DataStore
{
    public class WooOrderRepository : IWooOrderRepository
    {

        private readonly IDbConnection _db;

        public WooOrderRepository(IDbConnection db)
        {
            _db = db;
        }

        public async Task<int> InsertAsync(WooOrder order)
        {
            var sql = @"
            INSERT INTO [dbo].[Woo_Orders] (
                OrderID, Status, DateCreated, DateModified, TotalAmount,
                CustomerId, FirstName, LastName, Address1, Email, Phone,
                IMEI, NationalId, DOB, DealerRef, CustPhone, CustEmail, MpesaDepositRef
            )
            VALUES (
                @OrderID, @Status, @DateCreated, @DateModified, @TotalAmount,
                @CustomerId, @FirstName, @LastName, @Address1, @Email, @Phone,
                @IMEI, @NationalId, @DOB, @DealerRef, @CustPhone, @CustEmail, @MpesaDepositRef
            );

            SELECT CAST(SCOPE_IDENTITY() as bigint);
        ";

            return await _db.ExecuteScalarAsync<int>(sql, order);
        }

        public async Task<WooOrder?> GetByIdAsync(int id)
        {
            var sql = "SELECT * FROM [dbo].[Woo_Orders] WHERE Id = @Id";
            return await _db.QueryFirstOrDefaultAsync<WooOrder>(sql, new { Id = id });
        }

        public async Task<WooOrder?> GetByOrderIdAsync(long orderId)
        {
            var sql = "SELECT * FROM [dbo].[Woo_Orders] WHERE OrderID = @OrderID";
            return await _db.QueryFirstOrDefaultAsync<WooOrder>(sql, new { OrderID = orderId });
        }

        public async Task<WooOrder?> GetLastSyncedOrderAsync()
        {
            var sql = "SELECT * FROM [dbo].[Woo_Orders] WHERE ORDER BY DateSynced DESC";
            return await _db.QueryFirstOrDefaultAsync<WooOrder>(sql);
        }

        public async Task<IEnumerable<WooOrder>> GetAllAsync()
        {
            var sql = "SELECT * FROM [dbo].[Woo_Orders]";
            return await _db.QueryAsync<WooOrder>(sql);
        }

        public async Task<IEnumerable<WooOrder>> GetAllWithNoAccountsAsync()
        {
            var sql = "SELECT * FROM [dbo].[Woo_Orders]";
            return await _db.QueryAsync<WooOrder>(sql);
        }

        public async Task UpdateAsync(WooOrder order)
        {
            string query = @"
        UPDATE [dbo].[Woo_Orders]
        SET [Status] = @Status,
            [DateModified] = GETDATE(),
            [TotalAmount] = @TotalAmount,
            [IMEI] = @IMEI,
            [DealerRef] = @DealerRef,
            [MpesaDepositRef] = @MpesaDepositRef
        WHERE OrderID = @OrderID";

            var parameters = new
            {
                Status = order.Status,
                TotalAmount = order.TotalAmount,
                IMEI = order.IMEI,
                DealerRef = order.DealerRef,
                MpesaDepositRef = order.MpesaDepositRef,
                OrderID = order.OrderID
            };

            await _db.ExecuteAsync(query, parameters);
        }
    }
}
