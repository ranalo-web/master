using Dapper;
using Ranalo.Models;
using System.Data;

namespace Ranalo.DataStore
{
    public class DevicesRepository : IDevicesRepository
    {
        private readonly IDbConnection _db;

        public DevicesRepository(IDbConnection db)
        {
            _db = db;
        }

        public async Task<DevicesWithDealerViewModel> GetDevicesWithNoOrders(long dealerReference = 0, int page = 1, int pageSize = 10, string searchTerm = "")
        {
            var offset = (page - 1) * pageSize;

            var countSql = @"SELECT COUNT(DISTINCT D.Id)
                               FROM Devices d
                                 INNER JOIN KosePayments kp 
                                     ON TRY_CAST(kp.AccountNo AS BIGINT) = d.Id
                                 LEFT JOIN Dealers dealer
                                     ON dealer.DealerReference = d.DeviceGroupId
                                 WHERE NOT EXISTS (
                                     SELECT 1
                                     FROM Woo_Orders wo
                                     INNER JOIN KosePayments kp2 
                                         ON wo.MpesaDepositRef = kp2.MpesaCode
                                     WHERE TRY_CAST(kp2.AccountNo AS BIGINT) = d.Id
                                       AND wo.[Status] not in ('rejected', 'failed', 'cancelled', 'on-hold', 'pending' )
                                 )
                                 AND (
                                     @DealerId = 0 OR dealer.DealerReference = @DealerId
                                 )
                                 AND (
                                @SearchTerm IS NULL
                                OR d.Id LIKE '%' + @SearchTerm + '%'
                                OR d.DeviceGroupId LIKE '%' + @SearchTerm + '%'
                                OR dealer.DealerReference LIKE '%' + @SearchTerm + '%'
                                OR dealer.CompanyName LIKE '%' + @SearchTerm + '%'
                            )";

            var totalRecords = await _db.QuerySingleAsync<int>(countSql, new { DealerId = dealerReference, searchTerm });

            var sql = @"SELECT DISTINCT 
                                d.Id AS DeviceId,
                                d.DeviceGroupId,
                                dealer.DealerReference AS DealerId,
                                dealer.CompanyName AS DealerName,
								d.CreatedAt,
                                d.ImeiNo
                            FROM Devices d
                            INNER JOIN KosePayments kp 
                                ON TRY_CAST(kp.AccountNo AS BIGINT) = d.Id
                            LEFT JOIN Dealers dealer
                                ON dealer.DealerReference = d.DeviceGroupId
                            WHERE NOT EXISTS (
                                SELECT 1
                                FROM Woo_Orders wo
                                INNER JOIN KosePayments kp2 
                                    ON wo.MpesaDepositRef = kp2.MpesaCode
                                WHERE TRY_CAST(kp2.AccountNo AS BIGINT) = d.Id
                                  AND wo.[Status] not in ('rejected', 'failed', 'cancelled', 'on-hold', 'pending')
                            )
                            AND (
                                @DealerId = 0 OR dealer.DealerReference = @DealerId
                            )
                        AND (
                                @SearchTerm IS NULL
                                OR d.Id LIKE '%' + @SearchTerm + '%'
                                OR d.DeviceGroupId LIKE '%' + @SearchTerm + '%'
                                OR dealer.DealerReference LIKE '%' + @SearchTerm + '%'
                                OR dealer.CompanyName LIKE '%' + @SearchTerm + '%'
                            )
                            
                            order by d.Id
                          OFFSET @Offset ROWS 
                          FETCH NEXT @pageSize ROWS ONLY";
            var records = await _db.QueryAsync<DeviceWithDealerDto>(sql, new { DealerId = dealerReference, offset, pageSize, searchTerm });

            return new DevicesWithDealerViewModel()
            {
                Devices = records.ToList(),
                CurrentPage = page,
                TotalPages = (int)Math.Ceiling((double)totalRecords / pageSize),
                TotalRecords = totalRecords,
                SearchTerm = searchTerm
            };
        }

        public async Task<(string AccountNo, long? DeviceId)> GetOrderLinksAsync(
            long orderId)
                {
                    var sql = @"
                SELECT 
                    kp.AccountNo,
                    d.Id AS DeviceId
                FROM Woo_Orders wo
                LEFT JOIN KosePayments kp 
                    ON wo.MpesaDepositRef = kp.MpesaCode
                LEFT JOIN Devices d 
                    ON TRY_CAST(kp.AccountNo AS BIGINT) = d.Id
                WHERE wo.OrderID = @OrderId;";

            return await _db.QueryFirstOrDefaultAsync<(string, long?)>(sql, new { OrderId = orderId });
        }

        public async Task<bool> OrderNumberIsValidAsync(long orderId)
        {
            var sql = @"
                SELECT COUNT(1)
                FROM Woo_Orders wo
                WHERE wo.OrderID = @OrderId;";

            var count = await _db.ExecuteScalarAsync<int>(sql, new { OrderId = orderId });
            return count > 0;
        }

        public async Task<bool> MpesaCodeIsValidAsync(string mpesaCode)
        {
            var sql = @"
                SELECT COUNT(1)
                FROM KosePayments kp
                WHERE kp.MpesaCode = @Mpesa;";

            var count = await _db.ExecuteScalarAsync<int>(sql, new { Mpesa = mpesaCode });
            return count > 0;
        }

        public async Task<int> UpdateMpesaForOrder(long orderId, string newMpesa)
        {
            try
            {
                string query = @"
                    UPDATE [dbo].[Woo_Orders]
                    SET [MpesaDepositRef] = @MpesaDepositRef,
                        [DateModified] = GETDATE()
                    WHERE OrderID = @OrderID";

                var parameters = new
                {
                    MpesaDepositRef = newMpesa,
                    OrderID = orderId
                };

                await _db.ExecuteAsync(query, parameters);

                return 1;
            }
            catch (Exception)
            {

                return 0;
            }
            
        }

        public async Task<long> GetMetaDataByKeyForOrderNumber(int orderNumber, string metadataKey)
        {
            var sql = @"
                SELECT MetaDataId
                FROM Woo_Orders_MetaData
                WHERE OrderId = @OrderId
                AND [Key] = @Key;";

            var metaDataId = await _db.QueryFirstOrDefaultAsync<long>(sql, new { OrderId = orderNumber, Key = metadataKey });
            return metaDataId;
        }
    }
}
