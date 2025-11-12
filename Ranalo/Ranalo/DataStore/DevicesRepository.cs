using Dapper;
using DocumentFormat.OpenXml.Drawing;
using DocumentFormat.OpenXml.Wordprocessing;
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
                  ON kp.AccountNoBigint = d.Id
              LEFT JOIN Dealers dealer
                  ON dealer.DealerReference = d.DeviceGroupId
              WHERE NOT EXISTS (
                  SELECT 1
                  FROM Woo_Orders wo
                  INNER JOIN KosePayments kp2 
                      ON wo.MpesaDepositRef = kp2.MpesaCode
                  WHERE kp2.AccountNoBigint = d.Id
                    AND wo.[Status] not in ('rejected', 'failed', 'cancelled', 'on-hold', 'pending' )
            		AND d.[Status] = 'enrolled'
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
                                ON kp.AccountNoBigint = d.Id
                            LEFT JOIN Dealers dealer
                                ON dealer.DealerReference = d.DeviceGroupId
                            WHERE NOT EXISTS (
                                SELECT 1
                                FROM Woo_Orders wo
                                INNER JOIN KosePayments kp2 
                                    ON wo.MpesaDepositRef = kp2.MpesaCode
                                WHERE kp2.AccountNoBigint = d.Id
                                  AND wo.[Status] not in ('rejected', 'failed', 'cancelled', 'on-hold', 'pending')
                                  AND d.[Status] = 'enrolled'
                            )
                            AND (
                                @DealerId = 0 OR dealer.DealerReference = @DealerId
                            )
                            AND d.[Status] = 'enrolled'
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
                SearchTerm = searchTerm,
                PageSize = pageSize
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
                    ON kp.AccountNoBigint = d.Id
                WHERE wo.OrderID = @OrderId
                AND d.[Status] = 'enrolled';";

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

        public async Task<bool> MpesaCodeIsAlreadyLinked(string newMpesa)
        {
            var sql = @"
                SELECT TOP 1 [MpesaDepositRef] 
                FROM Woo_Orders
                WHERE [MpesaDepositRef] = @Mpesa;";

            var existingMpesa = await _db.ExecuteScalarAsync<string>(sql, new { Mpesa = newMpesa });
            return existingMpesa != null;
        }

        public async Task<AllAccountsViewModel> GetAllAccountsByUserAsync(int? dealerId, string searchTerm = "", int page = 1, int pageSize = 10)
        {
            var offset = (page - 1) * pageSize;

            var countSql = @"WITH 
                        ValidPayments AS (
                            SELECT *
                            FROM KosePayments
                            WHERE AccountNoBigint IS NOT NULL
                        ),
                        PTable1 AS (
                            SELECT 
                                AccountNoBigint AS AccountNo, 
                                SUM(TRY_CAST(Amount AS DECIMAL(18,2))) AS Total_Paid
                            FROM ValidPayments
                            GROUP BY AccountNoBigint
                        ),
                        PTable4 AS (
                            SELECT 
                                p.AccountNoBigint AS AccountNo,
                                p.Amount AS Last_Paid_Amount,
                                p.PaymentDate AS LastPaidDate,
                                p.MpesaCode AS Last_MPesaCode
                            FROM ValidPayments p
                            INNER JOIN (
                                SELECT AccountNoBigint AS AccountNo, MAX(PaymentDate) AS Last_Payment_Date
                                FROM ValidPayments
                                GROUP BY AccountNo
                            ) t3 
                              ON p.AccountNoBigint = t3.AccountNo 
                             AND p.PaymentDate = t3.Last_Payment_Date	
                        ),
                        PTable5 AS (
                            SELECT 
                                p.AccountNoBigint AS AccountNo,
                                TRY_CAST(p.Amount AS DECIMAL(18,2)) AS First_Paid_Amount,
                                p.PaymentDate AS FirstPaidDate,
                                p.MpesaCode AS First_MPesaCode
                            FROM ValidPayments p
                            INNER JOIN (
                                SELECT AccountNoBigint AS AccountNo, MIN(PaymentDate) AS First_Payment_Date
                                FROM ValidPayments
                                GROUP AccountNoBigint
                            ) t2 
                              ON p.AccountNoBigint = t2.AccountNo 
                             AND p.PaymentDate = t2.First_Payment_Date
                        ),
                        ContractInf0 AS (
                        	select d.Id, 
							ci.Total_Cost as TotalAmount  ,
							ci.First_Name as CustomerName,
                        	from Devices d
                        	INNER join KosePayments p on p.AccountNoBigint = d.Id
                        	INNER join Contract_Info ci on ci.ID = p.AccountNoBigint
                        	--where  wo.MpesaDepositRef is not null
                            where d.[Status] = 'enrolled'
                            GROUP BY d.Id, ci.Total_Cost, ci.First_Name
                        )                
					  
                        SELECT 
                            COUNT(*)
                        FROM PTable1 t1
                        left JOIN Devices d 
                          ON t1.AccountNo = d.Id
                        left JOIN PTable5 t5 
                          ON t1.AccountNo = t5.AccountNo
                        left JOIN PTable4 t4 
                          ON t1.AccountNo = t4.AccountNo
                        left JOIN ContractInf0 t6
                        	ON t1.AccountNo = t6.Id
                        WHERE d.[Status] = 'enrolled'
                          AND t6.TotalAmount is not null
						    AND (
							@SearchTerm IS NULL
							OR t6.FirstName LIKE '%' + @SearchTerm + '%'
							OR t1.AccountNo LIKE '%' + @SearchTerm + '%'
							OR t5.First_MPesaCode LIKE '%' + @SearchTerm + '%'
							)
							AND (@DealerId IS NULL
							OR d.DeviceGroupId = @DealerId
							)
							GROUP BY t1.AccountNo,
                            t1.Total_Paid,
                            t5.FirstPaidDate,
                            t6.CustomerName,
                            t5.First_Paid_Amount, 
                            t4.LastPaidDate,
                            t5.First_MPesaCode,
                            t4.Last_Paid_Amount,
                            d.Make,
                            d.Model,
                            d.LastConnectedAt,
                            d.Locked,
                            d.EnrolledOn,
                            d.DeviceGroupId,
                            d.[Name],
                            d.ImeiNo,
                            d.NextLockDate,
                            d.Status,
                            d.LockType
							ORDER BY t5.FirstPaidDate DESC
							OFFSET @Offset ROWS 
							FETCH NEXT @pageSize ROWS ONLY";
            var searchParam = string.IsNullOrWhiteSpace(searchTerm) ? null : searchTerm;
            var totalRecords = await _db.QuerySingleAsync<int>(countSql, new { SearchTerm = searchParam, dealerId });

            var sql = @"WITH 
                        ValidPayments AS (
                            SELECT *
                            FROM KosePayments
                            WHERE AccountNoBigint IS NOT NULL
                        ),
                        PTable1 AS (
                            SELECT 
                                AccountNoBigint AS AccountNo, 
                                SUM(TRY_CAST(Amount AS DECIMAL(18,2))) AS Total_Paid
                            FROM ValidPayments
                            GROUP BY AccountNoBigint
                        ),
                        PTable4 AS (
                            SELECT 
                                p.AccountNoBigint AS AccountNo,
                                p.Amount AS Last_Paid_Amount,
                                p.PaymentDate AS LastPaidDate,
                                p.MpesaCode AS Last_MPesaCode
                            FROM ValidPayments p
                            INNER JOIN (
                                SELECT AccountNoBigint AS AccountNo, MAX(PaymentDate) AS Last_Payment_Date
                                FROM ValidPayments
                                GROUP BY AccountNoBigint
                            ) t3 
                              ON p.AccountNoBigint = t3.AccountNo 
                             AND p.PaymentDate = t3.Last_Payment_Date	
                        ),
                        PTable5 AS (
                            SELECT 
                                p.AccountNoBigint AS AccountNo,
                                TRY_CAST(p.Amount AS DECIMAL(18,2)) AS First_Paid_Amount,
                                p.PaymentDate AS FirstPaidDate,
                                p.MpesaCode AS First_MPesaCode
                            FROM ValidPayments p
                            INNER JOIN (
                                SELECT AccountNoBigint AS AccountNo, MIN(PaymentDate) AS First_Payment_Date
                                FROM ValidPayments
                                GROUP BY AccountNoBigint
                            ) t2 
                              ON p.AccountNoBigint = t2.AccountNo 
                             AND p.PaymentDate = t2.First_Payment_Date
                        ),
                        ContractInf0 AS (
                        	select d.Id, 
							ci.Total_Cost as TotalAmount,
							ci.First_Name as CustomerName
                        	from Devices d
                        	INNER join KosePayments p on p.AccountNoBigint = d.Id
                        	INNER join Contract_Info ci on ci.ID = p.AccountNoBigint
                        	--where  wo.MpesaDepositRef is not null
                            where d.[Status] = 'enrolled'
                            GROUP BY d.Id, ci.Total_Cost, ci.First_Name
                        )                
					  
                        SELECT 
                            t1.AccountNo,
							t5.First_MPesaCode,
                            t6.CustomerName,
                            t1.Total_Paid AS TotalPaid,
                            t5.FirstPaidDate,
                            t5.First_Paid_Amount As FirstPaymentAmount,
                            t4.LastPaidDate,
                            t4.Last_Paid_Amount AS LastPaymentAmount,
                            d.Make,
                            d.Model,
                            d.LastConnectedAt,
                            d.Locked,
                            d.EnrolledOn,
                            d.DeviceGroupId,
                            d.[Name],
                            d.ImeiNo,
                            d.NextLockDate,
                            d.Status,
                            d.LockType
                        FROM PTable1 t1
                        left JOIN Devices d 
                          ON t1.AccountNo = d.Id
                        left JOIN PTable5 t5 
                          ON t1.AccountNo = t5.AccountNo
                        left JOIN PTable4 t4 
                          ON t1.AccountNo = t4.AccountNo
                        left JOIN ContractInf0 t6
                        	ON t1.AccountNo = t6.Id
                        WHERE d.[Status] = 'enrolled'
                          AND t6.TotalAmount is not null
						    AND (
							@SearchTerm IS NULL
							OR t6.FirstName LIKE '%' + @SearchTerm + '%'
							OR t1.AccountNo LIKE '%' + @SearchTerm + '%'
							OR t5.First_MPesaCode LIKE '%' + @SearchTerm + '%'
							)
							AND (@DealerId IS NULL
							OR d.DeviceGroupId = @DealerId
							)
							GROUP BY t1.AccountNo,
                            t1.Total_Paid,
                            t5.FirstPaidDate,
                            t5.First_MPesaCode,
                            t5.First_Paid_Amount,
                            t6.CustomerName,
                            t4.LastPaidDate,
                            t4.Last_Paid_Amount,
                            d.Make,
                            d.Model,
                            d.LastConnectedAt,
                            d.Locked,
                            d.EnrolledOn,
                            d.DeviceGroupId,
                            d.[Name],
                            d.ImeiNo,
                            d.NextLockDate,
                            d.Status,
                            d.LockType
							ORDER BY t5.FirstPaidDate DESC
							OFFSET @Offset ROWS 
							FETCH NEXT @pageSize ROWS ONLY";

            var records = await _db.QueryAsync<AllAccounts>(sql, new { SearchTerm = searchParam, dealerId, offset, pageSize });

            return new AllAccountsViewModel()
            {
                Accounts = records.ToList(),
                CurrentPage = page,
                TotalPages = (int)Math.Ceiling((double)totalRecords / pageSize)
            };
        }

        public async Task<Device?> GetDeviceByAccountId(long accountId)
        {
            var sql = @"SELECT * FROM Devices
                         WHERE [Id] = @AccountId"
            ;
            var device = await _db.QueryFirstOrDefaultAsync<Device>(sql, new { AccountId = accountId });

            return device;
        }
    }
}
