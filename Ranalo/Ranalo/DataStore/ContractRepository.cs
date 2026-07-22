using Dapper;
using DocumentFormat.OpenXml.Drawing;
using Ranalo.Calculator.Logic.Models;
using Ranalo.Models;
using Ranalo.Woocommece.Api.Models;
using System.Data;
using System.Diagnostics.Contracts;

namespace Ranalo.DataStore
{
    public class ContractRepository : IContractRepository
    {
        private readonly IDbConnection _db;

        public ContractRepository(IDbConnection db)
        {
            _db = db;
        }

        // Create
        public async Task<int> AddContractAsync(ContractInfo contract)
        {
            var sql = @"
            INSERT INTO Contract_Info
            (ContractID, ID, Deposit, Daily, Weekly, Monthly, 
             rePayment_Intervals, Term_in_Months, Total_Loan, Total_Cost, First_Name)
            VALUES
            (@ContractID, @ID, @Deposit, @Daily, @Weekly, @Monthly, 
             @RePaymentIntervals, @TermInMonths, @TotalLoan, @TotalCost, @FirstName);
            SELECT CAST(SCOPE_IDENTITY() as int);";

            return await _db.ExecuteScalarAsync<int>(sql, contract);
        }

        // Read (single)
        public async Task<ContractInfo?> GetContractByIdAsync(int contractId)
        {
            var sql = @"SELECT * FROM Contract_Info 
                        WHERE ContractID = @contractId
                        AND EndDate IS NULL";
            return await _db.QueryFirstOrDefaultAsync<ContractInfo>(sql, new { contractId });
        }

        public async Task<ContractInfo?> GetContractByDeviceIdAsync(int deviceId)
        {
            var sql = @"SELECT * FROM Contract_Info 
                        WHERE ID = @DeviceId
                        AND EndDate IS NULL";
            return await _db.QueryFirstOrDefaultAsync<ContractInfo>(sql, new { DeviceId = deviceId });
        }

        // Read (all)
        public async Task<ContractViewModel> GetAllContractsAsync(int page, int pageSize, string searchParam = "")
        {
            var offset = (page - 1) * pageSize;

            var countSql = @"SELECT COUNT(*) FROM Contract_Info  
                            WHERE (
                            @SearchTerm IS NULL
                            OR First_Name LIKE '%' + @SearchTerm + '%'
                            OR ID LIKE '%' + @SearchTerm + '%'
                            )
                            AND EndDate IS NULL";

            var totalRecords = await _db.QuerySingleAsync<int>(countSql, new { SearchTerm = searchParam });

            var sql = @"SELECT [ContractID]
                          ,[ID]
                          ,[Deposit]
                          ,[Daily]
                          ,[Weekly]
                          ,[Monthly]
                          ,[rePayment_Intervals] as RePaymentIntervals
                          ,[Term_in_Months] as TermInMonths
                          ,[Total_Loan] as TotalLoan
                          ,[Total_Cost] as TotalCost
                          ,[First_Name] as FirstName
                          ,TotalAmount
                          ,[BuyingPrice]
                        FROM Contract_Info
                         WHERE (
                            @SearchTerm IS NULL
                            OR First_Name LIKE '%' + @SearchTerm + '%'
                            OR ID LIKE '%' + @SearchTerm + '%'
                        )
                        AND EndDate IS NULL
                        ORDER BY [ContractID] DESC
                        OFFSET @Offset ROWS 
                        FETCH NEXT @pageSize ROWS ONLY";

            var contracts =  await _db.QueryAsync<ContractInfo>(sql, new { Offset = offset, pageSize = pageSize, SearchTerm = searchParam });

            var result = new ContractViewModel()
            {
                Contracts = contracts.ToList(),
                CurrentPage = page,
                PageSize = pageSize,
                SearchTerm = searchParam,
                TotalRecords = totalRecords,
                TotalPages = totalRecords / pageSize,
            };

            return result;
        }

        // Update
        public async Task<int> UpdateContractAsync(ContractInfo contract)
        {
            var sql = @"
            UPDATE Contract_Info
            SET Deposit = @Deposit,
                Daily = @Daily,
                Weekly = @Weekly,
                Monthly = @Monthly,
                rePayment_Intervals = @RePaymentIntervals,
                Total_Loan = @TotalLoan,
                Total_Cost = @TotalCost,
                First_Name = @FirstName,
                [Term_in_Months] = @TermInMonths,
                [BuyingPrice] = @BuyingPrice
            WHERE ID = @ID";

            return await _db.ExecuteAsync(sql, contract);
        }

        // Delete
        public async Task<int> DeleteContractAsync(int contractId)
        {
            var sql = "DELETE FROM Contract_Info WHERE ContractID = @contractId";
            return await _db.ExecuteAsync(sql, new { contractId });
        }

        public async Task<int> CreateRecoveredAccount(ContractInfo newContract)
        {
            if (_db.State != ConnectionState.Open)
                _db.Open();

            using (var transaction = _db.BeginTransaction())
            {
                try
                {
                    // Lets Update Payments first
                    var updatePaymentsQuery = @"
                        UPDATE KosePayments
                        SET [AccountNo] = @AccountNoNew
                        WHERE [AccountNo] = @AccountNo;
                    ";

                    await _db.ExecuteAsync(updatePaymentsQuery, new
                    {
                        AccountNo = newContract.ID,
                        AccountNoNew = $"{newContract.ID}_R"
                    }, transaction);

                    // Now Lets Update Orphaned Payments
                    var updateOrphanedPaymentsQuery = @"
                        UPDATE OrphanedPayments
                        SET [AccountNo] = @AccountNoNew
                        WHERE [AccountNo] = @AccountNo;
                    ";

                    await _db.ExecuteAsync(updateOrphanedPaymentsQuery, new
                    {
                        AccountNo = newContract.ID,
                        AccountNoNew = $"{newContract.ID}_R"
                    }, transaction);

                    // Then removeany manual restructures
                    var deleteRestructuredQuery = @"
                        DELETE FROM [RestructuredRecords]
                        WHERE [AccountNo] = @AccountNo;
                    ";

                    await _db.ExecuteAsync(deleteRestructuredQuery, new
                    {
                        AccountNo = newContract.ID
                    }, transaction);

                    // Before we create a new contract Lets End current one
                    var updateContractInfoQuery = @"
                        UPDATE [Contract_Info]
                        SET [EndDate] = GETDATE()
                        WHERE [AccountNo] = @AccountNo;
                    ";

                    await _db.ExecuteAsync(updateContractInfoQuery, new
                    {
                        AccountNo = newContract.ID,
                    }, transaction);

                    // Finally lets create a contract
                    var insertQuery = @"
                        INSERT INTO Contract_Info
                        (ContractID, ID, Deposit, Daily, Weekly, Monthly, 
                         rePayment_Intervals, Term_in_Months, Total_Loan, Total_Cost, First_Name)
                        VALUES
                        (@ContractID, @ID, @Deposit, @Daily, @Weekly, @Monthly, 
                         @RePaymentIntervals, @TermInMonths, @TotalLoan, @TotalCost, @FirstName);
                        SELECT CAST(SCOPE_IDENTITY() as int);"
                    ;

                    int newHistoryId = await _db.QuerySingleAsync<int>(insertQuery, 
                        newContract, transaction);
                    transaction.Commit();
                    return newHistoryId;
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }


        public async Task AssignContractToCollector(int contractId, int collectorUserId)
        {
            const string sql = @"
                UPDATE Contract_Info
                SET DebtCollectorUserId = @CollectorUserId
                WHERE ID = @ContractId;
            ";

            await _db.ExecuteAsync(sql, new
            {
                ContractId = contractId,
                CollectorUserId = collectorUserId
            });
        }

        public async Task<PaymentsViewModel> GetCollectorsContractSummaryAsync(int userId, int? accountId, int deviceGroupId = 0, int page = 1, int pageSize = 10, string searchTerm = "")
        {
            var offset = (page - 1) * pageSize;

            var countQuery = SetPaymentSummaryQuery();
            var totalRecords = await _db.QuerySingleAsync<int>(countQuery, new { UserId = userId, DealerId = deviceGroupId, searchParam = searchTerm, AccountId = accountId });

                                                    var sql = @"IF OBJECT_ID('tempdb..#ValidPayments') IS NOT NULL DROP TABLE #ValidPayments;
                                            SELECT 
                                                kp.Id,
                                                COALESCE(op.AccountNoBigint, kp.AccountNoBigint) AS AccountNo,
                                                kp.MpesaCode,
                                                kp.Amount,
                                                kp.AmountValue,
                                                kp.PaymentDateValue AS PaymentDateValue
                                            INTO #ValidPayments
                                            FROM KosePayments kp
                                            LEFT JOIN OrphanedPayments op ON op.MpesaCode = kp.MpesaCode;
                        
                                            -- Index for performance
                                            CREATE INDEX IX_ValidPayments_AccountNo ON #ValidPayments(AccountNo);
                                            CREATE INDEX IX_ValidPayments_PaymentDate ON #ValidPayments(AccountNo, PaymentDateValue);
                        
                                            ---------------------------------------------------
                                            -- STEP 2: Compute totals and first/last payments
                                            ---------------------------------------------------
                        
	                                        -- 🕒 Payments in last 24 hours
	                                        IF OBJECT_ID('tempdb..#PTable24hrs') IS NOT NULL DROP TABLE #PTable24hrs;

	                                        SELECT 
		                                        AccountNo,
		                                        SUM(AmountValue) AS Last24hrPaidAmount
	                                        INTO #PTable24hrs
	                                        FROM #ValidPayments
	                                        WHERE PaymentDateValue >= DATEADD(HOUR, -24, GETDATE())
	                                        GROUP BY AccountNo;

	                                        CREATE INDEX IX_PTable24hrs_AccountNo ON #PTable24hrs(AccountNo);

                                            -- 🧮 Total paid per account
                                            IF OBJECT_ID('tempdb..#PTable1') IS NOT NULL DROP TABLE #PTable1;
                                            SELECT 
                                                AccountNo,
                                                SUM(AmountValue) AS Total_Paid
                                            INTO #PTable1
                                            FROM #ValidPayments
                                            GROUP BY AccountNo;
                        
                                            CREATE INDEX IX_PTable1_AccountNo ON #PTable1(AccountNo);
                        
                                            -- 💰 Last payment details
                                            IF OBJECT_ID('tempdb..#PTable4') IS NOT NULL DROP TABLE #PTable4;
                                            SELECT 
                                                v.AccountNo,
                                                v.AmountValue AS Last_Paid_Amount,
                                                v.PaymentDateValue AS LastPaidDate,
                                                v.MpesaCode AS Last_MPesaCode
                                            INTO #PTable4
                                            FROM #ValidPayments v
                                            INNER JOIN (
                                                SELECT AccountNo, MAX(PaymentDateValue) AS Last_Payment_Date
                                                FROM #ValidPayments
                                                GROUP BY AccountNo
                                            ) t3 ON v.AccountNo = t3.AccountNo AND v.PaymentDateValue = t3.Last_Payment_Date;
                        
                                            CREATE INDEX IX_PTable4_AccountNo ON #PTable4(AccountNo);
                        
                                            -- 🪙 First payment details
                                            IF OBJECT_ID('tempdb..#PTable5') IS NOT NULL DROP TABLE #PTable5;
                                            SELECT 
                                                v.AccountNo,
                                                v.AmountValue AS First_Paid_Amount,
                                                v.PaymentDateValue AS FirstPaidDate,
                                                v.MpesaCode AS First_MPesaCode
                                            INTO #PTable5
                                            FROM #ValidPayments v
                                            INNER JOIN (
                                                SELECT AccountNo, MIN(PaymentDateValue) AS First_Payment_Date
                                                FROM #ValidPayments
                                                GROUP BY AccountNo
                                            ) t2 ON v.AccountNo = t2.AccountNo AND v.PaymentDateValue = t2.First_Payment_Date;
                        
                                            CREATE INDEX IX_PTable5_AccountNo ON #PTable5(AccountNo);
                        
                                            ---------------------------------------------------
                                            -- STEP 3: Combine all precomputed tables
                                            ---------------------------------------------------
                                            IF OBJECT_ID('tempdb..#ContractInfo') IS NOT NULL DROP TABLE #ContractInfo;
                        
                                            SELECT 
                                                d.Id AS AccountNo, 
                                                ci.TotalAmount,
                                                p1.Total_Paid AS TotalPaid,
                                                p5.FirstPaidDate,
                                                p5.First_Paid_Amount AS FirstPaymentAmount,
                                                p5.First_MPesaCode AS FirstMPesaCode,
                                                p4.LastPaidDate,
                                                p4.Last_Paid_Amount AS LastPaymentAmount,
		                                        p24.Last24hrPaidAmount,
                                                p4.Last_MPesaCode AS LastMPesaCode,
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
                                                d.[Name],
                                                d.ImeiNo,
                                                d.Status,
                                                d.LockType,
                                                d.NextLockDateIsoFormat,
                                                d.NextLockDate
                                            INTO #ContractInfo
                                            FROM Devices d
                                            JOIN #PTable1 p1 ON d.Id = p1.AccountNo
                                            JOIN #PTable5 p5 ON d.Id = p5.AccountNo
                                            JOIN #PTable4 p4 ON d.Id = p4.AccountNo
                                            JOIN Contract_Info ci ON ci.ID = d.Id
	                                        LEFT JOIN #PTable24hrs p24 ON d.Id = p24.AccountNo
                                            WHERE d.[Status] = 'enrolled'
                                             AND (@UserId = 0 OR ci.DebtCollectorUserId = @UserId)
                                            AND ci.EndDate IS NULL;
                        
                                            CREATE INDEX IX_ContractInfo_AccountNo ON #ContractInfo(AccountNo);
                        
                                            ---------------------------------------------------
                                            -- STEP 4: Return results
                                            ---------------------------------------------------
                                            SELECT *
                                            FROM #ContractInfo
                                            WHERE (@DealerId = 0
                                                OR DeviceGroupId = @DealerId
                                                )
                                            AND (@AccountId IS NULL
                                                OR AccountNo = @AccountId
                                                )
                                            AND (@searchParam IS NULL
                                                OR AccountNo LIKE '%' + @searchParam + '%'
                                                OR FirstMPesaCode LIKE '%' + @searchParam + '%'                            
    				                                        OR FirstMPesaCode LIKE '%' + @searchParam + '%'
                                                OR CustomerName LIKE '%' + @searchParam + '%'  
                                                )
                                            ORDER BY LastPaidDate DESC
                        	OFFSET @offset ROWS 
                        	FETCH NEXT @pageSize ROWS ONLY;";

            var searchParam = string.IsNullOrWhiteSpace(searchTerm) ? null : searchTerm;
            var payments = await _db.QueryAsync<PaymentSummary>(sql, new { UserId = userId, DealerId = deviceGroupId, offset, pageSize, searchParam, AccountId = accountId });

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
            return @"WITH EnrolledContracts AS (
    SELECT 
        d.Id,
        d.DeviceGroupId,
        ci.First_Name AS CustomerName
    FROM Devices d
    INNER JOIN Contract_Info ci ON ci.ID = d.Id
    AND ci.DebtCollectorUserId = @UserId
    AND ci.EndDate IS NULL
    WHERE d.Status = 'enrolled'
    AND (@UserId = 0 OR ci.DebtCollectorUserId = @UserId)
    AND ci.EndDate IS NULL
)
SELECT COUNT(DISTINCT kp.AccountNoBigint) AS ActiveAccountCount
FROM KosePayments kp
JOIN EnrolledContracts ec 
    ON ec.Id = kp.AccountNoBigint
WHERE kp.AccountNoBigint IS NOT NULL
  AND (@DealerId = 0 OR ec.DeviceGroupId = @DealerId)
  AND (@AccountId IS NULL OR kp.AccountNoBigint = @AccountId)
  AND (
        @searchParam IS NULL
        OR CAST(kp.AccountNoBigint AS NVARCHAR(50)) LIKE '%' + @searchParam + '%'
        OR kp.MpesaCode LIKE '%' + @searchParam + '%'
        OR ec.CustomerName LIKE '%' + @searchParam + '%'
      );";
        }

        public async Task<ContractViewModel> GetAccountsByDealerAsync(int dealerId, int page, int pageSize, string searchTerm)
        {
            var offset = (page - 1) * pageSize;

            var countSql = @"SELECT COUNT(*) FROM Contract_Info  
                            WHERE (
                            @SearchTerm IS NULL
                            OR First_Name LIKE '%' + @SearchTerm + '%'
                            OR ID LIKE '%' + @SearchTerm + '%'
                            )
                            AND EndDate IS NULL
                            AND AssignedAgentId IS NULL";

            var totalRecords = await _db.QuerySingleAsync<int>(countSql, new { SearchTerm = searchTerm });

            var sql = @"SELECT [ContractID]
                      ,CI.[ID]
	                  ,First_Name As FirstName
	                  ,Daily
                      ,[StartDate]
                      ,[EndDate]
	                  ,[AssignedAgentId]
	                  ,Deposit
                  FROM [dbo].[Contract_Info] CI
                  INNER JOIN Devices D on D.Id = CI.ID
                  WHERE D.DeviceGroupId = 9085
                  AND [AssignedAgentId] IS NULL
                   ORDER BY [ContractID] DESC
                        OFFSET @Offset ROWS 
                        FETCH NEXT @pageSize ROWS ONLY";

            var contracts = await _db.QueryAsync<ContractInfo>(sql, new { Offset = offset, pageSize = pageSize, SearchTerm = searchTerm });

            var result = new ContractViewModel()
            {
                Contracts = contracts.ToList(),
                CurrentPage = page,
                PageSize = pageSize,
                SearchTerm = searchTerm,
                TotalRecords = totalRecords,
                TotalPages = totalRecords / pageSize,
            };

            return result;
        }


        public async Task<ContractViewModel> GetAssignedAccountsByDealerAsync(int dealerId, int page, int pageSize, string searchTerm)
        {
            var offset = (page - 1) * pageSize;

            var countSql = @"SELECT COUNT(*) FROM Contract_Info  
                            WHERE (
                            @SearchTerm IS NULL
                            OR First_Name LIKE '%' + @SearchTerm + '%'
                            OR ID LIKE '%' + @SearchTerm + '%'
                            )
                            AND EndDate IS NULL
                            AND AssignedAgentId IS NOT NULL";

            var totalRecords = await _db.QuerySingleAsync<int>(countSql, new { SearchTerm = searchTerm });

            var sql = @"SELECT [ContractID]
                      ,CI.[ID]
	                  ,First_Name As FirstName
	                  ,Daily
                      ,[StartDate]
                      ,[EndDate]
	                  ,[AssignedAgentId]
	                  ,Deposit
                      ,u.[Name] + ' ' + u.[LastName] AS AssignedAgentName
                FROM [dbo].[Contract_Info] CI
                INNER JOIN Devices D on D.Id = CI.ID
	            INNER JOIN Users u on u.UserId = CI.AssignedAgentId
                WHERE D.DeviceGroupId = 9085
                AND [AssignedAgentId] IS NOT NULL
                ORDER BY [ContractID] DESC
                        OFFSET @Offset ROWS 
                        FETCH NEXT @pageSize ROWS ONLY";

            var contracts = await _db.QueryAsync<ContractInfo>(sql, new { Offset = offset, pageSize = pageSize, SearchTerm = searchTerm });

            var result = new ContractViewModel()
            {
                Contracts = contracts.ToList(),
                CurrentPage = page,
                PageSize = pageSize,
                SearchTerm = searchTerm,
                TotalRecords = totalRecords,
                TotalPages = totalRecords / pageSize,
            };

            return result;
        }

        public async Task AssignAccountToAgentAsync(int contractId, int agentId)
        {
            const string sql = @"
                UPDATE Contract_Info
                SET AssignedAgentId = @AssignedAgentId
                WHERE ID = @ContractId;
            ";

            await _db.ExecuteAsync(sql, new
            {
                ContractId = contractId,
                AssignedAgentId = agentId
            });
        }
    }
}
