using System.Data;
using Dapper;
using Ranalo.Models;

namespace Ranalo.DataStore
{
    public interface IRepoStockRepository
    {
        Task<RepoStockLookup?> LookupAsync(long accountNo);
        Task<List<RepoStockItem>> GetListAsync(string status);
        Task<RepoStockItem?> GetAsync(int id);
        Task<int> AddAsync(RepoStockLookup account, string paymentSuffix, string reason, string? condition, int userId);
        Task<RepoOldContract?> GetOldContractAsync(int contractId, string paymentAccount);
        Task<(List<RepoPayment> Page, int Total)> GetPaymentsAsync(string paymentAccount, int page, int pageSize);
        Task<int> SellAsync(RepoStockItem item, RepoSale sale, RepoOldContract old, decimal writeOff, int userId);
    }

    // RepoStock and its effect on Contract_Info, payments and write-offs
    // (Database/RepoStock/001). Each change runs in one transaction.
    public class RepoStockRepository : IRepoStockRepository
    {
        private readonly IDbConnection _db;

        public RepoStockRepository(IDbConnection db)
        {
            _db = db;
        }

        public async Task<RepoStockLookup?> LookupAsync(long accountNo) =>
            await _db.QueryFirstOrDefaultAsync<RepoStockLookup>(@"
                SELECT d.Id AS AccountNo, CAST(d.ImeiNo AS VARCHAR(20)) AS Imei, d.Make, d.Model, d.LockGroup, d.DeviceGroupId,
                       c.ContractID AS ContractId, c.First_Name AS CustomerName, c.EndDate AS ContractEndedOn,
                       dl.CompanyName AS DealerName,
                       (SELECT TOP 1 cc.Id FROM CollectionCases cc WHERE cc.AccountNo = d.Id AND cc.Status = 'Open') AS OpenCollectionsCaseId,
                       (SELECT COUNT(*) FROM RepoStock rs WHERE rs.AccountNo = d.Id) AS TimesInStockBefore,
                       CAST(CASE WHEN EXISTS (SELECT 1 FROM RepoStock rs WHERE rs.AccountNo = d.Id AND rs.Status = 'InStock') THEN 1 ELSE 0 END AS BIT) AS InStockNow
                FROM Devices d
                -- The account's latest contract: open, or already ended
                -- elsewhere (e.g. 8478363, ended before repo stock existed).
                -- Not one that has been through repo stock already.
                OUTER APPLY (SELECT TOP 1 ci.ContractID, ci.First_Name, ci.EndDate FROM Contract_Info ci
                             WHERE ci.ID = d.Id
                               AND ci.ContractID = (SELECT MAX(x.ContractID) FROM Contract_Info x WHERE x.ID = d.Id)
                               AND ci.PaymentAccount IS NULL) c
                LEFT JOIN Dealers dl ON dl.DealerReference = d.DeviceGroupId
                WHERE d.Id = @AccountNo", new { AccountNo = accountNo });

        private const string SelectSql = @"
            SELECT rs.*, d.LockGroup AS CurrentLockGroup,
                   LTRIM(RTRIM(ISNULL(iu.[Name], '') + ' ' + ISNULL(iu.[LastName], ''))) AS IntakeByName,
                   LTRIM(RTRIM(ISNULL(su.[Name], '') + ' ' + ISNULL(su.[LastName], ''))) AS SoldByName,
                   sd.CompanyName AS SoldDealerName
            FROM RepoStock rs
            LEFT JOIN Devices d ON d.Id = rs.AccountNo
            LEFT JOIN Users iu ON iu.UserId = rs.IntakeByUserId
            LEFT JOIN Users su ON su.UserId = rs.SoldByUserId
            LEFT JOIN Dealers sd ON sd.DealerReference = rs.SoldDealerReference";

        public async Task<List<RepoStockItem>> GetListAsync(string status)
        {
            var where = status switch
            {
                "sold" => "WHERE rs.Status = 'Sold'",
                "all" => "",
                _ => "WHERE rs.Status = 'InStock'",
            };
            return (await _db.QueryAsync<RepoStockItem>($"{SelectSql} {where} ORDER BY rs.IntakeDate DESC")).ToList();
        }

        public async Task<RepoStockItem?> GetAsync(int id) =>
            await _db.QueryFirstOrDefaultAsync<RepoStockItem>($"{SelectSql} WHERE rs.Id = @Id", new { Id = id });

        // Into stock: end the contract today, keep its payments and dealer
        // with it (renamed "<account>_R..."), drop any restructure.
        public async Task<int> AddAsync(RepoStockLookup account, string paymentSuffix, string reason, string? condition, int userId)
        {
            var paymentAccount = $"{account.AccountNo}{paymentSuffix}";
            if (_db.State != ConnectionState.Open) _db.Open();
            using var tx = _db.BeginTransaction();

            var id = await _db.ExecuteScalarAsync<int>(@"
                INSERT INTO RepoStock (AccountNo, Imei, Make, Model, LockGroup, OldContractId, OldCustomerName, PaymentSuffix, Reason, Condition, Status, IntakeByUserId)
                OUTPUT INSERTED.Id
                VALUES (@AccountNo, @Imei, @Make, @Model, @LockGroup, @ContractId, @CustomerName, @Suffix, @Reason, @Condition, 'InStock', @UserId)",
                new
                {
                    account.AccountNo, account.Imei, account.Make, account.Model, account.LockGroup, account.ContractId, account.CustomerName,
                    Suffix = paymentSuffix, Reason = reason, Condition = condition, UserId = userId,
                }, tx);

            var ended = await _db.ExecuteAsync(@"
                UPDATE Contract_Info
                SET EndDate = COALESCE(EndDate, GETDATE()), PaymentAccount = @PaymentAccount, DealerReference = COALESCE(DealerReference, @DeviceGroupId)
                WHERE ContractID = @ContractId AND ID = @AccountNo AND PaymentAccount IS NULL",
                new { PaymentAccount = paymentAccount, account.DeviceGroupId, account.ContractId, account.AccountNo }, tx);
            if (ended != 1)
            {
                throw new InvalidOperationException($"Contract {account.ContractId} on account {account.AccountNo} has changed -- reload and try again.");
            }

            await _db.ExecuteAsync("UPDATE KosePayments SET AccountNo = @PaymentAccount WHERE AccountNoBigint = @AccountNo",
                new { PaymentAccount = paymentAccount, account.AccountNo }, tx);
            await _db.ExecuteAsync("UPDATE OrphanedPayments SET AccountNo = @PaymentAccount WHERE AccountNoBigint = @AccountNo",
                new { PaymentAccount = paymentAccount, account.AccountNo }, tx);
            await _db.ExecuteAsync("DELETE FROM RestructuredRecords WHERE AccountNo = @AccountNo",
                new { account.AccountNo }, tx);

            tx.Commit();
            return id;
        }

        // The old contract's value and what its customer paid (its renamed
        // payments, direct or assigned).
        public async Task<RepoOldContract?> GetOldContractAsync(int contractId, string paymentAccount) =>
            await _db.QueryFirstOrDefaultAsync<RepoOldContract>(@"
                SELECT ci.ContractID AS ContractId, ci.BuyingPrice,
                       CAST(ci.Deposit + ci.Daily * 30 * ci.Term_in_Months
                            + ci.Weekly * (30.0 / 7.0) * ci.Term_in_Months
                            + ci.Monthly * ci.Term_in_Months AS DECIMAL(18,2)) AS ContractValue,
                       ISNULL(p.TotalPaid, 0) AS TotalPaid, p.LastPaymentDate
                FROM Contract_Info ci
                OUTER APPLY (
                    SELECT SUM(x.AmountValue) AS TotalPaid, MAX(x.PaymentDateValue) AS LastPaymentDate
                    FROM (" + PaymentsOnSql + @") x
                ) p
                WHERE ci.ContractID = @ContractId", new { ContractId = contractId, PaymentAccount = paymentAccount });

        // Newest first, one page at a time.
        public async Task<(List<RepoPayment> Page, int Total)> GetPaymentsAsync(string paymentAccount, int page, int pageSize)
        {
            using var multi = await _db.QueryMultipleAsync(@"
                SELECT COUNT(*) FROM (" + PaymentsOnSql + @") x;
                SELECT x.MpesaCode, x.AmountValue AS Amount, x.PaymentDateValue AS PaymentDate
                FROM (" + PaymentsOnSql + @") x
                ORDER BY x.PaymentDateValue DESC, x.MpesaCode
                OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY;",
                new { PaymentAccount = paymentAccount, Skip = (Math.Max(page, 1) - 1) * pageSize, Take = pageSize });
            var total = await multi.ReadSingleAsync<int>();
            return ((await multi.ReadAsync<RepoPayment>()).ToList(), total);
        }

        // Payments on a renamed account: typed to it, or assigned to it.
        private const string PaymentsOnSql = @"
                    SELECT kp.MpesaCode, kp.AmountValue, kp.PaymentDateValue
                    FROM KosePayments kp
                    WHERE LTRIM(RTRIM(kp.AccountNo)) = @PaymentAccount
                    UNION
                    SELECT kp.MpesaCode, kp.AmountValue, kp.PaymentDateValue
                    FROM OrphanedPayments op
                    INNER JOIN KosePayments kp ON kp.MpesaCode = op.MpesaCode
                    WHERE LTRIM(RTRIM(op.AccountNo)) = @PaymentAccount";

        // Sold: a new contract on the same account starting today, credited
        // to the seller; the old contract's unpaid balance proposed as a
        // write-off; the phone's dealer becomes the seller.
        public async Task<int> SellAsync(RepoStockItem item, RepoSale sale, RepoOldContract old, decimal writeOff, int userId)
        {
            if (_db.State != ConnectionState.Open) _db.Open();
            using var tx = _db.BeginTransaction();

            var totalLoan = Ranalo.Services.RepoStockRules.TotalLoan(sale.Daily, sale.TermInMonths);
            var totalCost = Ranalo.Services.RepoStockRules.TotalCost(sale.Deposit, sale.Daily, sale.TermInMonths);

            var contractId = await _db.ExecuteScalarAsync<int>(@"
                INSERT INTO Contract_Info
                    (ID, Deposit, Daily, Weekly, Monthly, rePayment_Intervals, Term_in_Months, Total_Loan, Total_Cost,
                     First_Name, StartDate, BuyingPrice, AssignedAgentId, DealerReference)
                VALUES
                    (@AccountNo, @Deposit, @Daily, 0, 0, 'Daily', @Term, @TotalLoan, @TotalCost,
                     @CustomerName, GETDATE(), @BuyingPrice, @AgentUserId, @DealerReference);
                SELECT CAST(SCOPE_IDENTITY() AS INT);",
                new
                {
                    item.AccountNo, sale.Deposit, sale.Daily, Term = sale.TermInMonths, TotalLoan = totalLoan, TotalCost = totalCost,
                    CustomerName = sale.CustomerName.Trim(), sale.BuyingPrice, sale.AgentUserId, sale.DealerReference,
                }, tx);

            await _db.ExecuteAsync("UPDATE Devices SET [Name] = @CustomerName, DeviceGroupId = @DealerReference WHERE Id = @AccountNo",
                new { CustomerName = sale.CustomerName.Trim(), sale.DealerReference, item.AccountNo }, tx);

            await _db.ExecuteAsync(@"
                IF NOT EXISTS (SELECT 1 FROM WriteOffs WHERE ContractId = @OldContractId AND ReinstatedDate IS NULL)
                    INSERT INTO WriteOffs
                        (ContractId, AccountNo, Reason, WrittenOffDate, LastPaymentDate, ContractValue, TotalPaid, OutstandingBalance, Status)
                    VALUES
                        (@OldContractId, @AccountNo, 'Repossessed', CAST(GETDATE() AS DATE), @LastPaymentDate,
                         @ContractValue, @TotalPaid, @WriteOff, 'Pending');

                INSERT INTO DeviceRecoveries (AccountNo, OldContractId, NewContractId, RecoveredDate, ResaleValue, RepossessionCost, Notes, RecordedByUserId)
                VALUES (@AccountNo, @OldContractId, @NewContractId, CAST(@IntakeDate AS DATE), @TotalCost, 0, 'Repo stock', @UserId);",
                new
                {
                    OldContractId = old.ContractId, item.AccountNo, old.LastPaymentDate, old.ContractValue, old.TotalPaid, WriteOff = writeOff,
                    NewContractId = contractId, item.IntakeDate, TotalCost = totalCost, UserId = userId,
                }, tx);

            var sold = await _db.ExecuteAsync(@"
                UPDATE RepoStock
                SET Status = 'Sold', NewContractId = @ContractId, NewCustomerName = @CustomerName, NewCustomerPhone = @Phone,
                    NewCustomerIdNo = @IdNo, SoldDate = SYSUTCDATETIME(), SoldByUserId = @UserId,
                    SoldDealerReference = @DealerReference, WriteOffAmount = @WriteOff
                WHERE Id = @Id AND Status = 'InStock'",
                new
                {
                    ContractId = contractId, CustomerName = sale.CustomerName.Trim(), Phone = sale.CustomerPhone, IdNo = sale.CustomerIdNo,
                    UserId = userId, sale.DealerReference, WriteOff = writeOff, item.Id,
                }, tx);
            if (sold != 1)
            {
                throw new InvalidOperationException("This phone was sold or changed by someone else -- reload and check.");
            }

            tx.Commit();
            return contractId;
        }
    }
}
