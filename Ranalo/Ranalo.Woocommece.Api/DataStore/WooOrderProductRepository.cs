using Dapper;
using Ranalo.Woocommece.Api.Models;
using System.Data;
using System.Reflection.Metadata;
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
            var existSql = @"SELECT TOP (1) [Id]
                                   ,[ImageId]
                                   ,[OrderId]
                                   ,[Key]
                                   ,[FileName]
                                   ,[Url]
                                   ,[File]
                                   ,[Type]
                                   ,[Size]
                               FROM [dbo].[Woo_Orders_Images]
                               WHERE [OrderId] = @OrderID
                               AND [Key] = @ImageId";

            var existingId = await _db.QueryFirstOrDefaultAsync<ImagesMetadata>(existSql, new { OrderID = orderId, ImageId = imageDetail.Key });

            if(existingId != null)
            {
                return existingId.Id;
            }

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

        public async Task InsertNextOfKinAsync(Contact nextOfKin)
        {
            var existSql = @"SELECT TOP (1) [Id]
                              ,[OrderId]
                              ,[Name]
                              ,[Phone]
                              ,[Email]
                              ,[Address]
                               FROM [dbo].[Woo_Orders_NextOfKin]
                               WHERE [OrderId] = @OrderID
                               AND [IsPrimary] = 1";

            var existingId = await _db.QueryFirstOrDefaultAsync<Contact>(existSql, new { OrderID = nextOfKin.OrderId });

            if (existingId != null)
            {
                return;
            }

            var sql = @"INSERT INTO [dbo].[Woo_Orders_NextOfKin]
                              ([Id]
                              ,[OrderId]
                              ,[Name]
                              ,[Phone]
                              ,[Email]
                              ,[Address])
                        VALUES
                              (@Id
                              ,@OrderId
                              ,@Name
                              ,@Phone
                              ,@Email
                              ,@Address);"
                               ;

            await _db.ExecuteScalarAsync<int>(sql, new
            {
                Id = nextOfKin.Id,
                OrderId = nextOfKin.OrderId,
                Name = nextOfKin.Name,
                Phone = nextOfKin.Phone,
                Email = nextOfKin.Email,
                Address = nextOfKin.Address

            });
        }

        public async Task InsertNextOfKin2Async(Contact nextOfKin)
        {
            var existSql = @"SELECT TOP (1) [Id]
                              ,[OrderId]
                              ,[Name]
                              ,[Phone]
                              ,[Email]
                              ,[Address]
                               FROM [dbo].[Woo_Orders_NextOfKin]
                               WHERE [OrderId] = @OrderID
                               AND [IsPrimary] = 0";

            var existingId = await _db.QueryFirstOrDefaultAsync<Contact>(existSql, new { OrderID = nextOfKin.OrderId });

            if (existingId != null)
            {
                return;
            }

            var sql = @"INSERT INTO [dbo].[Woo_Orders_NextOfKin]
                              ([Id]
                              ,[OrderId]
                              ,[Name]
                              ,[Phone]
                              ,[Email]
                              ,[Address]
                              ,[IsPrimary])
                        VALUES
                              (@Id
                              ,@OrderId
                              ,@Name
                              ,@Phone
                              ,@Email
                              ,@Address
                              ,0);"
                               ;

            await _db.ExecuteScalarAsync<int>(sql, new
            {
                Id = nextOfKin.Id,
                OrderId = nextOfKin.OrderId,
                Name = nextOfKin.Name,
                Phone = nextOfKin.Phone,
                Email = nextOfKin.Email,
                Address = nextOfKin.Address

            });
        }

        public async Task InsertMetaDataAsync(UserMetaData metaData)
        {
            if(metaData.MetaData != null)
            {
                foreach (var meta in metaData.MetaData)
                {
                    var existSql = @"SELECT TOP (1) [Id]
                              ,[MetaDataId]
                              ,[OrderId]
                              ,[Key]
                              ,[Value]
                              ,[CreatedAt]
                              ,[UpdatedAt]
                               FROM [dbo].[Woo_Orders_MetaData]
                               WHERE [OrderId] = @OrderID
                               AND [Key] = @Key";

                    var existingId = await _db.QueryFirstOrDefaultAsync<MetaDataEntry>(existSql, new { OrderID = metaData.OrderId, Key = meta.Key });

                    if (existingId != null)
                    {
                        return;
                    }

                    var sql = @"INSERT INTO [dbo].[Woo_Orders_MetaData]
                              ([Id]
                              ,[MetaDataId]
                              ,[OrderId]
                              ,[Key]
                              ,[Value]
                              ,[CreatedAt]
                              ,[UpdatedAt])
                        VALUES
                              (@Id
                              ,@MetaDataId
                              ,@OrderId
                              ,@Key
                              ,@Value
                              ,GETDATE()
                              ,GETDATE());"
                               ;

                    await _db.ExecuteScalarAsync<int>(sql, new
                    {
                        Id = Guid.NewGuid(),
                        OrderId = metaData.OrderId,
                        MetaDataId = meta.Id,
                        Key = meta.Key,
                        Value = meta.Value
                    });
                }
            }
            
        }

        public async Task<List<ContractCreateDto>> GetContractEligibleOrders()
        {
            var sql = @"SELECT wo.OrderId, 
                        	   wo.MpesaDepositRef, 
                        	   kp.AccountNo, 
                        	   wo.TotalAmount,
	                           wo.FirstName,
                               wo.DailySalePrice
                        FROM Woo_Orders wo
                        INNER JOIN KosePayments kp
                            ON kp.MpesaCode = wo.MpesaDepositRef
                        LEFT JOIN Contract_Info ci
                            ON ci.ID = kp.AccountNoBigint
                        WHERE wo.[Status] IN ('approved', 'approval-waiting')
                          AND wo.ContractId IS NULL;";

            var records = await _db.QueryAsync<ContractCreateDto>(sql);

            return records.ToList();
        }


    }
}
