using Ranalo.Models;
using System.Data;
using Dapper;
using System.Drawing.Printing;

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

        public async Task<IEnumerable<KosePayments>> GetOrphanedPaymentsAsync()
        {
            var sql = @" SELECT kp.MpesaCode, kp.AccountNo, kp.AmountValue, kp.PaymentDateValue 
                        FROM KosePayments kp
                        LEFT JOIN Devices D ON D.Id = TRY_CAST(kp.AccountNo AS BIGINT)
                        WHERE D.Id is null
                        ORDER BY kp.PaymentDateValue desc";
            return await _db.QueryAsync<KosePayments>(sql);
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
                FROM [dbo].[Devices] ";
            return await _db.QueryAsync<Device>(sql);
        }

        public async  Task<CustomerDetails?> GetCustomerDetails(long orderId)
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
                          where wo.OrderID = @OrderId";

            return await _db.QueryFirstOrDefaultAsync<CustomerDetails>(sql, new { OrderId = orderId});
        }

        public async Task<IEnumerable<ImagesMetadata>> GetIdentityImagesForOrder(long orderId)
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
                          ON wo.OrderID = woi.OrderId
                          where wo.OrderID = @OrderId";

            return await _db.QueryAsync<ImagesMetadata>(sql, new { OrderId = orderId });
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
    }
}
