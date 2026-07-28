using Ranalo.Models;
using System.Data;
using Dapper;
using System.Drawing.Printing;
using Ranalo.Woocommece.Api.Models;
using Microsoft.Identity.Client;
using DocumentFormat.OpenXml.Wordprocessing;
using DocumentFormat.OpenXml.Office2010.Excel;
using DocumentFormat.OpenXml.Drawing;
using iText.Kernel.Geom;

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
                WHERE [Status] IN ('approval-waiting', 'approved')
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
                    ,WO.[FirstName]
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
                WHERE [Status] IN ('approval-waiting', 'approved')
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
	                    INNER JOIN Devices d on kp.AccountNoBigint = d.Id
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
                            ,wo.[FirstName]
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
	                    INNER JOIN Devices d on kp.AccountNoBigint = d.Id
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
                            WHERE AccountNoBigint IS NOT NULL
                        ),
                        PTable1 AS (
                            SELECT 
                                AccountNoBigint AS AccountNo, 
                                SUM(AmountValue) AS Total_Paid
                            FROM ValidPayments
                            GROUP BY AccountNoBigint
                        ),
                        PTable4 AS (
                            SELECT 
                                p.AccountNoBigint AS AccountNo,
                                p.AmountValue AS Last_Paid_Amount,
                                p.PaymentDate AS LastPaidDate,
                                p.MpesaCode AS Last_MPesaCode
                            FROM ValidPayments p
                            INNER JOIN (
                                SELECT AccountNoBigint AS AccountNo, MAX(PaymentDateValue) AS Last_Payment_Date
                                FROM ValidPayments
                                GROUP BY AccountNoBigint
                            ) t3 
                              ON p.AccountNoBigint = t3.AccountNo 
                             AND p.PaymentDateValue = t3.Last_Payment_Date	
                        ),
                        PTable5 AS (
                            SELECT 
                                p.AccountNoBigint AS AccountNo,
                                p.AmountValue AS First_Paid_Amount,
                                p.PaymentDateValue AS FirstPaidDate,
                                p.MpesaCode AS First_MPesaCode
                            FROM ValidPayments p
                            INNER JOIN (
                                SELECT AccountNoBigint AS AccountNo, MIN(PaymentDateValue) AS First_Payment_Date
                                FROM ValidPayments
                                GROUP BY AccountNoBigint
                            ) t2 
                              ON p.AccountNoBigint = t2.AccountNo 
                             AND p.PaymentDateValue = t2.First_Payment_Date
                        ),
                        ContractInf0 AS (
                        	select d.Id, 
							ci.TotalAmount,
							ci.First_Name as CustomerName,
                            ci.Daily,
                            ci.Deposit,
                            ci.Weekly,
                            ci.Monthly
                        	from Devices d
                        	INNER join KosePayments p on p.AccountNoBigint = d.Id
                        	INNER join Contract_Info ci on ci.ID = p.AccountNoBigint
                            AND ci.EndDate IS NULL
                        	--where  wo.MpesaDepositRef is not null
                            where d.[Status] = 'enrolled'
                            GROUP BY d.Id, ci.TotalAmount, ci.First_Name,
                            ci.Daily,
                            ci.Deposit,
                            ci.Weekly,
                            ci.Monthly
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
                            d.LockType,
                            t6.Deposit,
                            t6.Weekly,
                            t6.Monthly,
                            t6.Daily
							ORDER BY t5.FirstPaidDate DESC
							";
            var searchParam = string.IsNullOrWhiteSpace(searchTerm) ? null : searchTerm;
            var queryRecords = await _db.QueryAsync<int>(countSql, new { SearchTerm = searchParam, dealerId});
            var totalRecords = queryRecords.Count();
            var sql = @"WITH 
                ValidPayments AS (
                    SELECT *
                    FROM KosePayments
                    WHERE AccountNoBigint IS NOT NULL
                ),
                PTable1 AS (
                    SELECT 
                        AccountNoBigint AS AccountNo, 
                        SUM(AmountValue) AS Total_Paid
                    FROM ValidPayments
                    GROUP BY AccountNoBigint
                ),
                PTable4 AS (
                    SELECT 
                        p.AccountNoBigint AS AccountNo,
                        p.Amount AS Last_Paid_Amount,
                        p.PaymentDateValue AS LastPaidDate,
                        p.MpesaCode AS Last_MPesaCode
                    FROM ValidPayments p
                    INNER JOIN (
                        SELECT AccountNoBigint AS AccountNo, MAX(PaymentDate) AS Last_Payment_Date
                        FROM ValidPayments
                        GROUP BY AccountNoBigint
                    ) t3 
                        ON p.AccountNoBigint = t3.AccountNo 
                        AND p.PaymentDateValue = t3.Last_Payment_Date	
                ),
                PTable5 AS (
                    SELECT 
                        p.AccountNoBigint AS AccountNo,
                        p.AmountValue AS First_Paid_Amount,
                        p.PaymentDateValue AS FirstPaidDate,
                        p.MpesaCode AS First_MPesaCode
                    FROM ValidPayments p
                    INNER JOIN (
                        SELECT AccountNoBigint AS AccountNo, MIN(PaymentDateValue) AS First_Payment_Date
                        FROM ValidPayments
                        GROUP BY AccountNoBigint
                    ) t2 
                        ON p.AccountNoBigint = t2.AccountNo 
                        AND p.PaymentDate = t2.First_Payment_Date
                ),
                ContractInf0 AS (
                    select d.Id, 
                	ci.TotalAmount ,
                	ci.First_Name as CustomerName,
                    ci.Deposit,
                    ci.Daily,
                    ci.Weekly,
                    ci.Monthly,
                	ci.Term_in_Months AS TermsInMonths
                    from Devices d
                    INNER join KosePayments p on p.AccountNoBigint = d.Id
                    INNER join Contract_Info ci on ci.ID = p.AccountNoBigint
                    AND ci.EndDate IS NULL
                    --where  wo.MpesaDepositRef is not null
                    where d.[Status] = 'enrolled'
                    GROUP BY 
                    d.Id, 
                    ci.TotalAmount, 
                    ci.First_Name,
                    ci.Daily,
                    ci.Deposit,
                    ci.Weekly,
                    ci.Monthly,
                	ci.Term_in_Months
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
                    d.LockType,
                    t6.Daily,
                    t6.Deposit,
                    t6.Weekly,
                    t6.Monthly,
                	t6.TermsInMonths
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
                    d.LockType,
                    t6.Daily,
                    t6.Deposit,
                    t6.Weekly,
                    t6.Monthly,
                	t6.TermsInMonths
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
	                    INNER JOIN Devices d on kp.AccountNoBigint = d.Id
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
                            ,wo.[FirstName]
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
	                    INNER JOIN Devices d on kp.AccountNoBigint = d.Id
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
                FROM [dbo].[Woo_Orders] 
                ORDER BY [DateCreated] DESC";
            return await _db.QueryAsync<AwaitingApprovalDto>(sql);
        }

        public async Task<KosePaymentsViewModel> GetOrphanedPaymentsAsync(int page, int pageSize, string searchTearm = "")
        {
            var offset = (page - 1) * pageSize;

            var countSql = @"WITH PaymentsNoDevice AS (
                            SELECT kp.*
                            FROM KosePayments kp
                            WHERE NOT EXISTS (
                                SELECT 1 
                                FROM Devices d 
                                WHERE d.Id = kp.AccountNoBigint
                            )
                        ),
                        PaymentsLinkedToOrphaned AS (
                            SELECT pnd.*, op.AccountNoBigint AS OrphanedAccountNoBigint
                            FROM PaymentsNoDevice pnd
                            LEFT JOIN OrphanedPayments op 
                                ON op.OrphanedAccountNo = pnd.AccountNo
                        )
                        SELECT COUNT(*)
                        FROM PaymentsLinkedToOrphaned plo
                        LEFT JOIN Devices d 
                            ON d.Id = plo.AccountNoBigint
                        WHERE d.Id IS NULL
                        AND (
                        @SearchTerm IS NULL
                         OR plo.FirstName LIKE '%' + @SearchTerm + '%'
                         OR d.DeviceGroupId LIKE '%' + @SearchTerm + '%'
                         OR plo.AccountNoBigint LIKE '%' + @SearchTerm + '%'
                         OR plo.OrphanedAccountNoBigint LIKE '%' + @SearchTerm + '%'
                         OR plo.MpesaCode LIKE '%' + @SearchTerm + '%'
                        );";

            var sql = @"WITH PaymentsNoDevice AS (
                            -- Step 1: payments with no matching device
                            SELECT kp.*
                            FROM KosePayments kp
                            WHERE NOT EXISTS (
                                SELECT 1 
                                FROM Devices d 
                                WHERE d.Id = kp.AccountNoBigint
                            )
                        ),
                        PaymentsLinkedToOrphaned AS (
                            -- Step 2: link to OrphanedPayments using AccountNo
                            SELECT pnd.*, op.AccountNoBigint AS OrphanedAccountNoBigint
                            FROM PaymentsNoDevice pnd
                            LEFT JOIN OrphanedPayments op 
                                ON op.OrphanedAccountNo = pnd.AccountNo
                        )
                        -- Step 3: remove any payment that now has a device
                        SELECT plo.*
                        FROM PaymentsLinkedToOrphaned plo
                        LEFT JOIN Devices d 
                        on d.Id = plo.AccountNoBigint
	                    WHERE d.Id IS NULL
                        AND (
                        @SearchTerm IS NULL
                         OR plo.FirstName LIKE '%' + @SearchTerm + '%'
                         OR d.DeviceGroupId LIKE '%' + @SearchTerm + '%'
                         OR plo.AccountNoBigint LIKE '%' + @SearchTerm + '%'
                         OR plo.OrphanedAccountNoBigint LIKE '%' + @SearchTerm + '%'
                         OR plo.MpesaCode LIKE '%' + @SearchTerm + '%'
                        )
                        ORDER BY plo.PaymentDateValue DESC
                        OFFSET @Offset ROWS 
                        FETCH NEXT @pageSize ROWS ONLY";

            var payments = await _db.QueryAsync<KosePayments>(sql, new { SearchTerm = searchTearm, offset, pageSize });
            var totalRecords = await _db.QuerySingleAsync<int>(countSql, new { SearchTerm = searchTearm });

            return new KosePaymentsViewModel()
            {
                CurrentPage = page,
                Payments = payments.ToList(),
                TotalPages = (int)Math.Ceiling((double)totalRecords / pageSize)
            };


        }

        public async Task<PaymentsSummaryTotalsViewModel> GetPaymentsSummaryAsync(string searchTerm = "", int page = 1, int pageSize = 10)
        {
            var offset = (page - 1) * pageSize;

            var countsql = @" SELECT COUNT(DISTINCT kp.AccountNoBigint)
                                FROM KosePayments kp
                                WHERE (
                                    @SearchTerm IS NULL
                                    OR kp.AccountNo LIKE '%' + @SearchTerm + '%'
                                    OR CAST(kp.AmountValue AS NVARCHAR(50)) LIKE '%' + @SearchTerm + '%'
                                    OR CAST(kp.PaymentDateValue AS NVARCHAR(50)) LIKE '%' + @SearchTerm + '%'
                                    OR kp.MpesaCode LIKE '%' + @SearchTerm + '%'
                                );
                        ";

            var searchParam = string.IsNullOrWhiteSpace(searchTerm) ? null : searchTerm;
            var totalRecords = await _db.QuerySingleAsync<int>(countsql, new { SearchTerm = searchParam });

            var sql = @" SELECT
                            a.AccountNoBigint AS Account,

                            totals.TotalPaid,

                            fp.AmountValue AS FirstPayment,
                            fp.PaymentDateValue AS First,

                            lp.AmountValue AS LastPayment,
                            lp.PaymentDateValue AS Last

                        FROM
                        (
                            SELECT DISTINCT AccountNoBigint
                            FROM dbo.KosePayments
                            WHERE AccountNoBigint IS NOT NULL
                        ) a

                        OUTER APPLY
                        (
                            SELECT SUM(AmountValue) AS TotalPaid
                            FROM dbo.KosePayments kp
                            WHERE kp.AccountNoBigint = a.AccountNoBigint
                        ) totals

                        OUTER APPLY
                        (
                            SELECT TOP 1
                                AmountValue,
                                PaymentDateValue
                            FROM dbo.KosePayments kp
                            WHERE kp.AccountNoBigint = a.AccountNoBigint
                            ORDER BY PaymentDateValue ASC
                        ) fp

                        OUTER APPLY
                        (
                            SELECT TOP 1
                                AmountValue,
                                PaymentDateValue
                            FROM dbo.KosePayments kp
                            WHERE kp.AccountNoBigint = a.AccountNoBigint
                            ORDER BY PaymentDateValue DESC
                        ) lp

                        ORDER BY lp.PaymentDateValue DESC
                        OFFSET @Offset ROWS 
                        FETCH NEXT @pageSize ROWS ONLY";

            var payments = await _db.QueryAsync<PaymentsSummaryTotals>(sql, new { SearchTerm = searchParam, offset, pageSize });

            return new PaymentsSummaryTotalsViewModel()
            {
                CurrentPage = page,
                Payments = payments.ToList(),
                TotalPages = (int)Math.Ceiling((double)totalRecords / pageSize)
            };
        }

        public async Task<KosePaymentsViewModel> GetAllPaymentsAsync(int? dealerId, string searchTerm = "", int page = 1, int pageSize = 10)
        {
            var offset = (page - 1) * pageSize;

            var countsql = @" SELECT COUNT(*) 
                                FROM [dbo].[KosePayments] kp
                        LEFT JOIN Devices d 
                            ON kp.AccountNoBigint = d.Id
                        LEFT JOIN Dealers dl 
                            ON dl.DealerReference = d.DeviceGroupId
                        WHERE
                            -- Dealer filter (only applies if provided)
                            (
                                @dealerId IS NULL 
                                OR dl.DealerId = @dealerId
                            )
                            -- Search filter
                            AND (
                                @SearchTerm IS NULL
                                OR kp.AccountNoBigint LIKE '%' + @SearchTerm + '%'
                                OR CAST(kp.AmountValue AS NVARCHAR(50)) LIKE '%' + @SearchTerm + '%'
                                OR CAST(kp.PaymentDateValue AS NVARCHAR(50)) LIKE '%' + @SearchTerm + '%'
                                OR kp.MpesaCode LIKE '%' + @SearchTerm + '%'
                            );";

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
                            ,kp.FirstName
                        FROM [dbo].[KosePayments] kp
                        LEFT JOIN Devices d 
                            ON kp.AccountNoBigint = d.Id
                        LEFT JOIN Dealers dl 
                            ON dl.DealerReference = d.DeviceGroupId
                        WHERE
                            -- Dealer filter (only applies if provided)
                            (
                                @dealerId IS NULL 
                                OR dl.DealerId = @dealerId
                            )
                            -- Search filter
                            AND (
                                @SearchTerm IS NULL
                                OR kp.AccountNoBigint LIKE '%' + @SearchTerm + '%'
                                OR CAST(kp.AmountValue AS NVARCHAR(50)) LIKE '%' + @SearchTerm + '%'
                                OR CAST(kp.PaymentDateValue AS NVARCHAR(50)) LIKE '%' + @SearchTerm + '%'
                                OR kp.MpesaCode LIKE '%' + @SearchTerm + '%'
                            )
                        ORDER BY PaymentDateValue DESC
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

        public async Task<KosePaymentsViewModel> GetAllPaymentsByDealerIdAsync(int dealerId, string searchTerm = "", int page = 1, int pageSize = 10)
        {
            var offset = (page - 1) * pageSize;

            var countsql = @" SELECT COUNT(*) 
                        FROM [dbo].[KosePayments] kp
                        INNER JOIN Devices d on kp.AccountNoBigint = d.Id
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
                        INNER JOIN Devices d on kp.AccountNoBigint = d.Id
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

        public async Task<CustomerDetails?> GetCustomerDetailsByAccountId(int accountId)
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
                    INNER JOIN KosePayments KP 
                    ON KP.MpesaCode = wo.MpesaDepositRef
                    LEFT JOIN [dbo].[Woo_Orders_Images] woi
                    ON wo.Id = woi.OrderId
                    WHERE KP.AccountNoBigint = @AccountId";

            return await _db.QueryFirstOrDefaultAsync<CustomerDetails>(sql, new { AccountId = accountId });
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

        public async Task<IEnumerable<AccountSummary>?> GetCustomersForReminderLockFullyPaid()
        {
            var sql = @"WITH 
                            PTable1 AS (
                                SELECT 
                                    AccountNoBigint AS AccountNo, 
                                    SUM(TRY_CAST(Amount AS DECIMAL(18,2))) AS Total_Paid
                                FROM KosePayments
                                GROUP BY AccountNoBigint
                            ),
                            PTable2 AS (
                                SELECT 
                                    AccountNoBigint AS AccountNo,
                                    MIN(PaymentDateValue) AS First_Payment_Date
                                FROM KosePayments
                                GROUP BY AccountNoBigint
                            ),
                            PTable3 AS (
                                SELECT 
                                    AccountNoBigint AS AccountNo, 
                                    MAX(PaymentDateValue) AS Last_Payment_Date
                                FROM KosePayments
                                GROUP BY AccountNoBigint
                            ),
                            PTable4 AS (
                                SELECT 
                                    p.AccountNoBigint AS AccountNo,,
                                    p.AmountValue AS Last_Paid_Amount,
                                    p.PaymentDateValue AS LastPaidDate,
                                    p.MpesaCode AS Last_MPesaCode
                                FROM KosePayments p
                                INNER JOIN PTable3 t3 ON p.AccountNoBigint = t3.AccountNo
                                WHERE p.PaymentDateValue = t3.Last_Payment_Date	
                            ),
                            PTable5 AS (
                                SELECT 
                                    p.AccountNoBigint AS AccountNo,
                                    p.AmountValue AS First_Paid_Amount,
                                    p.PaymentDateValue AS FirstPaidDate,
                                    p.MpesaCode AS First_MPesaCode
                                FROM KosePayments p
                                INNER JOIN PTable2 t2 ON p.AccountNoBigint = t2.AccountNo
                                WHERE p.PaymentDateValue = t2.First_Payment_Date
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
                                LEFT JOIN PTable1 t ON p.id = t.AccountNo
                                LEFT JOIN PTable2 f ON p.id = f.AccountNo
                                LEFT JOIN PTable3 l ON p.id = l.AccountNo
                                LEFT JOIN PTable4 a ON p.id = a.AccountNo
                                LEFT JOIN PTable5 b ON p.id = b.AccountNo
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
                                SELECT b.id,
                                    b.[Status],
                                    b.Model,
                                    b.Make,
                                    b.Locked,
                                    b.LockType,
                                    b.Last_Payment_Date,
                                    b.First_Paid_Amount,
                                    b.First_Lock_Date,
                                    b.Next_Lock_Date,
                                    b.LastConnectedAt,
                                    b.Total_Paid,
                                    b.First_Payment_Date,
                                    b.FirstPaidDate,
                                    b.First_MPesaCode,
                                    b.Last_Paid_Amount,
                                    b.LastPaidDate,
                                    b.Last_MPesaCode,
                                    b.ImeiNo,
	                        		Total_Cost AS dealer_payment,
	                        		ci.Deposit  AS deposit,
	                        		ci.Daily AS daily_rate,
                                    ci.First_Name AS FirstName
	                        		FROM Base b
	                        		INNER JOIN Contract_Info ci on b.Id = ci.ID
                                    AND ci.EndDate IS NULL
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
                            		d.monthly_rate as Monthly,
	                        		d.FirstName
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
                                    ,First_Paid_Amount AS FirstAmount
                            		,Daily
                            		,Weekly
                            		,Monthly
                            		,deposit As Deposit
                            		,Total_Paid AS TotalPaid
                                    ,Contract_End_Date AS ContractEndDate
                                    ,FirstName
                                    ,Arrears
                            FROM WithTotalDue
                            where Arrears >= 0
	                        AND Contract_End_Date > GETDATE()
                            GROUP BY WooOrderID
                            ,Loan_Balance
                            , Daily
                            , Weekly
                            , Monthly
                            , deposit
                            , Total_Paid
                            ,LastPaidDate
                            ,FirstPaidDate
                            ,Contract_End_Date
                            ,First_Paid_Amount
                            ,FirstName
                            ,Arrears
                        ORDER BY LastPaidDate DESC";

            return await _db.QueryAsync<AccountSummary>(sql);
        }
        public async Task<AccountSummary?> GetPaymentSummaryForAccountId(string customerId)
        {

            var sql = @"WITH 
                        -- ============================================================
                        -- 1. Payments (Unified source table)
                        -- ============================================================
                        Payments AS (
                            SELECT
                                kp.Id,
                                kp.MpesaCode,
                                kp.Amount,
                                kp.AmountValue,
                                kp.PaymentDateValue,
                                COALESCE(op.AccountNoBigint, kp.AccountNoBigint) AS RealAccountNo
                            FROM KosePayments kp
                            LEFT JOIN OrphanedPayments op 
                                ON op.MpesaCode = kp.MpesaCode
                            WHERE COALESCE(op.AccountNoBigint, kp.AccountNoBigint) = @AccountId
                        ),

                        -- ============================================================
                        -- X. Total paid in last 24 hours
                        -- ============================================================
                        Last24 AS (
                            SELECT 
                                RealAccountNo AS AccountNo,
                                SUM(TRY_CAST(Amount AS DECIMAL(18,2))) AS Total_Last24Hours
                            FROM Payments
                            WHERE PaymentDateValue >= DATEADD(HOUR, -24, GETDATE())
                            GROUP BY RealAccountNo
                        ),

                        -- ============================================================
                        -- 2. Total paid
                        -- ============================================================
                        PTable1 AS (
                            SELECT 
                                RealAccountNo AS AccountNo,
                                SUM(TRY_CAST(Amount AS DECIMAL(18,2))) AS Total_Paid
                            FROM Payments
                            GROUP BY RealAccountNo
                        ),

                        -- ============================================================
                        -- 3. First payment date
                        -- ============================================================
                        PTable2 AS (
                            SELECT 
                                RealAccountNo AS AccountNo,
                                MIN(PaymentDateValue) AS First_Payment_Date
                            FROM Payments
                            GROUP BY RealAccountNo
                        ),

                        -- ============================================================
                        -- 4. Last payment date
                        -- ============================================================
                        PTable3 AS (
                            SELECT 
                                RealAccountNo AS AccountNo,
                                MAX(PaymentDateValue) AS Last_Payment_Date
                            FROM Payments
                            GROUP BY RealAccountNo
                        ),

                        -- ============================================================
                        -- 5. Last payment detail
                        -- ============================================================
                        PTable4 AS (
                            SELECT 
                                p.RealAccountNo AS AccountNo,
                                p.AmountValue AS Last_Paid_Amount,
                                p.PaymentDateValue AS LastPaidDate,
                                p.MpesaCode AS Last_MPesaCode
                            FROM Payments p
                            INNER JOIN PTable3 t3 
                                ON p.RealAccountNo = t3.AccountNo
                                AND p.PaymentDateValue = t3.Last_Payment_Date
                        ),

                        -- ============================================================
                        -- 6. First payment detail
                        -- ============================================================
                        PTable5 AS (
                            SELECT 
                                p.RealAccountNo AS AccountNo,
                                TRY_CAST(p.Amount AS DECIMAL(18,2)) AS First_Paid_Amount,
                                p.PaymentDateValue AS FirstPaidDate,
                                p.MpesaCode AS First_MPesaCode
                            FROM Payments p
                            INNER JOIN PTable2 t2 
                                ON p.RealAccountNo = t2.AccountNo
                                AND p.PaymentDateValue = t2.First_Payment_Date
                        ),

                        -- ============================================================
                        -- 7. Combine with Devices + cleaned payment data
                        -- ============================================================
                        STable AS (
                            SELECT 
                                d.Id,
                                d.Status,
                                d.Model,
                                d.Make,
                                d.Locked,
                                d.LockType,
                                p3.Last_Payment_Date,
                                p5.First_Paid_Amount,
                                d.FirstLockDateIsoFormat AS First_Lock_Date,
                                d.NextLockDateIsoFormat AS Next_Lock_Date,
                                d.LastConnectedAt,
                                p1.Total_Paid,
                                p2.First_Payment_Date,
                                p5.FirstPaidDate,
                                p5.First_MPesaCode,
                                p4.Last_Paid_Amount,
                                p4.LastPaidDate,
                                p4.Last_MPesaCode,
                                l24.Total_Last24Hours,
                                d.ImeiNo,
                                d.LockGroup
                            FROM Devices d
                            LEFT JOIN PTable1 p1 ON d.Id = p1.AccountNo
                            LEFT JOIN PTable2 p2 ON d.Id = p2.AccountNo
                            LEFT JOIN PTable3 p3 ON d.Id = p3.AccountNo
                            LEFT JOIN PTable4 p4 ON d.Id = p4.AccountNo
                            LEFT JOIN PTable5 p5 ON d.Id = p5.AccountNo
                            LEFT JOIN Last24 l24 ON d.Id = l24.AccountNo
                            WHERE d.Status = 'enrolled'
                        ),

                        -- Only those with valid first payment
                        FilteredSTable AS (
                            SELECT *
                            FROM STable
                            WHERE First_Paid_Amount IS NOT NULL
                        ),

                        -- ============================================================
                        -- 8. Add Contract_Info details
                        -- ============================================================
                        Computed AS (
                            SELECT 
                                s.*,
                                ci.Term_in_Months AS TermsInMonths,
                                ci.Total_Cost AS dealer_payment,
                                ci.Deposit,
                                ci.Daily AS Daily,
                                ci.Weekly AS Weekly,
                                ci.Monthly AS Monthly,
                                ci.First_Name AS FirstName
                            FROM FilteredSTable s
                            INNER JOIN Contract_Info ci ON s.Id = ci.ID
                            AND ci.EndDate IS NULL
                        )

                        -- ============================================================
                        -- 11. FINAL RESULT
                        -- ============================================================
                        SELECT 
                            Id As AccountId,
                            LastPaidDate As LastPaymentDate,
                            FirstPaidDate AS FirstPaymentDate,
                            First_Paid_Amount AS FirstAmount,
                            Last_Paid_Amount AS LastPaidAmount,
                            Daily,
                            Weekly,
                            Monthly,
                            Next_Lock_Date AS NextLockDate,
                            Deposit,
                            Total_Paid AS TotalPaid,
                            Total_Last24Hours AS PaidLast24Hours,    -- <--- new column
                            FirstName,
                            TermsInMonths,
                            LastConnectedAt,
                            LockGroup
                        FROM Computed
                        ORDER BY LastPaidDate DESC;";

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

        public async Task<PaymentsViewModel> GetPaymentSummaryAsync(int? accountId, int deviceGroupId = 0, int page = 1, int pageSize = 10, string searchTerm = "")
        {
            var offset = (page - 1) * pageSize;

            var countQuery = SetPaymentSummaryQuery();
            var totalRecords = await _db.QuerySingleAsync<int>(countQuery, new { DealerId = deviceGroupId, searchParam = searchTerm, AccountId = accountId });

            var sql = @";WITH ValidPayments AS
(
    SELECT
        COALESCE(op.AccountNoBigint, kp.AccountNoBigint) AS AccountNo,
        kp.MpesaCode,
        kp.AmountValue,
        kp.PaymentDateValue
    FROM KosePayments kp
    LEFT JOIN OrphanedPayments op 
        ON op.MpesaCode = kp.MpesaCode
),

PaymentStats AS
(
    SELECT
        AccountNo,

        SUM(AmountValue) AS TotalPaid,

        SUM(CASE 
            WHEN PaymentDateValue >= DATEADD(HOUR,-24,GETDATE()) 
            THEN AmountValue END) AS Last24hrPaidAmount,

        SUM(CASE 
            WHEN PaymentDateValue >= DATEADD(DAY,-7,GETDATE()) 
            THEN AmountValue END) AS LastWeekPaidAmount,

        MIN(PaymentDateValue) AS FirstPaidDate,
        MAX(PaymentDateValue) AS LastPaidDate
    FROM ValidPayments
    GROUP BY AccountNo
),

FirstPayment AS
(
    SELECT *
    FROM
    (
        SELECT
            AccountNo,
            AmountValue,
            MpesaCode,
            PaymentDateValue,
            ROW_NUMBER() OVER
            (
                PARTITION BY AccountNo
                ORDER BY PaymentDateValue ASC
            ) rn
        FROM ValidPayments
    ) t
    WHERE rn = 1
),

LastPayment AS
(
    SELECT *
    FROM
    (
        SELECT
            AccountNo,
            AmountValue,
            MpesaCode,
            PaymentDateValue,
            ROW_NUMBER() OVER
            (
                PARTITION BY AccountNo
                ORDER BY PaymentDateValue DESC
            ) rn
        FROM ValidPayments
    ) t
    WHERE rn = 1
)

SELECT
    d.Id AS AccountNo,
    ci.TotalAmount,
    ps.TotalPaid,

    fp.PaymentDateValue AS FirstPaidDate,
    fp.AmountValue AS FirstPaymentAmount,
    fp.MpesaCode AS FirstMPesaCode,

    lp.PaymentDateValue AS LastPaidDate,
    lp.AmountValue AS LastPaymentAmount,
    lp.MpesaCode AS LastMPesaCode,

    ps.Last24hrPaidAmount,
    ps.LastWeekPaidAmount,

    ci.First_Name AS CustomerName,
    ci.Daily,
    ci.Deposit,
    ci.Weekly,
    ci.Monthly,
    ci.Term_in_Months AS TermsInMonths,

    d.Make,
    d.Model,
    d.LastConnectedAt,
    d.Locked,
    d.EnrolledOn,
    d.DeviceGroupId,
    d.Name,
    d.ImeiNo,
    d.Status,
    d.LockType,
    d.NextLockDateIsoFormat,
    d.NextLockDate,
    ci.DebtCollectorUserId,
    d.LockGroup

FROM Devices d
JOIN PaymentStats ps ON d.Id = ps.AccountNo
JOIN FirstPayment fp ON d.Id = fp.AccountNo
JOIN LastPayment lp ON d.Id = lp.AccountNo
JOIN Contract_Info ci 
    ON ci.ID = d.Id
    AND ci.EndDate IS NULL

WHERE d.Status = 'enrolled'
AND (@DealerId = 0 OR d.DeviceGroupId = @DealerId)
AND (@AccountId IS NULL OR d.Id = @AccountId)
AND (
    @searchParam IS NULL
    OR CAST(d.Id AS NVARCHAR(50)) LIKE '%' + @searchParam + '%'
    OR fp.MpesaCode LIKE '%' + @searchParam + '%'
    OR ci.First_Name LIKE '%' + @searchParam + '%'
)

ORDER BY lp.PaymentDateValue DESC
                        	OFFSET @offset ROWS 
                        	FETCH NEXT @pageSize ROWS ONLY;";

            var searchParam = string.IsNullOrWhiteSpace(searchTerm) ? null : searchTerm;
            var payments = await _db.QueryAsync<PaymentSummary>(sql, new { DealerId = deviceGroupId, offset, pageSize, searchParam, AccountId = accountId });

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
            return @"SELECT COUNT(*)
FROM
(
    SELECT kp.AccountNoBigint
    FROM KosePayments kp

    JOIN Devices d 
        ON d.Id = kp.AccountNoBigint

    JOIN Contract_Info ci 
        ON ci.ID = d.Id
        AND ci.EndDate IS NULL

    WHERE d.Status = 'enrolled'

    AND (@DealerId = 0 OR d.DeviceGroupId = @DealerId)

    AND (@AccountId IS NULL OR kp.AccountNoBigint = @AccountId)

    AND (
        @searchParam IS NULL
        OR ci.First_Name LIKE '%' + @searchParam + '%'
        OR kp.MpesaCode LIKE '%' + @searchParam + '%'
        OR (
            TRY_CAST(@searchParam AS bigint) IS NOT NULL
            AND kp.AccountNoBigint = TRY_CAST(@searchParam AS BIGINT)
        )
    )

    GROUP BY kp.AccountNoBigint
) t;";
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

        public async Task<Contact?> GetNextOfKinForOrder(long orderId, bool isPrimary)
        {
            var sql = @" SELECT *
                        FROM [dbo].[Woo_Orders_NextOfKin]
                          WHERE OrderId = @OrderId
                          AND [IsPrimary] = @Primary";

            var record = await _db.QueryFirstOrDefaultAsync<Contact>(sql, new { OrderId = orderId, Primary = isPrimary });

            return record;
        }

        public async Task<KosePaymentsViewModel> GetPaymentsForAccount(string? customerAccount, int page = 1, int pageSize = 10)
        {
            var offset = (page - 1) * pageSize;

            var sql = @"SELECT kp.Id,
                            TRY_CAST(LTRIM(RTRIM(COALESCE(op.AccountNoBigint, kp.AccountNoBigint))) AS BIGINT) AS AccountNo,
                            kp.MpesaCode,
                            kp.Amount,
                            kp.PaymentDate,
                            kp.Amount AS AmountValue,
                            kp.PaymentDateValue
                FROM [dbo].[KosePayments] kp
                LEFT JOIN OrphanedPayments op ON op.MpesaCode = kp.MpesaCode
                WHERE COALESCE(op.AccountNoBigint, kp.AccountNoBigint) = @AccountNo
                ORDER BY [PaymentDateValue] DESC
                OFFSET @Offset ROWS 
                FETCH NEXT @pageSize ROWS ONLY";
            var payments = await _db.QueryAsync<KosePayments>(sql, new { AccountNo = customerAccount, offset, pageSize });

            var countSql = @"SELECT COUNT(*)
                FROM [dbo].[KosePayments] 
                WHERE [AccountNoBigint] = @AccountNo";

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

            var countSql = @"SELECT 
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
							ON d.Id = kp.AccountNoBigint
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
							ON d.Id = kp.AccountNoBigint
							WHERE D.DeviceGroupId = @DealerId) AS TotalTransactions
                FROM Woo_Orders wo
				INNER JOIN KosePayments kp
				ON wo.MpesaDepositRef = KP.MpesaCode
				INNER JOIN Devices d
				ON d.Id = kp.AccountNoBigint
				where d.DeviceGroupId = @DealerId
                ";
        }

        public async Task<List<TransactionHistory>> GetTransactionHistory(int dealerId = 0)
        {
            var sql = @"SELECT TOP(4) kp.AccountNoBigint AccountNo
                                ,[Status]
                                ,[DateCreated]
                                ,WO.[FirstName]
                                ,[LastName]
                                ,[DealerRef]
                                ,[MpesaDepositRef]
                                ,SUM(AmountValue) Amount
                            FROM [dbo].[Woo_Orders] WO
                            LEFT JOIN KosePayments KP
                            ON WO.MpesaDepositRef = KP.MpesaCode
	                        WHERE KP.[AccountNoBigint] IS NOT NULL
	                        AND [Status] IN ('approved', 'approval-waiting')
	                        GROUP BY KP.[AccountNoBigint]
	                            ,[Status]
                                ,[DateCreated]
                                ,WO.[FirstName]
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
                                ,WO.[FirstName]
                                ,[LastName]
                                ,[DealerRef]
                                ,[MpesaDepositRef]
                                ,SUM(AmountValue) Amount
                            FROM [dbo].[Woo_Orders] WO
                            LEFT JOIN KosePayments KP
                            ON WO.MpesaDepositRef = KP.MpesaCode
							INNER JOIN Devices d on kp.AccountNoBigint = d.Id
    						INNER JOIN Dealers dl on dl.DealerReference = d.DeviceGroupId
    						WHERE dl.DealerId = @DealerId
    						AND d.[Status] = 'enrolled'
	                        AND KP.[AccountNo] IS NOT NULL
	                        AND WO.[Status] IN ('approved', 'approval-waiting')
	                        GROUP BY KP.[AccountNo]
	                            ,WO.[Status]
                                ,[DateCreated]
                                ,WO.[FirstName]
                                ,[LastName]
                                ,[DealerRef]
                                ,[MpesaDepositRef]
	                        ORDER BY [DateCreated] desc";
        }
        #endregion

        #region Restructured
        public async Task InsertRestructured(RestructuredRecord restructuringRecord)
        {
            var sql = @"
                    SELECT CASE WHEN EXISTS (
                        SELECT 1 FROM [RestructuredRecords]
                        WHERE [AccountNo] = @AccountNo
                    ) THEN 1 ELSE 0 END;
                ";

            bool exists = await _db.ExecuteScalarAsync<bool>(sql, new
            {
                AccountNo = restructuringRecord.AccountNo.ToString()
            });

            if (exists)
            {
                var update = @"UPDATE [dbo].[RestructuredRecords]
                              SET [Date_Agreed] = @DateAgreed
                                 ,[Amount_Res] = @AmountRes
                            WHERE [AccountNo] = @AccountNo";
                await _db.ExecuteAsync(update, new { AccountNo = restructuringRecord.AccountNo
                                                    ,AmountRes  = restructuringRecord.AmountRes
                                                    ,DateAgreed = restructuringRecord.DateAgreed
                });

                return;
            }

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

        public async Task<RestructuredViewModel> GetAllRestructured(string searchTerm, int page = 1, int pageSize = 10)
        {

            var offset = (page - 1) * pageSize;
            var countSql = @"SELECT 
                            COUNT(*)
                        FROM RestructuredRecords wo
                        WHERE (
                        @SearchTerm IS NULL
                        OR WO.AccountNo LIKE '%' + @SearchTerm + '%'
                    )";
            var searchParam = string.IsNullOrWhiteSpace(searchTerm) ? null : searchTerm;
            var totalRecords = await _db.QuerySingleAsync<int>(countSql, new { SearchTerm = searchParam });

            var sqlSelectAll = @"
                SELECT 
                    r.AccountNo,
                    r.ID,
                    r.Date_Agreed AS DateAgreed,
                    r.Amount_Res AS AmountRes,
                    SUM(vp.AmountValue) AS TotalPaidR,
                    MIN(vp.PaymentDateValue) AS FirstResPaymentDate,
                    MAX(vp.PaymentDateValue) AS LastResPaymentDate,
                    ci.Daily,
                    ci.Weekly,
                    ci.Monthly,
                    ci.First_Name AS FirstName
                FROM RestructuredRecords r
                -- ✅ Merge KosePayments + OrphanedPayments properly
                LEFT JOIN (
                    SELECT 
                        COALESCE(op.AccountNoBigint, kp.AccountNoBigint) AS AccountNo,
                        kp.MpesaCode,
                        kp.AmountValue,
                        kp.PaymentDateValue
                    FROM KosePayments kp
                    LEFT JOIN OrphanedPayments op 
                        ON kp.MpesaCode = op.MpesaCode
                ) vp 
                    ON vp.AccountNo = r.AccountNo
                    AND vp.PaymentDateValue > r.Date_Agreed
                INNER JOIN Contract_Info ci 
                    ON ci.ID = r.AccountNo
                    AND ci.EndDate IS NULL
                WHERE 
                    @SearchTerm IS NULL
                    OR r.AccountNo LIKE '%' + @SearchTerm + '%'
                GROUP BY 
                    r.AccountNo,
                    r.ID,
                    r.Date_Agreed,
                    r.Amount_Res,
                    ci.Daily,
                    ci.Weekly,
                    ci.Monthly,
                    ci.First_Name
                ORDER BY 
                    r.Date_Agreed DESC
                OFFSET @Offset ROWS 
                FETCH NEXT @pageSize ROWS ONLY;";

            var records = await _db.QueryAsync<RestructuredRecord>(sqlSelectAll, new { SearchTerm = searchParam, offset, pageSize });

            return new RestructuredViewModel()
            {
                Records = records.ToList(),
                CurrentPage = page,
                TotalPages = (int)Math.Ceiling((double)totalRecords / pageSize),
                TotalRecords = totalRecords,
                SearchTerm = searchTerm
            };
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
                WHERE Amount_Res = @AccountId
                ORDER BY Date_Agreed DESC;";

            var records = await _db.QueryAsync<RestructuredRecord>(sqlSelectAll, new {AccountId = accountId});
            return records.ToList();
        }

        public async Task<decimal> GetPaymentTotalAfterDate(DateTime agreedDate, long accountId)
        {
            var sql = @"SELECT 
                            SUM(AmountValue) AS TotalPayments
                        FROM 
                            dbo.KosePayments
                        WHERE 
                            PaymentDateValue > @GivenDate
                        AND [AccountNo] = @AccountNo"
            ;

            var amount = await _db.QueryFirstOrDefaultAsync<decimal?>(sql, new { AccountNo = accountId.ToString(), GivenDate = agreedDate });

            if (amount == null)
                return 0;

            return amount.Value;
        }

        public async Task<(decimal?, DateTime)> GetPaymentTotalAfterDateAndFirstPaymentDate(DateTime agreedDate, long accountId)
        {
            var sql = @"SELECT 
                            SUM(AmountValue) AS TotalPayments,
                            MIN(PaymentDateValue) AS PaymentDateValue                            
                        FROM 
                            dbo.KosePayments
                        WHERE 
                            PaymentDateValue > @GivenDate
                        AND [AccountNo] = @AccountNo"
            ;

            var result = await _db.QueryFirstOrDefaultAsync<(decimal?, DateTime)>(sql, new { AccountNo = accountId.ToString(), GivenDate = agreedDate });

            if (result.Item1 == null)
                result.Item1 = 0;

            return result;
        }

        public async Task<KosePaymentsViewModel> GetAssignedPaymentsAsync(string searchTerm, int page, int pageSize)
        {
            var offset = (page - 1) * pageSize;

            var countSql = @"SELECT Count(*)
                                FROM [dbo].[OrphanedPayments] op
                                INNER JOIN Devices d 
                                ON d.Id = op.AccountNoBigint
                                  WHERE (
                                @SearchTerm IS NULL
                                OR MpesaCode LIKE '%' + @SearchTerm + '%'
                                OR AccountNo LIKE '%' + @SearchTerm + '%'
                                OR OrphanedAccountNo LIKE '%' + @SearchTerm + '%')";

            var sql = @" SELECT [OrphanedAccountNo] AS OrphanedAccountNo
                            ,[MpesaCode] AS MpesaCode
                            ,[AccountNo] AS AccountNo
                            ,[DateCreated] AS PaymentDateValue
                        FROM [dbo].[OrphanedPayments] op
                        INNER JOIN Devices d 
                        ON d.Id = op.AccountNoBigint
                          WHERE (
                        @SearchTerm IS NULL
                        OR MpesaCode LIKE '%' + @SearchTerm + '%'
                        OR AccountNo LIKE '%' + @SearchTerm + '%'
                        OR OrphanedAccountNo LIKE '%' + @SearchTerm + '%')
                        ORDER BY [DateCreated] DESC
                        OFFSET @Offset ROWS 
                        FETCH NEXT @pageSize ROWS ONLY";

            var payments = await _db.QueryAsync<KosePayments>(sql, new { offset, pageSize, searchTerm });
            var totalRecords = await _db.QuerySingleAsync<int>(countSql, new { searchTerm });

            return new KosePaymentsViewModel()
            {
                CurrentPage = page,
                Payments = payments.ToList(),
                TotalPages = (int)Math.Ceiling((double)totalRecords / pageSize)
            };
        }

        public async Task CreateAssignedPaymentsAsync(string orphanedNo, string mpesaCode, string accountNo)
        {

            if(accountNo == "0")
            {
                return;
            }

            var existing = @"SELECT * FROM OrphanedPayments
                            WHERE MpesaCode = @MpesaCode"
            ;
            var existingPayments = await _db.QueryFirstOrDefaultAsync<KosePayments>(existing, new { MpesaCode = mpesaCode });

            if (existingPayments != null)
            {
                return;
            }

            var insertSql = @"INSERT INTO [dbo].[OrphanedPayments]
                                           ([Id]
                                           ,[OrphanedAccountNo]
                                           ,[MpesaCode]
                                           ,[AccountNo]
                                           ,[DateCreated])
                                     VALUES
                                           (@Id
                                           ,@OrphanedAccountNo
                                           ,@MpesaCode
                                           ,@AccountNo
                                           ,GETDATE())";


            await _db.ExecuteScalarAsync<int>(insertSql, new { Id = Guid.NewGuid(), OrphanedAccountNo = orphanedNo, MpesaCode = mpesaCode, AccountNo = accountNo });
        }

        public async Task<List<RestructuredRecord>> GetAllRestructuredFlat()
        {
            var sqlSelectAll = @"
                SELECT 
                    Id,
                    [AccountNo],
                    Date_Agreed AS DateAgreed,
                    Amount_Res AS AmountRes,
                    Days_Restructured AS DaysRestructured,
                    Total_Due_R AS TotalDueR,
                    Total_Paid_R AS TotalPaidR,
                    Arrears_R AS ArrearsR,
                    Auto_lock_date_pmt_R AS AutoLockDatePmtR
                FROM RestructuredRecords
                ORDER BY Date_Agreed DESC;";

            var records = await _db.QueryAsync<RestructuredRecord>(sqlSelectAll);
            return records.ToList();
        }

        public async Task<List<AccountSummary>> GetPaymentSummariesForAccounts(List<long> accountIds)
        {
            var sql = @"WITH Payments AS (
                            SELECT
                                kp.MpesaCode,
                                TRY_CAST(kp.Amount AS DECIMAL(18,2)) AS Amount,
                                kp.AmountValue,
                                kp.PaymentDateValue,
                                kp.AccountNoBigint
                            FROM KosePayments kp
                            WHERE kp.AccountNoBigint IN @AccountIds

                            UNION ALL

                            SELECT
                                kp.MpesaCode,
                                TRY_CAST(kp.Amount AS DECIMAL(18,2)) AS Amount,
                                kp.AmountValue,
                                kp.PaymentDateValue,
                                op.AccountNoBigint
                            FROM KosePayments kp
                            INNER JOIN OrphanedPayments op 
                                ON op.MpesaCode = kp.MpesaCode
                            WHERE op.AccountNoBigint IN @AccountIds
                        ),

                        AggregatedPayments AS (
                            SELECT
                                AccountNoBigint AS AccountNo,

                                SUM(Amount) AS Total_Paid,

                                MIN(PaymentDateValue) AS First_Payment_Date,

                                MAX(PaymentDateValue) AS Last_Payment_Date,

                                SUM(
                                    CASE 
                                        WHEN PaymentDateValue >= DATEADD(HOUR,-24,GETDATE())
                                        THEN Amount
                                        ELSE 0
                                    END
                                ) AS Total_Last24Hours

                            FROM Payments
                            GROUP BY AccountNoBigint
                        ),

                        LastPayment AS (
                            SELECT p.*
                            FROM Payments p
                            INNER JOIN AggregatedPayments a
                                ON p.AccountNoBigint = a.AccountNo
                               AND p.PaymentDateValue = a.Last_Payment_Date
                        ),

                        FirstPayment AS (
                            SELECT p.*
                            FROM Payments p
                            INNER JOIN AggregatedPayments a
                                ON p.AccountNoBigint = a.AccountNo
                               AND p.PaymentDateValue = a.First_Payment_Date
                        )

                        SELECT 
                            d.Id AS AccountId,

                            lp.PaymentDateValue AS LastPaymentDate,

                            fp.PaymentDateValue AS FirstPaymentDate,

                            fp.Amount AS FirstAmount,

                            lp.AmountValue AS LastPaidAmount,

                            ci.Daily,
                            ci.Weekly,
                            ci.Monthly,

                            d.NextLockDateIsoFormat AS NextLockDate,

                            ci.Deposit,

                            ap.Total_Paid AS TotalPaid,

                            ap.Total_Last24Hours AS PaidLast24Hours,

                            ci.First_Name AS FirstName,

                            ci.Term_in_Months AS TermsInMonths,

                            d.LastConnectedAt,

                            d.LockGroup,
                            d.ImeiNo

                        FROM Devices d

                        INNER JOIN AggregatedPayments ap
                            ON d.Id = ap.AccountNo

                        LEFT JOIN LastPayment lp
                            ON d.Id = lp.AccountNoBigint

                        LEFT JOIN FirstPayment fp
                            ON d.Id = fp.AccountNoBigint

                        INNER JOIN Contract_Info ci
                            ON d.Id = ci.ID
                           AND ci.EndDate IS NULL

                        WHERE d.Status = 'enrolled'

                        ORDER BY lp.PaymentDateValue DESC";

            var summaries =
                await _db.QueryAsync<AccountSummary>(
                sql,
                new { AccountIds = accountIds },
                commandTimeout: 180); // 3 minutes

            return summaries.ToList();
        }

        public async Task<KosePaymentsViewModel> GetAllPaymentAccountsByUserIdAsync(int userId, string searchTerm, int page, int pageSize)
        {
            var offset = (page - 1) * pageSize;

            var countsql = @" SELECT COUNT(*) 
                            FROM [dbo].[KosePayments] kp
                            INNER JOIN Devices d on kp.AccountNoBigint = d.Id
                            INNER JOIN Contract_Info ci on d.Id = ci.ID
                            WHERE ci.AssignedAgentId = @userId
                            AND d.[Status] = 'enrolled'
                            AND (
                            @SearchTerm IS NULL
                            OR AccountNo LIKE '%' + @SearchTerm + '%'
                            OR AmountValue LIKE '%' + @SearchTerm + '%'
                            OR PaymentDateValue LIKE '%' + @SearchTerm + '%'
                            OR MpesaCode LIKE '%' + @SearchTerm + '%')";

            var searchParam = string.IsNullOrWhiteSpace(searchTerm) ? null : searchTerm;
            var totalRecords = await _db.QuerySingleAsync<int>(countsql, new { userId, SearchTerm = searchParam });

            var sql = @"SELECT kp.[Id]
                                ,[AccountNo]
                                ,[MpesaCode]
                                ,[Amount]
                                ,[PaymentDate]
                                ,[AmountValue]
                                ,[PaymentDateValue]
                                ,kp.[Created]
                        FROM [dbo].[KosePayments] kp
                        INNER JOIN Devices d on kp.AccountNoBigint = d.Id
                        INNER JOIN Contract_Info ci on d.Id = ci.ID
                        WHERE ci.AssignedAgentId = @userId
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

            var payments = await _db.QueryAsync<KosePayments>(sql, new { userId, offset, pageSize, SearchTerm = searchParam });

            return new KosePaymentsViewModel()
            {
                CurrentPage = page,
                Payments = payments.ToList(),
                TotalPages = (int)Math.Ceiling((double)totalRecords / pageSize)
            };
    }

    #endregion

}
}
