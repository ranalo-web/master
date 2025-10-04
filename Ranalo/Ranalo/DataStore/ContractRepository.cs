using Dapper;
using DocumentFormat.OpenXml.Drawing;
using Ranalo.Calculator.Logic.Models;
using Ranalo.Models;
using Ranalo.Woocommece.Api.Models;
using System.Data;

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
            var sql = "SELECT * FROM Contract_Info WHERE ContractID = @contractId";
            return await _db.QueryFirstOrDefaultAsync<ContractInfo>(sql, new { contractId });
        }

        public async Task<ContractInfo?> GetContractByDeviceIdAsync(int deviceId)
        {
            var sql = "SELECT * FROM Contract_Info WHERE ID = @DeviceId";
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
                        )";

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
                        FROM Contract_Info
                         WHERE (
                            @SearchTerm IS NULL
                            OR First_Name LIKE '%' + @SearchTerm + '%'
                            OR ID LIKE '%' + @SearchTerm + '%'
                        )
                        ORDER BY First_Name
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
                Total_Cost = @TotalCost
            WHERE ID = @ID";

            return await _db.ExecuteAsync(sql, contract);
        }

        // Delete
        public async Task<int> DeleteContractAsync(int contractId)
        {
            var sql = "DELETE FROM Contract_Info WHERE ContractID = @contractId";
            return await _db.ExecuteAsync(sql, new { contractId });
        }
    }
}
