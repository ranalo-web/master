using Dapper;
using Ranalo.Woocommece.Api.Models;
using System.Data;
using static System.Net.Mime.MediaTypeNames;

namespace Ranalo.Woocommece.Api.DataStore
{
    public class WooOrderProductRepository : IWooOrderProductRepository
    {

        private readonly IDbConnection _db;

        public WooOrderProductRepository(IDbConnection db)
        {
            _db = db;
        }

        public async Task<int> InsertAsync(OrderProduct product)
        {
            var sql = @"
            INSERT INTO [dbo].[Woo_OrderProduct] (
            [OrderId]
           ,[ProductId]
           ,[ProductName]
           ,[ProductColor]
           ,[ProductRam]
           ,[ProductStorage]
           ,[Sku]
           ,[Quantity]
           ,[DateCreated]
            )
            VALUES (
                @OrderId, @ProductId, @ProductName, @ProductColor, @ProductRam,
                @ProductStorage, @Sku, @Quantity, GETDATE()
            );

            SELECT CAST(SCOPE_IDENTITY() as bigint);
        ";

            return await _db.ExecuteScalarAsync<int>(sql, product);
        }

        public async Task<int> InsertImageDetailsAsync(long orderId, ImagesMetadata imageDetail)
        {
            var sql = @"INSERT INTO [dbo].[Woo_Orders_Images]
                   ([ImageId]
                   ,[OrderId]
                   ,[Key]
                   ,[FileName]
                   ,[Url]
                   ,[File]
                   ,[Type]
                   ,[Size])
             VALUES
                   (@ImageId
                   ,@OrderId
                   ,@Key
                   ,@FileName
                   ,@Url
                   ,@File
                   ,@Type
                   ,@Size);
                SELECT CAST(SCOPE_IDENTITY() as bigint);"
            ;

            return await _db.ExecuteScalarAsync<int>(sql, new { ImageId = imageDetail.Id,
                                                                OrderId = orderId, 
                                                                Key = imageDetail.Key,
                                                                FileName = imageDetail.FileName,
                                                                Url = imageDetail.Url,
                                                                File = imageDetail.File,
                                                                Type = imageDetail.Type,
                                                                Size = imageDetail.Size
            
            });
        }

        public async Task<OrderProduct?> GetByIdAsync(int id)
        {
            var sql = "SELECT * FROM [dbo].[Woo_OrderProduct] WHERE Id = @Id";
            return await _db.QueryFirstOrDefaultAsync<OrderProduct>(sql, new { Id = id });
        }

        public async Task<IEnumerable<OrderProduct>?> GetByProductsForOrderIdAsync(int orderId)
        {
            var sql = "SELECT * FROM [dbo].[Woo_OrderProduct] WHERE OrderID = @OrderID";
            return await _db.QueryAsync<OrderProduct>(sql, new { OrderID = orderId });
        }

        public async Task<OrderProduct?> GetLastCreatedProductOrderAsync()
        {
            var sql = "SELECT * FROM [dbo].[Woo_OrderProduct] WHERE ORDER BY DateCreated DESC";
            return await _db.QueryFirstOrDefaultAsync<OrderProduct>(sql);
        }

        public async Task<IEnumerable<OrderProduct>> GetAllAsync()
        {
            var sql = "SELECT * FROM [dbo].[Woo_OrderProduct]";
            return await _db.QueryAsync<OrderProduct>(sql);
        }
    }
}
