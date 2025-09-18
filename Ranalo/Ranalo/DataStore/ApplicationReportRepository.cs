using Ranalo.Models;
using System.Data;
using Dapper;
using System.Drawing.Printing;
using Ranalo.Woocommece.Api.Models;

namespace Ranalo.DataStore
{
    public class ApplicationReportRepository : IApplicationReportRepository
    {
        private readonly IDbConnection _db;

        public ApplicationReportRepository(IDbConnection db)
        {
            _db = db;
        }

        public async Task<AwaitingApprovalViewModel> GetAllWaitingApprovalAsync(string searchTerm = "", int page = 1, int pageSize = 10)
        {
            var offset = (page - 1) * pageSize;

            var countSql = @"SELECT COUNT(*) 
                FROM [dbo].[Woo_Orders] WO
                LEFT JOIN KosePayments KP
                ON WO.MpesaDepositRef = KP.MpesaCode
                WHERE [Status] = 'approval-waiting'
                AND (
                        @SearchTerm IS NULL
                        OR WO.FirstName LIKE '%' + @SearchTerm + '%'
                        OR WO.DealerRef LIKE '%' + @SearchTerm + '%'
                        OR WO.Email LIKE '%' + @SearchTerm + '%'
                        OR WO.MpesaDepositRef LIKE '%' + @SearchTerm + '%'
                    )";

            var searchParam = string.IsNullOrWhiteSpace(searchTerm) ? null : searchTerm;
            var totalRecords = await _db.QuerySingleAsync<int>(countSql, new { SearchTerm = searchParam });

            var sql = @"SELECT WO.[Id]
                    ,[OrderID]
                    ,[Status]
                    ,[DateCreated]
                    ,[FirstName]
                    ,[LastName]
                    ,[Address1]
                    ,[Email]
                    ,[Phone]
                    ,[NationalId]
                    ,[DealerRef]
                    ,[MpesaDepositRef]
	                ,KP.MpesaCode
                FROM [dbo].[Woo_Orders] WO
                LEFT JOIN KosePayments KP
                ON WO.MpesaDepositRef = KP.MpesaCode
                WHERE [Status] = 'approval-waiting'
                AND (
                        @SearchTerm IS NULL
                        OR WO.FirstName LIKE '%' + @SearchTerm + '%'
                        OR WO.DealerRef LIKE '%' + @SearchTerm + '%'
                        OR WO.Email LIKE '%' + @SearchTerm + '%'
                        OR WO.MpesaDepositRef LIKE '%' + @SearchTerm + '%'
                    )
                ORDER BY [DateCreated] DESC
                OFFSET @Offset ROWS 
                FETCH NEXT @pageSize ROWS ONLY";
            var records = await _db.QueryAsync<AwaitingApprovalDto>(sql, new { SearchTerm = searchParam, offset, pageSize });

            return new AwaitingApprovalViewModel()
            {
                AwaitingApprovals = records.ToList(),
                CurrentPage = page,
                TotalPages = (int)Math.Ceiling((double)totalRecords / pageSize)
            };
        }

        public async Task<AwaitingApprovalViewModel> GetAllMissingMpesaAsync(string searchTerm = "", int page = 1, int pageSize = 10)
        {
            var offset = (page - 1) * pageSize;

            var countSql = @"SELECT COUNT(*) 
                FROM [dbo].[Woo_Orders]
                WHERE [MpesaDepositRef] = ''
                AND [Status] != 'cancelled'
                AND (
                        @SearchTerm IS NULL
                        OR FirstName LIKE '%' + @SearchTerm + '%'
                        OR DealerRef LIKE '%' + @SearchTerm + '%'
                        OR Email LIKE '%' + @SearchTerm + '%'
                    )";

            var searchParam = string.IsNullOrWhiteSpace(searchTerm) ? null : searchTerm;
            var totalRecords = await _db.QuerySingleAsync<int>(countSql, new { SearchTerm = searchParam });

            var sql = @"SELECT [Id]
                    ,[OrderID]
                    ,[Status]
                    ,[DateCreated]
                    ,[FirstName]
                    ,[LastName]
                    ,[Address1]
                    ,[Email]
                    ,[Phone]
                    ,[NationalId]
                    ,[DealerRef]
                    ,[MpesaDepositRef]
                FROM [dbo].[Woo_Orders]  
                WHERE [MpesaDepositRef] = ''
                  AND [Status] != 'cancelled'
                AND (
                        @SearchTerm IS NULL
                        OR FirstName LIKE '%' + @SearchTerm + '%'
                        OR DealerRef LIKE '%' + @SearchTerm + '%'
                        OR Email LIKE '%' + @SearchTerm + '%'
                    )
                ORDER BY [DateCreated] DESC
                OFFSET @Offset ROWS 
                FETCH NEXT @pageSize ROWS ONLY";
            var records = await _db.QueryAsync<AwaitingApprovalDto>(sql, new { SearchTerm = searchParam, offset, pageSize });

            return new AwaitingApprovalViewModel()
            {
                AwaitingApprovals = records.ToList(),
                CurrentPage = page,
                TotalPages = (int)Math.Ceiling((double)totalRecords / pageSize)
            };
        }

        public async Task<AwaitingApprovalViewModel> GetAllOrdersByUserAsync(int dealerId, string searchTerm = "", int page = 1, int pageSize = 10)
        {
            var offset = (page - 1) * pageSize;

            var countSql = @"SELECT COUNT(*) 
                FROM [dbo].[Woo_Orders] wo
	                    INNER JOIN KosePayments kp
	                    ON kp.MpesaCode = wo.MpesaDepositRef
	                    INNER JOIN Devices d on TRY_CAST(kp.AccountNo AS bigint) = d.Id
	                    INNER JOIN Dealers dl on dl.DealerReference = d.DeviceGroupId
                        WHERE dl.DealerId = @dealerId
                        AND d.[Status] = 'enrolled'
                        AND (
                        @SearchTerm IS NULL
                        OR WO.FirstName LIKE '%' + @SearchTerm + '%'
                        OR WO.DealerRef LIKE '%' + @SearchTerm + '%'
                        OR WO.Email LIKE '%' + @SearchTerm + '%'
                        OR KP.MpesaCode LIKE '%' + @SearchTerm + '%'
                    )";
            var searchParam = string.IsNullOrWhiteSpace(searchTerm) ? null : searchTerm;
            var totalRecords = await _db.QuerySingleAsync<int>(countSql, new { SearchTerm = searchParam, dealerId });

            var sql = @"SELECT wo.[Id]
                            ,[OrderID]
                            ,wo.[Status]
                            ,[DateCreated]
                            ,[FirstName]
                            ,[LastName]
                            ,[Address1]
                            ,wo.[Email]
                            ,wo.[Phone]
                            ,[NationalId]
                            ,[DealerRef]
                            ,[MpesaDepositRef]
                        FROM [dbo].[Woo_Orders] wo
	                    INNER JOIN KosePayments kp
	                    ON kp.MpesaCode = wo.MpesaDepositRef
	                    INNER JOIN Devices d on TRY_CAST(kp.AccountNo AS bigint) = d.Id
	                    INNER JOIN Dealers dl on dl.DealerReference = d.DeviceGroupId
                        WHERE dl.DealerId = @dealerId
                        AND d.[Status] = 'enrolled'
                        AND (
                        @SearchTerm IS NULL
                        OR WO.FirstName LIKE '%' + @SearchTerm + '%'
                        OR WO.DealerRef LIKE '%' + @SearchTerm + '%'
                        OR WO.Email LIKE '%' + @SearchTerm + '%'
                        OR KP.MpesaCode LIKE '%' + @SearchTerm + '%'
                    )
                        ORDER BY [DateCreated] DESC
                        OFFSET @Offset ROWS 
                        FETCH NEXT @pageSize ROWS ONLY";

            var records = await _db.QueryAsync<AwaitingApprovalDto>(sql, new { SearchTerm = searchParam, dealerId, offset, pageSize });

            return new AwaitingApprovalViewModel()
            {
                AwaitingApprovals = records.ToList(),
                CurrentPage = page,
                TotalPages = (int)Math.Ceiling((double)totalRecords / pageSize)
            };
        }

        public async Task<AllAccountsViewModel> GetAllAccountsByUserAsync(int? dealerId, string searchTerm = "", int page = 1, int pageSize = 10)
        {
            var offset = (page - 1) * pageSize;

            var countSql = @"WITH 
                        ValidPayments AS (
                            SELECT *
                            FROM KosePayments
                            WHERE TRY_CAST(AccountNo AS BIGINT) IS NOT NULL
                        ),
                        PTable1 AS (
                            SELECT 
                                TRY_CAST(LTRIM(RTRIM(AccountNo)) AS BIGINT) AS AccountNo, 
                                SUM(TRY_CAST(Amount AS DECIMAL(18,2))) AS Total_Paid
                            FROM ValidPayments
                            GROUP BY TRY_CAST(LTRIM(RTRIM(AccountNo)) AS BIGINT)
                        ),
                        PTable4 AS (
                            SELECT 
                                TRY_CAST(LTRIM(RTRIM(p.AccountNo)) AS BIGINT) AS AccountNo,
                                p.Amount AS Last_Paid_Amount,
                                p.PaymentDate AS LastPaidDate,
                                p.MpesaCode AS Last_MPesaCode
                            FROM ValidPayments p
                            INNER JOIN (
                                SELECT TRY_CAST(LTRIM(RTRIM(AccountNo)) AS BIGINT) AS AccountNo, MAX(PaymentDate) AS Last_Payment_Date
                                FROM ValidPayments
                                GROUP BY TRY_CAST(LTRIM(RTRIM(AccountNo)) AS BIGINT)
                            ) t3 
                              ON TRY_CAST(LTRIM(RTRIM(p.AccountNo)) AS BIGINT) = t3.AccountNo 
                             AND p.PaymentDate = t3.Last_Payment_Date	
                        ),
                        PTable5 AS (
                            SELECT 
                                TRY_CAST(LTRIM(RTRIM(p.AccountNo)) AS BIGINT) AS AccountNo,
                                TRY_CAST(p.Amount AS DECIMAL(18,2)) AS First_Paid_Amount,
                                p.PaymentDate AS FirstPaidDate,
                                p.MpesaCode AS First_MPesaCode
                            FROM ValidPayments p
                            INNER JOIN (
                                SELECT TRY_CAST(LTRIM(RTRIM(AccountNo)) AS BIGINT) AS AccountNo, MIN(PaymentDate) AS First_Payment_Date
                                FROM ValidPayments
                                GROUP BY TRY_CAST(LTRIM(RTRIM(AccountNo)) AS BIGINT)
                            ) t2 
                              ON TRY_CAST(LTRIM(RTRIM(p.AccountNo)) AS BIGINT) = t2.AccountNo 
                             AND p.PaymentDate = t2.First_Payment_Date
                        ),
                        ContractInf0 AS (
                        	select d.Id, 
							wo.TotalAmount ,
							wo.FirstName + ' ' + wo.LastName as CustomerName,
							wo.CustEmail as Email
                        	from Devices d
                        	LEFT join KosePayments p on TRY_CAST(LTRIM(RTRIM(p.AccountNo )) AS BIGINT) = TRY_CAST(LTRIM(RTRIM(d.Id )) AS BIGINT)
                        	left join Woo_Orders wo on wo.MpesaDepositRef = p.MpesaCode
                        	--where  wo.MpesaDepositRef is not null
                            where d.[Status] = 'enrolled'
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
							OR t6.CustomerName LIKE '%' + @SearchTerm + '%'
							OR t1.AccountNo LIKE '%' + @SearchTerm + '%'
							OR t6.Email LIKE '%' + @SearchTerm + '%'
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
							";
            var searchParam = string.IsNullOrWhiteSpace(searchTerm) ? null : searchTerm;
            var queryRecords = await _db.QueryAsync<int>(countSql, new { SearchTerm = searchParam, dealerId});
            var totalRecords = queryRecords.Count();
            var sql = @"WITH 
                        ValidPayments AS (
                            SELECT *
                            FROM KosePayments
                            WHERE TRY_CAST(AccountNo AS BIGINT) IS NOT NULL
                        ),
                        PTable1 AS (
                            SELECT 
                                TRY_CAST(LTRIM(RTRIM(AccountNo)) AS BIGINT) AS AccountNo, 
                                SUM(TRY_CAST(Amount AS DECIMAL(18,2))) AS Total_Paid
                            FROM ValidPayments
                            GROUP BY TRY_CAST(LTRIM(RTRIM(AccountNo)) AS BIGINT)
                        ),
                        PTable4 AS (
                            SELECT 
                                TRY_CAST(LTRIM(RTRIM(p.AccountNo)) AS BIGINT) AS AccountNo,
                                p.Amount AS Last_Paid_Amount,
                                p.PaymentDate AS LastPaidDate,
                                p.MpesaCode AS Last_MPesaCode
                            FROM ValidPayments p
                            INNER JOIN (
                                SELECT TRY_CAST(LTRIM(RTRIM(AccountNo)) AS BIGINT) AS AccountNo, MAX(PaymentDate) AS Last_Payment_Date
                                FROM ValidPayments
                                GROUP BY TRY_CAST(LTRIM(RTRIM(AccountNo)) AS BIGINT)
                            ) t3 
                              ON TRY_CAST(LTRIM(RTRIM(p.AccountNo)) AS BIGINT) = t3.AccountNo 
                             AND p.PaymentDate = t3.Last_Payment_Date	
                        ),
                        PTable5 AS (
                            SELECT 
                                TRY_CAST(LTRIM(RTRIM(p.AccountNo)) AS BIGINT) AS AccountNo,
                                TRY_CAST(p.Amount AS DECIMAL(18,2)) AS First_Paid_Amount,
                                p.PaymentDate AS FirstPaidDate,
                                p.MpesaCode AS First_MPesaCode
                            FROM ValidPayments p
                            INNER JOIN (
                                SELECT TRY_CAST(LTRIM(RTRIM(AccountNo)) AS BIGINT) AS AccountNo, MIN(PaymentDate) AS First_Payment_Date
                                FROM ValidPayments
                                GROUP BY TRY_CAST(LTRIM(RTRIM(AccountNo)) AS BIGINT)
                            ) t2 
                              ON TRY_CAST(LTRIM(RTRIM(p.AccountNo)) AS BIGINT) = t2.AccountNo 
                             AND p.PaymentDate = t2.First_Payment_Date
                        ),
                        ContractInf0 AS (
                        	select d.Id, 
							wo.TotalAmount ,
							wo.FirstName + ' ' + wo.LastName as CustomerName,
							wo.CustEmail as Email
                        	from Devices d
                        	LEFT join KosePayments p on TRY_CAST(LTRIM(RTRIM(p.AccountNo )) AS BIGINT) = TRY_CAST(LTRIM(RTRIM(d.Id )) AS BIGINT)
                        	left join Woo_Orders wo on wo.MpesaDepositRef = p.MpesaCode
                        	--where  wo.MpesaDepositRef is not null
                            where d.[Status] = 'enrolled'
                        )                
					  
                        SELECT 
                            t1.AccountNo,
							t5.First_MPesaCode,
                            t6.CustomerName,
                            t6.TotalAmount ,
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
							OR t6.CustomerName LIKE '%' + @SearchTerm + '%'
							OR t1.AccountNo LIKE '%' + @SearchTerm + '%'
							OR t6.Email LIKE '%' + @SearchTerm + '%'
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
                            t6.TotalAmount ,
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


        public async Task<AwaitingApprovalViewModel> GetAllMissingMpesaForUserAsync(int dealerId, string searchTerm = "", int page = 1, int pageSize = 10)
        {
            var offset = (page - 1) * pageSize;

            var countSql = @"SELECT COUNT(*) 
                FROM [dbo].[Woo_Orders] wo
	                    INNER JOIN KosePayments kp
	                    ON kp.MpesaCode = wo.MpesaDepositRef
	                    INNER JOIN Devices d on TRY_CAST(kp.AccountNo AS bigint) = d.Id
	                    INNER JOIN Dealers dl on dl.DealerReference = d.DeviceGroupId
                        WHERE dl.DealerId = @dealerId
                        AND d.[Status] = 'enrolled'
                        AND (
                        @SearchTerm IS NULL
                        OR WO.FirstName LIKE '%' + @SearchTerm + '%'
                        OR WO.DealerRef LIKE '%' + @SearchTerm + '%'
                        OR WO.Email LIKE '%' + @SearchTerm + '%'
                        OR KP.MpesaCode LIKE '%' + @SearchTerm + '%'
                    )";
            var searchParam = string.IsNullOrWhiteSpace(searchTerm) ? null : searchTerm;
            var totalRecords = await _db.QuerySingleAsync<int>(countSql, new { SearchTerm = searchParam, dealerId });

            var sql = @"SELECT wo.[Id]
                            ,[OrderID]
                            ,wo.[Status]
                            ,[DateCreated]
                            ,[FirstName]
                            ,[LastName]
                            ,[Address1]
                            ,wo.[Email]
                            ,wo.[Phone]
                            ,[NationalId]
                            ,[DealerRef]
                            ,[MpesaDepositRef]
                        FROM [dbo].[Woo_Orders] wo
	                    INNER JOIN KosePayments kp
	                    ON kp.MpesaCode = wo.MpesaDepositRef
	                    INNER JOIN Devices d on TRY_CAST(kp.AccountNo AS bigint) = d.Id
	                    INNER JOIN Dealers dl on dl.DealerReference = d.DeviceGroupId
                        WHERE dl.DealerId = @dealerId
                        AND d.[Status] = 'enrolled'
                        AND (
                        @SearchTerm IS NULL
                        OR WO.FirstName LIKE '%' + @SearchTerm + '%'
                        OR WO.DealerRef LIKE '%' + @SearchTerm + '%'
                        OR WO.Email LIKE '%' + @SearchTerm + '%'
                        OR KP.MpesaCode LIKE '%' + @SearchTerm + '%'
                    )
                        ORDER BY [DateCreated] DESC
                        OFFSET @Offset ROWS 
                        FETCH NEXT @pageSize ROWS ONLY";

            var records = await _db.QueryAsync<AwaitingApprovalDto>(sql, new { SearchTerm = searchParam, dealerId, offset, pageSize });

            return new AwaitingApprovalViewModel()
            {
                AwaitingApprovals = records.ToList(),
                CurrentPage = page,
                TotalPages = (int)Math.Ceiling((double)totalRecords / pageSize)
            };
        }

        public async Task<IEnumerable<AwaitingApprovalDto>> GetAllOrdersAsync()
        {
            var sql = @"SELECT [Id]
                    ,[OrderID]
                    ,[Status]
                    ,[DateCreated]
                    ,[FirstName]
                    ,[LastName]
                    ,[Address1]
                    ,[Email]
                    ,[Phone]
                    ,[NationalId]
                    ,[DealerRef]
                    ,[MpesaDepositRef]
	                ,MpesaCode
                FROM [dbo].[Woo_Orders] ";
            return await _db.QueryAsync<AwaitingApprovalDto>(sql);
        }

        public async Task<KosePaymentsViewModel> GetOrphanedPaymentsAsync(int page, int pageSize)
        {
            var offset = (page - 1) * pageSize;

            var countSql = @"SELECT COUNT(*)
                            FROM KosePayments kp
                        LEFT JOIN Devices D ON D.Id = TRY_CAST(kp.AccountNo AS BIGINT)
                        WHERE D.Id is null";

            var sql = @" SELECT kp.MpesaCode, kp.AccountNo, kp.AmountValue, kp.PaymentDateValue 
                        FROM KosePayments kp
                        LEFT JOIN Devices D ON D.Id = TRY_CAST(kp.AccountNo AS BIGINT)
                        WHERE D.Id is null
                        --AND D.[Status] = 'enrolled'
                        ORDER BY kp.PaymentDateValue desc
                        OFFSET @Offset ROWS 
                        FETCH NEXT @pageSize ROWS ONLY";

            var payments = await _db.QueryAsync<KosePayments>(sql, new { offset, pageSize });
            var totalRecords = await _db.QuerySingleAsync<int>(countSql);

            return new KosePaymentsViewModel()
            {
                CurrentPage = page,
                Payments = payments.ToList(),
                TotalPages = (int)Math.Ceiling((double)totalRecords / pageSize)
            };


        }

        public async Task<KosePaymentsViewModel> GetAllPaymentsAsync(string searchTerm = "", int page = 1, int pageSize = 10)
        {
            var offset = (page - 1) * pageSize;

            var countsql = @" SELECT COUNT(*) 
                        FROM KosePayments
                        WHERE (
                        @SearchTerm IS NULL
                        OR AccountNo LIKE '%' + @SearchTerm + '%'
                        OR AmountValue LIKE '%' + @SearchTerm + '%'
                        OR PaymentDateValue LIKE '%' + @SearchTerm + '%'
                        OR MpesaCode LIKE '%' + @SearchTerm + '%')
                        ";

            var searchParam = string.IsNullOrWhiteSpace(searchTerm) ? null : searchTerm;
            var totalRecords = await _db.QuerySingleAsync<int>(countsql, new { SearchTerm = searchParam });

            var sql = @" SELECT MpesaCode, AccountNo, AmountValue, PaymentDateValue 
                        FROM KosePayments
                        WHERE (
                        @SearchTerm IS NULL
                        OR AccountNo LIKE '%' + @SearchTerm + '%'
                        OR AmountValue LIKE '%' + @SearchTerm + '%'
                        OR PaymentDateValue LIKE '%' + @SearchTerm + '%'
                        OR MpesaCode LIKE '%' + @SearchTerm + '%')
                        ORDER BY PaymentDateValue desc
                        OFFSET @Offset ROWS 
                        FETCH NEXT @pageSize ROWS ONLY";

            var payments = await _db.QueryAsync<KosePayments>(sql, new { SearchTerm = searchParam, offset, pageSize });

            return new KosePaymentsViewModel()
            {
                CurrentPage = page,
                Payments = payments.ToList(),
                TotalPages = (int)Math.Ceiling((double)totalRecords / pageSize)
            };
        }

        public async Task<KosePaymentsViewModel> GetAllPaymentsByDealerIdAsync(int dealerId, string searchTerm = "", int page = 1, int pageSize = 10)
        {
            var offset = (page - 1) * pageSize;

            var countsql = @" SELECT COUNT(*) 
                        FROM [dbo].[KosePayments] kp
                        INNER JOIN Devices d on TRY_CAST(kp.AccountNo AS bigint) = d.Id
                        INNER JOIN Dealers dl on dl.DealerReference = d.DeviceGroupId
                        WHERE dl.DealerId = @dealerId
                        AND d.[Status] = 'enrolled'
                        AND (
                        @SearchTerm IS NULL
                        OR AccountNo LIKE '%' + @SearchTerm + '%'
                        OR AmountValue LIKE '%' + @SearchTerm + '%'
                        OR PaymentDateValue LIKE '%' + @SearchTerm + '%'
                        OR MpesaCode LIKE '%' + @SearchTerm + '%')";

            var searchParam = string.IsNullOrWhiteSpace(searchTerm) ? null : searchTerm;
            var totalRecords = await _db.QuerySingleAsync<int>(countsql, new { dealerId, SearchTerm = searchParam });

            var sql = @"SELECT kp.[Id]
                             ,[AccountNo]
                             ,[MpesaCode]
                             ,[Amount]
                             ,[PaymentDate]
                             ,[AmountValue]
                             ,[PaymentDateValue]
                             ,[Created]
                        FROM [dbo].[KosePayments] kp
                        INNER JOIN Devices d on TRY_CAST(kp.AccountNo AS bigint) = d.Id
                        INNER JOIN Dealers dl on dl.DealerReference = d.DeviceGroupId
                        WHERE dl.DealerId = @dealerId
                        AND d.[Status] = 'enrolled'
                        AND (
                        @SearchTerm IS NULL
                        OR AccountNo LIKE '%' + @SearchTerm + '%'
                        OR AmountValue LIKE '%' + @SearchTerm + '%'
                        OR PaymentDateValue LIKE '%' + @SearchTerm + '%'
                        OR MpesaCode LIKE '%' + @SearchTerm + '%')
                        ORDER BY PaymentDateValue desc
                        OFFSET @Offset ROWS 
                        FETCH NEXT @pageSize ROWS ONLY";

            var payments = await _db.QueryAsync<KosePayments>(sql, new { dealerId, offset, pageSize, SearchTerm = searchParam });

            return new KosePaymentsViewModel()
            {
                CurrentPage = page,
                Payments = payments.ToList(),
                TotalPages = (int)Math.Ceiling((double)totalRecords / pageSize)
            };
        }


        public async Task<IEnumerable<Dealer>> GetAllDealersAsync()
        {
            var sql = @"SELECT *
                FROM [dbo].[Dealers] ";
            return await _db.QueryAsync<Dealer>(sql);
        }

        public async Task<IEnumerable<Device>> GetAllDevicesAsync()
        {
            var sql = @"SELECT *
                FROM [dbo].[Devices] 
                WHERE [Status] = 'enrolled'";
            return await _db.QueryAsync<Device>(sql);
        }

        public async  Task<CustomerDetails?> GetCustomerDetails(long orderId)
        {
            var sql = @" SELECT wo.Id,
                                wo.OrderID,
                        		 wo.[Status],
                        		 wo.DateCreated,
                        		 wo.DateModified,
                        		 wo.TotalAmount,
                        		 wo.CustomerId,
                        		 wo.FirstName,
                        		 wo.LastName,
                        		 wo.Address1,
                        		 wo.Email,
                        		 wo.Phone,
                        		 wo.IMEI,
                        		 wo.NationalId,
                        		 wo.DealerRef,
                        		 wo.MpesaDepositRef,
                        		 woi.[Url]
                          FROM [dbo].[Woo_Orders] wo
                          LEFT JOIN [dbo].[Woo_Orders_Images] woi
                          ON wo.OrderID = woi.OrderId
                          where wo.OrderID = @OrderId";

            return await _db.QueryFirstOrDefaultAsync<CustomerDetails>(sql, new { OrderId = orderId});
        }

        public async Task<CustomerDetails?> GetCustomerDetailsByFirstMpesaCode(string firstMpesaCode)
        {
            var sql = @" SELECT wo.OrderID,
                        		 wo.[Status],
                        		 wo.DateCreated,
                        		 wo.DateModified,
                        		 wo.TotalAmount,
                        		 wo.CustomerId,
                        		 wo.FirstName,
                        		 wo.LastName,
                        		 wo.Address1,
                        		 wo.Email,
                        		 wo.Phone,
                        		 wo.IMEI,
                        		 wo.NationalId,
                        		 wo.DealerRef,
                        		 wo.MpesaDepositRef,
                        		 woi.[Url]
                          FROM [dbo].[Woo_Orders] wo
                          LEFT JOIN [dbo].[Woo_Orders_Images] woi
                          ON wo.OrderID = woi.OrderId
                          where wo.MpesaDepositRef = @FirstMpesaCode";

            return await _db.QueryFirstOrDefaultAsync<CustomerDetails>(sql, new { FirstMpesaCode = firstMpesaCode });
        }


        public async Task<IEnumerable<Models.ImagesMetadata>> GetIdentityImagesForOrder(long orderId)
        {
            var sql = @" SELECT woi.[Id]
                                ,woi.[ImageId]
                                ,woi.[OrderId]
                                ,woi.[Key]
                                ,woi.[FileName]
                                ,woi.[Url]
                                ,woi.[File]
                                ,woi.[Type]
                                ,woi.[Size]
                          FROM [dbo].[Woo_Orders] wo
                          INNER JOIN [dbo].[Woo_Orders_Images] woi
                          ON wo.Id = woi.OrderId
                          where wo.OrderID = @OrderId";

            return await _db.QueryAsync<Models.ImagesMetadata>(sql, new { OrderId = orderId });
        }

        public async Task<int> RejectOrder(long orderId)
        {
            try
            {
                string query = @"
                    UPDATE [dbo].[Woo_Orders]
                    SET [Status] = @Status,
                        [DateModified] = GETDATE()
                    WHERE OrderID = @OrderID";

                var parameters = new
                {
                    Status = "rejected",
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

        public async Task<int> ApproveOrder(long orderId)
        {
            try
            {
                string query = @"
                    UPDATE [dbo].[Woo_Orders]
                    SET [Status] = @Status,
                        [DateModified] = GETDATE()
                    WHERE OrderID = @OrderID";

                var parameters = new
                {
                    Status = "approved",
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

        public async Task<AccountSummary?> GetPaymentSummaryForAccountId(string customerId)
        {

            var sql = @"WITH 
                        PTable1 AS (
                            SELECT 
                                AccountNo, 
                                SUM(TRY_CAST(Amount AS DECIMAL(18,2))) AS Total_Paid
                            FROM KosePayments
                        	where AccountNo = @AccountId
                            GROUP BY AccountNo
                        ),
                        PTable2 AS (
                            SELECT 
                                AccountNo, 
                                MIN(PaymentDate) AS First_Payment_Date
                            FROM KosePayments
                        	where AccountNo = @AccountId
                            GROUP BY AccountNo
                        ),
                        PTable3 AS (
                            SELECT 
                                AccountNo, 
                                MAX(PaymentDate) AS Last_Payment_Date
                            FROM KosePayments
                        	where AccountNo = @AccountId
                            GROUP BY AccountNo
                        ),
                        PTable4 AS (
                            SELECT 
                                p.AccountNo,
                                p.Amount AS Last_Paid_Amount,
                                p.PaymentDate AS LastPaidDate,
                                p.MpesaCode AS Last_MPesaCode
                            FROM KosePayments p
                            INNER JOIN PTable3 t3 ON p.AccountNo = t3.AccountNo
                            WHERE p.PaymentDate = t3.Last_Payment_Date	
                        	AND P.AccountNo = @AccountId
                        ),
                        PTable5 AS (
                            SELECT 
                                p.AccountNo,
                                TRY_CAST(p.Amount AS DECIMAL(18,2)) AS First_Paid_Amount,
                                p.PaymentDate AS FirstPaidDate,
                                p.MpesaCode AS First_MPesaCode
                            FROM KosePayments p
                            INNER JOIN PTable2 t2 ON p.AccountNo = t2.AccountNo
                            WHERE p.PaymentDate = t2.First_Payment_Date
                        	AND p.AccountNo = @AccountId
                        ),
                        STable AS (
                            SELECT 
                                p.id,
                                p.[Status],
                                p.Model,
                                p.Make,
                                p.Locked,
                                p.LockType,
                                l.Last_Payment_Date,
                                b.First_Paid_Amount,
                                p.FirstLockDateIsoFormat AS First_Lock_Date,
                                p.NextLockDateIsoFormat AS Next_Lock_Date,
                                p.LastConnectedAt,
                                t.Total_Paid,
                                f.First_Payment_Date,
                                b.FirstPaidDate,
                                b.First_MPesaCode,
                                a.Last_Paid_Amount,
                                a.LastPaidDate,
                                a.Last_MPesaCode,
                                p.ImeiNo
                            FROM Devices p
                            LEFT JOIN PTable1 t ON p.id = TRY_CAST(t.AccountNo AS BIGINT)
                            LEFT JOIN PTable2 f ON p.id = TRY_CAST(f.AccountNo AS BIGINT)
                            LEFT JOIN PTable3 l ON p.id = TRY_CAST(l.AccountNo AS BIGINT)
                            LEFT JOIN PTable4 a ON p.id = TRY_CAST(a.AccountNo AS BIGINT)
                            LEFT JOIN PTable5 b ON p.id = TRY_CAST(b.AccountNo AS BIGINT)
                        WHERE p.[Status] = 'enrolled'
                        ),
                        FilteredSTable AS (
                            SELECT *
                            FROM STable
                            WHERE First_Paid_Amount IS NOT NULL
                        ),
                        Base AS (
                            SELECT 
                                *,
                                CAST(Total_Paid AS DECIMAL(18,2)) AS b_price_numeric
                            FROM FilteredSTable
                        	--where [Status] in ('approved', 'approval-waiting')
                        ),
                        Computed AS (
                            SELECT 
                                *,
                                b_price_numeric + 5000 AS dealer_payment,
                                (b_price_numeric + 5000) * 0.235 AS deposit,
                                0.0066733 * b_price_numeric + 8.1015 AS daily_rate
                            FROM Base
                        ),
                        Final AS (
                            SELECT 
                                *,
                                daily_rate * 7 AS weekly_rate,
                                deposit + (daily_rate * 365) AS unit_year,
                        		daily_rate * 30 AS monthly_rate
                            FROM Computed
                        ),
                        STable1 AS (
                            SELECT 
                                s.*,
                                12 as Term_in_Months, -- assuming this comes from Woo_Orders
                                d.ID AS WooOrderID,
                        		d.deposit,
                        		d.daily_rate as Daily,
                        		d.weekly_rate as Weekly,
                        		d.monthly_rate as Monthly
                            FROM FilteredSTable s
                            LEFT JOIN Final d ON s.Id = d.Id
                        ),
                        ComputedSTable1 AS (
                            SELECT *,
                                CAST(First_Payment_Date AS DATETIME) AS First_Pay_Date,
                                DATEDIFF(DAY, First_Payment_Date, GETDATE()) AS No_Days_Lifetime,
                                DATEDIFF(DAY, First_Payment_Date, GETDATE()) * 1.0 AS No_Days_Units,
                                DATEADD(DAY, Term_in_Months * 30, CAST(FirstPaidDate AS DATETIME)) AS Contract_End_Date,
                                DATEDIFF(
                                    DAY,
                                    CAST(First_Payment_Date AS DATETIME),
                                    DATEADD(DAY, Term_in_Months * 30, CAST(FirstPaidDate AS DATETIME))
                                ) AS Days_Contract_End
                            FROM STable1
                        ),
                        FinalTable AS (
                            SELECT *,
                                -- Minimum_Days = least of Days_Contract_End and No_Days_Units (handling NULLs)
                                CASE 
                                    WHEN Days_Contract_End IS NULL THEN No_Days_Units
                                    WHEN No_Days_Units IS NULL THEN Days_Contract_End
                                    ELSE 
                                        CASE 
                                            WHEN Days_Contract_End < No_Days_Units THEN Days_Contract_End
                                            ELSE No_Days_Units
                                        END
                                END AS Minimum_Days
                            FROM ComputedSTable1
                        ),
                        
                        WithTotalDue AS (
                            SELECT *,
                                -- Total_Due = Deposit + (Daily * Minimum_Days) + (Weekly * Minimum_Days / 7) + (Monthly * Minimum_Days / 30)
                                (Deposit 
                                    + (Daily * Minimum_Days) 
                                    + (Weekly * Minimum_Days / 7.0) 
                                    + (Monthly * Minimum_Days / 30.0)
                                ) AS Total_Due,
                        
                                -- DailyPaymentALL = Daily + Weekly / 7 + Monthly / 30
                                (Daily + (Weekly / 7.0) + (Monthly / 30.0)) AS DailyPaymentALL,
                        
                                -- Arrears = Total_Paid - Total_Due
                                (Total_Paid - 
                                    (Deposit 
                                        + (Daily * Minimum_Days) 
                                        + (Weekly * Minimum_Days / 7.0) 
                                        + (Monthly * Minimum_Days / 30.0)
                                    )
                                ) AS Arrears,
                        
                                -- Loan_Balance = Deposit + Daily * 30 * Term + Weekly * (30/7) * Term + Monthly * Term - Total_Paid
                                (
                                    Deposit 
                                    + (Daily * 30 * Term_in_Months) 
                                    + (Weekly * (30.0/7.0) * Term_in_Months) 
                                    + (Monthly * Term_in_Months) 
                                    - Total_Paid
                                ) AS Loan_Balance,
                        
                                -- Curr_Run_Time = current datetime (used in R for timestamps)
                                GETDATE() AS Curr_Run_Time
                            FROM FinalTable
                        )
                        
                        
                        SELECT WooOrderID As AccountId
                        		,LastPaidDate As LastPaymentDate
                        		,FirstPaidDate AS FirstPaymentDate
                        		,Loan_Balance AS LoanBalance
                        		,Daily
                        		,Weekly
                        		,Monthly
                        		,deposit As Deposit
                        		,Total_Paid AS TotalPaid
                                ,Contract_End_Date AS ContractEndDate
                        FROM WithTotalDue
                        --where WooOrderID = 1894076
                        GROUP BY WooOrderID
                        ,Loan_Balance
                        , Daily
                        , Weekly
                        , Monthly
                        , deposit
                        , Total_Paid
                        ,LastPaidDate
                        ,FirstPaidDate
                        ,Contract_End_Date";

            return await _db.QueryFirstOrDefaultAsync<AccountSummary>(sql, new { AccountId = customerId });
        }

        public async Task<string?> GetCustomerAccountByMpesa(string mpesaDepositRef)
        {
            var sql = @"SELECT [AccountNo]
                        FROM [dbo].[KosePayments]
                        Where MpesaCode = @MpesaCode";

            return await _db.QueryFirstOrDefaultAsync<string>(sql, new { MpesaCode = mpesaDepositRef });
        }

        public Task<AccountSummary?> GetAccountSummary(string customerAccount)
        {
            throw new NotImplementedException();
        }

        public async Task<PaymentsViewModel> GetPaymentSummaryAsync(int deviceGroupId = 0, int page = 1, int pageSize = 10, string searchTerm = "")
        {
            var offset = (page - 1) * pageSize;

            var countQuery = SetPaymentSummaryQuery();
            var totalRecords = await _db.QuerySingleAsync<int>(countQuery, new { DealerId = deviceGroupId, searchParam = searchTerm });

            var sql = @"WITH 
                        ValidPayments AS (
                            SELECT *
                            FROM KosePayments
                            WHERE TRY_CAST(AccountNo AS BIGINT) IS NOT NULL
                        ),
                        PTable1 AS (
                            SELECT 
                                TRY_CAST(LTRIM(RTRIM(AccountNo)) AS BIGINT) AS AccountNo, 
                                SUM(TRY_CAST(Amount AS DECIMAL(18,2))) AS Total_Paid
                            FROM ValidPayments
                            GROUP BY TRY_CAST(LTRIM(RTRIM(AccountNo)) AS BIGINT)
                        ),
                        PTable4 AS (
                            SELECT 
                                TRY_CAST(LTRIM(RTRIM(p.AccountNo)) AS BIGINT) AS AccountNo,
                                p.Amount AS Last_Paid_Amount,
                                p.PaymentDate AS LastPaidDate,
                                p.MpesaCode AS Last_MPesaCode
                            FROM ValidPayments p
                            INNER JOIN (
                                SELECT TRY_CAST(LTRIM(RTRIM(AccountNo)) AS BIGINT) AS AccountNo, MAX(PaymentDate) AS Last_Payment_Date
                                FROM ValidPayments
                                GROUP BY TRY_CAST(LTRIM(RTRIM(AccountNo)) AS BIGINT)
                            ) t3 
                              ON TRY_CAST(LTRIM(RTRIM(p.AccountNo)) AS BIGINT) = t3.AccountNo 
                             AND p.PaymentDate = t3.Last_Payment_Date	
                        ),
                        PTable5 AS (
                            SELECT 
                                TRY_CAST(LTRIM(RTRIM(p.AccountNo)) AS BIGINT) AS AccountNo,
                                TRY_CAST(p.Amount AS DECIMAL(18,2)) AS First_Paid_Amount,
                                p.PaymentDate AS FirstPaidDate,
                                p.MpesaCode AS First_MPesaCode
                            FROM ValidPayments p
                            INNER JOIN (
                                SELECT TRY_CAST(LTRIM(RTRIM(AccountNo)) AS BIGINT) AS AccountNo, MIN(PaymentDate) AS First_Payment_Date
                                FROM ValidPayments
                                GROUP BY TRY_CAST(LTRIM(RTRIM(AccountNo)) AS BIGINT)
                            ) t2 
                              ON TRY_CAST(LTRIM(RTRIM(p.AccountNo)) AS BIGINT) = t2.AccountNo 
                             AND p.PaymentDate = t2.First_Payment_Date
                        ),
                        ContractInf0 AS (
                        	select d.Id, wo.TotalAmount 
                        	from Devices d
                        	inner join KosePayments p on TRY_CAST(LTRIM(RTRIM(p.AccountNo )) AS BIGINT) = TRY_CAST(LTRIM(RTRIM(d.Id )) AS BIGINT)
                        	left join Woo_Orders wo on wo.MpesaDepositRef = p.MpesaCode
                        	where  wo.MpesaDepositRef is not null
                            and d.[Status] = 'enrolled'
                        )
                        
                        SELECT 
                            t1.AccountNo,
                            t1.Total_Paid AS TotalPaid,
                            t5.FirstPaidDate,
                            t5.First_Paid_Amount As FirstPaymentAmount,
                            t5.First_MPesaCode as FirstMPesaCode,
                            t4.LastPaidDate,
                            t4.Last_Paid_Amount AS LastPaymentAmount,
                            t4.Last_MPesaCode AS LastMPesaCode,
                            d.Make,
                            d.Model,
                            d.LastConnectedAt,
                            d.Locked,
                            d.EnrolledOn,
                            d.DeviceGroupId,
                            d.[Name],
                            d.ImeiNo,
                            d.NextLockDate,
                        	t6.TotalAmount,
                            d.Status,
                            d.LockType
                        FROM PTable1 t1
                        JOIN Devices d 
                          ON t1.AccountNo = d.Id
                        JOIN PTable5 t5 
                          ON t1.AccountNo = t5.AccountNo
                        JOIN PTable4 t4 
                          ON t1.AccountNo = t4.AccountNo
                        JOIN ContractInf0 t6
                        	ON t1.AccountNo = t6.Id
                        WHERE d.[Status] = 'enrolled'
                        AND (@DealerId = 0
                            OR d.DeviceGroupId = @DealerId
                            )
                        AND (@searchParam IS NULL
                            OR t1.AccountNo LIKE '%' + @searchParam + '%'
                            OR t5.First_MPesaCode LIKE '%' + @searchParam + '%'
                            )
                        ORDER BY t1.AccountNo
                        OFFSET @offset ROWS 
                        FETCH NEXT @pageSize ROWS ONLY;";

            var searchParam = string.IsNullOrWhiteSpace(searchTerm) ? null : searchTerm;
            var payments = await _db.QueryAsync<PaymentSummary>(sql, new { DealerId = deviceGroupId, offset, pageSize, searchParam });

            return new PaymentsViewModel() 
            { 
                CurrentPage = page,
                Payments = payments.ToList(),
                SearchTerm = searchTerm,
                TotalPages = (int)Math.Ceiling((double)totalRecords / pageSize),
                TotalRecords = totalRecords
            };
        }

        private string SetPaymentSummaryQuery()
        {
            return @"WITH 
                        ValidPayments AS (
                            SELECT *
                            FROM KosePayments
                            WHERE TRY_CAST(AccountNo AS BIGINT) IS NOT NULL
                        ),
                        PTable1 AS (
                            SELECT 
                                TRY_CAST(LTRIM(RTRIM(AccountNo)) AS BIGINT) AS AccountNo, 
                                SUM(TRY_CAST(Amount AS DECIMAL(18,2))) AS Total_Paid
                            FROM ValidPayments
                            GROUP BY TRY_CAST(LTRIM(RTRIM(AccountNo)) AS BIGINT)
                        ),
                        PTable4 AS (
                            SELECT 
                                TRY_CAST(LTRIM(RTRIM(p.AccountNo)) AS BIGINT) AS AccountNo,
                                p.Amount AS Last_Paid_Amount,
                                p.PaymentDate AS LastPaidDate,
                                p.MpesaCode AS Last_MPesaCode
                            FROM ValidPayments p
                            INNER JOIN (
                                SELECT TRY_CAST(LTRIM(RTRIM(AccountNo)) AS BIGINT) AS AccountNo, MAX(PaymentDate) AS Last_Payment_Date
                                FROM ValidPayments
                                GROUP BY TRY_CAST(LTRIM(RTRIM(AccountNo)) AS BIGINT)
                            ) t3 
                              ON TRY_CAST(LTRIM(RTRIM(p.AccountNo)) AS BIGINT) = t3.AccountNo 
                             AND p.PaymentDate = t3.Last_Payment_Date	
                        ),
                        PTable5 AS (
                            SELECT 
                                TRY_CAST(LTRIM(RTRIM(p.AccountNo)) AS BIGINT) AS AccountNo,
                                TRY_CAST(p.Amount AS DECIMAL(18,2)) AS First_Paid_Amount,
                                p.PaymentDate AS FirstPaidDate,
                                p.MpesaCode AS First_MPesaCode
                            FROM ValidPayments p
                            INNER JOIN (
                                SELECT TRY_CAST(LTRIM(RTRIM(AccountNo)) AS BIGINT) AS AccountNo, MIN(PaymentDate) AS First_Payment_Date
                                FROM ValidPayments
                                GROUP BY TRY_CAST(LTRIM(RTRIM(AccountNo)) AS BIGINT)
                            ) t2 
                              ON TRY_CAST(LTRIM(RTRIM(p.AccountNo)) AS BIGINT) = t2.AccountNo 
                             AND p.PaymentDate = t2.First_Payment_Date
                        ),
                        ContractInf0 AS (
                        	select d.Id, wo.TotalAmount 
                        	from Devices d
                        	inner join KosePayments p on TRY_CAST(LTRIM(RTRIM(p.AccountNo )) AS BIGINT) = TRY_CAST(LTRIM(RTRIM(d.Id )) AS BIGINT)
                        	left join Woo_Orders wo on wo.MpesaDepositRef = p.MpesaCode
                        	where  wo.MpesaDepositRef is not null
                            and d.[Status] = 'enrolled'
                        )
                        
                        SELECT 
                            COUNT(*)
                        FROM PTable1 t1
                        JOIN Devices d 
                          ON t1.AccountNo = d.Id
                        JOIN PTable5 t5 
                          ON t1.AccountNo = t5.AccountNo
                        JOIN PTable4 t4 
                          ON t1.AccountNo = t4.AccountNo
                        JOIN ContractInf0 t6
                        	ON t1.AccountNo = t6.Id
                        WHERE d.[Status] = 'enrolled'
                        AND (@DealerId = 0
                            OR d.DeviceGroupId = @DealerId
                            )
                        AND (@searchParam IS NULL
                            OR t1.AccountNo LIKE '%' + @searchParam + '%'
                            OR t5.First_MPesaCode LIKE '%' + @searchParam + '%'
                            )";
        }

        public async Task CreateCustomerNote(CustomerNote newNote)
        {
            var sql = @"
            INSERT INTO [dbo].[CustomerNote]
            ([Id]
            ,[OrderId]
            ,[UserId]
            ,[Note]
            ,[Created])
           VALUES
            (@Id
            ,@OrderId
            ,@UserId
            ,@Note
            ,@Created)
        ";

            await _db.ExecuteScalarAsync<int>(sql, newNote);
        }

        public async Task<List<CustomerNote>> GetNotesByOrderId(long orderId)
        {
            var sql = @" SELECT *
                        FROM [dbo].[CustomerNote]
                          WHERE OrderId = @OrderId";

            var records = await _db.QueryAsync<CustomerNote>(sql, new { OrderId = orderId });

            return records.ToList();
        }

        public async Task<WooOrderProduct?> GetProductDetailsForOrder(long orderId)
        {
            var sql = @" SELECT *
                        FROM [dbo].[Woo_OrderProduct]
                          WHERE OrderId = @OrderId";

            var record = await _db.QueryFirstOrDefaultAsync<WooOrderProduct>(sql, new { OrderId = orderId });

            return record;
        }

        public async Task<Contact?> GetNextOfKinForOrder(long orderId)
        {
            var sql = @" SELECT *
                        FROM [dbo].[Woo_Orders_NextOfKin]
                          WHERE OrderId = @OrderId";

            var record = await _db.QueryFirstOrDefaultAsync<Contact>(sql, new { OrderId = orderId });

            return record;
        }

        public async Task<KosePaymentsViewModel> GetPaymentsForAccount(string? customerAccount, int page = 1, int pageSize = 10)
        {
            var offset = (page - 1) * pageSize;

            var sql = @"SELECT *
                FROM [dbo].[KosePayments] 
                WHERE [AccountNo] = @AccountNo
                ORDER BY [PaymentDateValue] DESC
                OFFSET @Offset ROWS 
                FETCH NEXT @pageSize ROWS ONLY";
            var payments = await _db.QueryAsync<KosePayments>(sql, new { AccountNo = customerAccount, offset, pageSize });

            var countSql = @"SELECT COUNT(*)
                FROM [dbo].[KosePayments] 
                WHERE [AccountNo] = @AccountNo";

            var totalRecords = await _db.QuerySingleAsync<int>(countSql, new { AccountNo = customerAccount });

            return new KosePaymentsViewModel()
            {
                CurrentPage = page,
                Payments = payments.ToList(),
                TotalPages = (int)Math.Ceiling((double)totalRecords / pageSize)
            };
        }

        public async Task<AwaitingApprovalViewModel> GetAllNeverPaidOrdersAsync(string searchTerm = "", int page = 1, int pageSize = 10)
        {
            var offset = (page - 1) * pageSize;

            var countSql = @"    SELECT 
                            COUNT(*)
                        FROM Woo_Orders wo
                        WHERE wo.[Status] in ('approved', 'approval-waiting')
                          AND NOT EXISTS (
                              SELECT 1
                              FROM KosePayments kp
                              WHERE kp.MpesaCode = wo.MpesaDepositRef
                          )
                        AND (
                        @SearchTerm IS NULL
                        OR WO.FirstName LIKE '%' + @SearchTerm + '%'
                        OR WO.DealerRef LIKE '%' + @SearchTerm + '%'
                        OR WO.Email LIKE '%' + @SearchTerm + '%'
                        OR wo.MpesaDepositRef LIKE '%' + @SearchTerm + '%'
                    )";
            var searchParam = string.IsNullOrWhiteSpace(searchTerm) ? null : searchTerm;
            var totalRecords = await _db.QuerySingleAsync<int>(countSql, new { SearchTerm = searchParam});

            var sql = @"SELECT wo.[Id]
                            ,[OrderID]
                            ,wo.[Status]
                            ,[DateCreated]
                            ,[FirstName]
                            ,[LastName]
                            ,[Address1]
                            ,wo.[Email]
                            ,wo.[Phone]
                            ,[NationalId]
                            ,[DealerRef]
                            ,[MpesaDepositRef]
                            ,DATEDIFF(DAY, wo.DateCreated, GETDATE()) AS DaysUnpaid
                        FROM Woo_Orders wo
                        WHERE wo.[Status] in ('approved', 'approval-waiting')
                          AND NOT EXISTS (
                              SELECT 1
                              FROM KosePayments kp
                              WHERE kp.MpesaCode = wo.MpesaDepositRef
                          )
                        AND (
                        @SearchTerm IS NULL
                        OR WO.FirstName LIKE '%' + @SearchTerm + '%'
                        OR WO.DealerRef LIKE '%' + @SearchTerm + '%'
                        OR WO.Email LIKE '%' + @SearchTerm + '%'
                        OR wo.MpesaDepositRef LIKE '%' + @SearchTerm + '%'
                        )
                        ORDER BY [DateCreated] DESC
                        OFFSET @Offset ROWS 
                        FETCH NEXT @pageSize ROWS ONLY";

            var records = await _db.QueryAsync<AwaitingApprovalDto>(sql, new { SearchTerm = searchParam, offset, pageSize });

            return new AwaitingApprovalViewModel()
            {
                AwaitingApprovals = records.ToList(),
                CurrentPage = page,
                TotalPages = (int)Math.Ceiling((double)totalRecords / pageSize)
            };
        }

        #region Dashboard
        public async Task<List<CustomerDetails>> GetRecentCustomers(int dealerId = 0)
        {
            var sql = @" SELECT TOP (4) wo.OrderID,
                        		 wo.[Status],
                        		 wo.DateCreated,
                        		 wo.DateModified,
                        		 wo.TotalAmount,
                        		 wo.CustomerId,
                        		 wo.FirstName,
                        		 wo.LastName,
                        		 wo.Address1,
                        		 wo.Email,
                        		 wo.Phone,
                        		 wo.IMEI,
                        		 wo.NationalId,
                        		 wo.DealerRef,
                        		 wo.MpesaDepositRef
                          FROM [dbo].[Woo_Orders] wo
                          order by wo.DateCreated desc";

            var result = await _db.QueryAsync<CustomerDetails>(sql, new { DealerId = dealerId });
            if (dealerId != 0)
                sql = SetDealerRecentCustomersQueries();

            return result.ToList();
        }

        private string SetDealerRecentCustomersQueries()
        {
            return @"DECLARE @DealerId AS INT = 2
                    SELECT TOP (4) 
                        wo.OrderID,
                        wo.[Status],
                        wo.DateCreated,
                        wo.DateModified,
                        wo.TotalAmount,
                        wo.CustomerId,
                        wo.FirstName,
                        wo.LastName,
                        wo.Address1,
                        wo.Email,
                        wo.Phone,
                        wo.IMEI,
                        wo.NationalId,
                        wo.DealerRef,
                        wo.MpesaDepositRef
                    FROM Woo_Orders wo
                    LEFT JOIN KosePayments kp 
                        ON wo.MpesaDepositRef = kp.MpesaCode
                    LEFT JOIN Devices d 
                        ON TRY_CAST(kp.AccountNo AS bigint) = d.Id
                    LEFT JOIN Dealers dl 
                        ON dl.DealerReference = d.DeviceGroupId
                    WHERE dl.DealerId = @DealerId
                    ORDER BY wo.DateCreated DESC;";
        }

        public async Task<DashboardTotals> GetDashboardTotals(int dealerId = 0)
        {
            var sql = @"SELECT 
                        -- Today
                        SUM(CASE WHEN CAST(DateCreated AS DATE) = CAST(GETDATE() AS DATE) 
                                 THEN TotalAmount ELSE 0 END) AS TotalToday,
                        COUNT(CASE WHEN CAST(DateCreated AS DATE) = CAST(GETDATE() AS DATE) 
                                   THEN 1 END) AS CountToday,
                    
                        -- Yesterday
                        SUM(CASE WHEN CAST(DateCreated AS DATE) = CAST(DATEADD(DAY, -1, GETDATE()) AS DATE) 
                                 THEN TotalAmount ELSE 0 END) AS TotalYesterday,
                        COUNT(CASE WHEN CAST(DateCreated AS DATE) = CAST(DATEADD(DAY, -1, GETDATE()) AS DATE) 
                                   THEN 1 END) AS CountYesterday,
                    
                        -- This Week (Mon–Sun)
                        SUM(CASE WHEN DATEADD(WEEK, DATEDIFF(WEEK, 0, DateCreated), 0) =
                                       DATEADD(WEEK, DATEDIFF(WEEK, 0, GETDATE()), 0)
                                 THEN TotalAmount ELSE 0 END) AS TotalThisWeek,
                        COUNT(CASE WHEN DATEADD(WEEK, DATEDIFF(WEEK, 0, DateCreated), 0) =
                                         DATEADD(WEEK, DATEDIFF(WEEK, 0, GETDATE()), 0)
                                   THEN 1 END) AS CountThisWeek,
                    
                        -- Last Week (Mon–Sun)
                        SUM(CASE WHEN DATEADD(WEEK, DATEDIFF(WEEK, 0, DateCreated), 0) =
                                       DATEADD(WEEK, DATEDIFF(WEEK, 0, GETDATE()), 0) - 7
                                 THEN TotalAmount ELSE 0 END) AS TotalLastWeek,
                        COUNT(CASE WHEN DATEADD(WEEK, DATEDIFF(WEEK, 0, DateCreated), 0) =
                                         DATEADD(WEEK, DATEDIFF(WEEK, 0, GETDATE()), 0) - 7
                                   THEN 1 END) AS CountLastWeek,
                    
                        -- This Month
                        SUM(CASE WHEN MONTH(DateCreated) = MONTH(GETDATE()) 
                                   AND YEAR(DateCreated) = YEAR(GETDATE())
                                 THEN TotalAmount ELSE 0 END) AS TotalThisMonth,
                        COUNT(CASE WHEN MONTH(DateCreated) = MONTH(GETDATE()) 
                                     AND YEAR(DateCreated) = YEAR(GETDATE())
                                   THEN 1 END) AS CountThisMonth,
                    
                        -- Last Month
                        SUM(CASE WHEN MONTH(DateCreated) = MONTH(DATEADD(MONTH, -1, GETDATE())) 
                                   AND YEAR(DateCreated) = YEAR(DATEADD(MONTH, -1, GETDATE()))
                                 THEN TotalAmount ELSE 0 END) AS TotalLastMonth,
                        COUNT(CASE WHEN MONTH(DateCreated) = MONTH(DATEADD(MONTH, -1, GETDATE())) 
                                     AND YEAR(DateCreated) = YEAR(DATEADD(MONTH, -1, GETDATE()))
                                   THEN 1 END) AS CountLastMonth,
                        SUM(CASE 
                                WHEN DateCreated >= DATEADD(WEEK, DATEDIFF(WEEK, 0, GETDATE()), 0) 
                                 AND DateCreated <  DATEADD(DAY, 1, CAST(GETDATE() AS DATE)) 
                             THEN TotalAmount ELSE 0 END) AS TotalThisWeekSoFar,

                        COUNT(CASE 
                                  WHEN DateCreated >= DATEADD(WEEK, DATEDIFF(WEEK, 0, GETDATE()), 0) 
                                   AND DateCreated <  DATEADD(DAY, 1, CAST(GETDATE() AS DATE)) 
                               THEN 1 END) AS CountThisWeekSoFar,
						 (SELECT COUNT(DISTINCT OrderID) 
							FROM Woo_Orders 
							WHERE [Status] IN ('approval-waiting', 'approved')) AS TotalOrders,
						(SELECT COUNT(DISTINCT Id) 
							FROM [Devices] 
							WHERE [Status] = 'enrolled') AS TotalDevices,
						(SELECT COUNT(DISTINCT UserId) 
							FROM [Users] 
							WHERE [Status] = 1) AS TotalUsers,
						(SELECT COUNT(DISTINCT Id) 
							FROM [KosePayments]) AS TotalTransactions
                FROM Woo_Orders;";

            if (dealerId != 0)
                sql = SetDealerQueries();

            var records = await _db.QueryFirstAsync<DashboardTotals>(sql, new { DealerId = dealerId });

            return records;
        }

        private string SetDealerQueries()
        {
            return @"		SELECT 
                        -- Today
                        SUM(CASE WHEN CAST(DateCreated AS DATE) = CAST(GETDATE() AS DATE) 
                                 THEN TotalAmount ELSE 0 END) AS TotalToday,
                        COUNT(CASE WHEN CAST(DateCreated AS DATE) = CAST(GETDATE() AS DATE) 
                                   THEN 1 END) AS CountToday,
                    
                        -- Yesterday
                        SUM(CASE WHEN CAST(DateCreated AS DATE) = CAST(DATEADD(DAY, -1, GETDATE()) AS DATE) 
                                 THEN TotalAmount ELSE 0 END) AS TotalYesterday,
                        COUNT(CASE WHEN CAST(DateCreated AS DATE) = CAST(DATEADD(DAY, -1, GETDATE()) AS DATE) 
                                   THEN 1 END) AS CountYesterday,
                    
                        -- This Week (Mon–Sun)
                        SUM(CASE WHEN DATEADD(WEEK, DATEDIFF(WEEK, 0, DateCreated), 0) =
                                       DATEADD(WEEK, DATEDIFF(WEEK, 0, GETDATE()), 0)
                                 THEN TotalAmount ELSE 0 END) AS TotalThisWeek,
                        COUNT(CASE WHEN DATEADD(WEEK, DATEDIFF(WEEK, 0, DateCreated), 0) =
                                         DATEADD(WEEK, DATEDIFF(WEEK, 0, GETDATE()), 0)
                                   THEN 1 END) AS CountThisWeek,
                    
                        -- Last Week (Mon–Sun)
                        SUM(CASE WHEN DATEADD(WEEK, DATEDIFF(WEEK, 0, DateCreated), 0) =
                                       DATEADD(WEEK, DATEDIFF(WEEK, 0, GETDATE()), 0) - 7
                                 THEN TotalAmount ELSE 0 END) AS TotalLastWeek,
                        COUNT(CASE WHEN DATEADD(WEEK, DATEDIFF(WEEK, 0, DateCreated), 0) =
                                         DATEADD(WEEK, DATEDIFF(WEEK, 0, GETDATE()), 0) - 7
                                   THEN 1 END) AS CountLastWeek,
                    
                        -- This Month
                        SUM(CASE WHEN MONTH(DateCreated) = MONTH(GETDATE()) 
                                   AND YEAR(DateCreated) = YEAR(GETDATE())
                                 THEN TotalAmount ELSE 0 END) AS TotalThisMonth,
                        COUNT(CASE WHEN MONTH(DateCreated) = MONTH(GETDATE()) 
                                     AND YEAR(DateCreated) = YEAR(GETDATE())
                                   THEN 1 END) AS CountThisMonth,
                    
                        -- Last Month
                        SUM(CASE WHEN MONTH(DateCreated) = MONTH(DATEADD(MONTH, -1, GETDATE())) 
                                   AND YEAR(DateCreated) = YEAR(DATEADD(MONTH, -1, GETDATE()))
                                 THEN TotalAmount ELSE 0 END) AS TotalLastMonth,
                        COUNT(CASE WHEN MONTH(DateCreated) = MONTH(DATEADD(MONTH, -1, GETDATE())) 
                                     AND YEAR(DateCreated) = YEAR(DATEADD(MONTH, -1, GETDATE()))
                                   THEN 1 END) AS CountLastMonth,
                        SUM(CASE 
                                WHEN DateCreated >= DATEADD(WEEK, DATEDIFF(WEEK, 0, GETDATE()), 0) 
                                 AND DateCreated <  DATEADD(DAY, 1, CAST(GETDATE() AS DATE)) 
                             THEN TotalAmount ELSE 0 END) AS TotalThisWeekSoFar,

                        COUNT(CASE 
                                  WHEN DateCreated >= DATEADD(WEEK, DATEDIFF(WEEK, 0, GETDATE()), 0) 
                                   AND DateCreated <  DATEADD(DAY, 1, CAST(GETDATE() AS DATE)) 
                               THEN 1 END) AS CountThisWeekSoFar,
						 (SELECT COUNT(DISTINCT OrderID) 
							FROM Woo_Orders wo INNER JOIN KosePayments kp
							ON wo.MpesaDepositRef = KP.MpesaCode
							INNER JOIN Devices d
							ON d.Id = TRY_CAST(kp.AccountNo AS bigint)
							where d.DeviceGroupId = @DealerId 
							AND wo.[Status] IN ('approval-waiting', 'approved')) AS TotalOrders,
						(SELECT COUNT(DISTINCT Id) 
							FROM [Devices] 
							WHERE [Status] = 'enrolled'
							AND DeviceGroupId = @DealerId ) AS TotalDevices,
						(SELECT COUNT(DISTINCT U.UserId) 
							FROM [Users] U
							INNER JOIN [dbo].[Dealers] D
							ON U.UserId = D.UserId
							WHERE [Status] = 1
							AND D.DealerReference = @DealerId) AS TotalUsers,
						(SELECT COUNT(DISTINCT kp.Id) 
							FROM [KosePayments] kp
							INNER JOIN Devices D
							ON d.Id = TRY_CAST(kp.AccountNo AS bigint)
							WHERE D.DeviceGroupId = @DealerId) AS TotalTransactions
                FROM Woo_Orders wo
				INNER JOIN KosePayments kp
				ON wo.MpesaDepositRef = KP.MpesaCode
				INNER JOIN Devices d
				ON d.Id = TRY_CAST(kp.AccountNo AS bigint)
				where d.DeviceGroupId = @DealerId";
        }

        public async Task<List<TransactionHistory>> GetTransactionHistory(int dealerId = 0)
        {
            var sql = @"SELECT TOP(4) TRY_CAST(kp.AccountNo AS bigint) AccountNo
                                ,[Status]
                                ,[DateCreated]
                                ,[FirstName]
                                ,[LastName]
                                ,[DealerRef]
                                ,[MpesaDepositRef]
                                ,SUM(AmountValue) Amount
                            FROM [dbo].[Woo_Orders] WO
                            LEFT JOIN KosePayments KP
                            ON WO.MpesaDepositRef = KP.MpesaCode
	                        WHERE KP.[AccountNo] IS NOT NULL
	                        AND [Status] IN ('approved', 'approval-waiting')
	                        GROUP BY KP.[AccountNo]
	                            ,[Status]
                                ,[DateCreated]
                                ,[FirstName]
                                ,[LastName]
                                ,[DealerRef]
                                ,[MpesaDepositRef]
	                        ORDER BY [DateCreated] desc";

            if (dealerId != 0)
                sql = SetDealerTransactionHistoryQueries();

            var result = await _db.QueryAsync<TransactionHistory>(sql, new { DealerId = dealerId });

            return result.ToList();
        }

        private string SetDealerTransactionHistoryQueries()
        {
            return @"
                        SELECT TOP(4) TRY_CAST(kp.AccountNo AS bigint) AccountNo
                                ,WO.[Status]
                                ,[DateCreated]
                                ,[FirstName]
                                ,[LastName]
                                ,[DealerRef]
                                ,[MpesaDepositRef]
                                ,SUM(AmountValue) Amount
                            FROM [dbo].[Woo_Orders] WO
                            LEFT JOIN KosePayments KP
                            ON WO.MpesaDepositRef = KP.MpesaCode
							INNER JOIN Devices d on TRY_CAST(kp.AccountNo AS bigint) = d.Id
    						INNER JOIN Dealers dl on dl.DealerReference = d.DeviceGroupId
    						WHERE dl.DealerId = @DealerId
    						AND d.[Status] = 'enrolled'
	                        AND KP.[AccountNo] IS NOT NULL
	                        AND WO.[Status] IN ('approved', 'approval-waiting')
	                        GROUP BY KP.[AccountNo]
	                            ,WO.[Status]
                                ,[DateCreated]
                                ,[FirstName]
                                ,[LastName]
                                ,[DealerRef]
                                ,[MpesaDepositRef]
	                        ORDER BY [DateCreated] desc";
        }
        #endregion

        #region Restructured
        public async Task InsertRestructured(RestructuredRecord restructuringRecord)
        {
            var sqlInsert = @"
                            INSERT INTO RestructuredRecords
                            (
                                AccountNo,
                                Date_Agreed,
                                Amount_Res,
                                Days_Restructured,
                                Total_Due_R,
                                Total_Paid_R,
                                Arrears_R,
                                Auto_lock_date_pmt_R
                            )
                            VALUES
                            (
                                @AccountNo,
                                @DateAgreed,
                                @AmountRes,
                                @DaysRestructured,
                                @TotalDueR,
                                @TotalPaidR,
                                @ArrearsR,
                                @AutoLockDatePmtR
                            );";

            
                await _db.ExecuteAsync(sqlInsert, restructuringRecord);
        }

        public async Task<List<RestructuredRecord>> GetAllRestructured()
        {
            var sqlSelectAll = @"
                SELECT 
                    Id,
                    Date_Agreed AS DateAgreed,
                    Amount_Res AS AmountRes,
                    Days_Restructured AS DaysRestructured,
                    Total_Due_R AS TotalDueR,
                    Total_Paid_R AS TotalPaidR,
                    Arrears_R AS ArrearsR,
                    Auto_lock_date_pmt_R AS AutoLockDatePmtR
                FROM RestructuredRecords;";

            var records = await _db.QueryAsync<RestructuredRecord>(sqlSelectAll);
            return records.ToList();
        }

        public async Task<List<RestructuredRecord>> GetAllRestructuredForAccount(long accountId)
        {
            var sqlSelectAll = @"
                SELECT 
                    Id,
                    Date_Agreed AS DateAgreed,
                    Amount_Res AS AmountRes,
                    Days_Restructured AS DaysRestructured,
                    Total_Due_R AS TotalDueR,
                    Total_Paid_R AS TotalPaidR,
                    Arrears_R AS ArrearsR,
                    Auto_lock_date_pmt_R AS AutoLockDatePmtR
                FROM RestructuredRecords
                WHERE Amount_Res = @AccountId;";

            var records = await _db.QueryAsync<RestructuredRecord>(sqlSelectAll, new {AccountId = accountId});
            return records.ToList();
        }

        #endregion

    }
}
