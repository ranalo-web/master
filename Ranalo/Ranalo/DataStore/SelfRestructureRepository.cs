using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Ranalo.Models;

namespace Ranalo.DataStore
{
    // Database/Customers/004 (SelfRestructureRules). Starting and undoing a
    // plan each run in one transaction with the RestructuredRecords change,
    // so a plan can't be half saved.
    public interface ISelfRestructureRepository
    {
        Task<int?> GetOpenContractIdAsync(long accountNo);
        Task<bool> HasUsedAsync(int contractId);
        Task<bool> InRecoveryAsync(long accountNo);
        Task<SelfRestructure?> GetLatestAsync(long accountNo);
        Task<bool> AddPendingAsync(SelfRestructure plan);
        Task<List<SelfRestructure>> GetPendingAsync();
        Task<decimal> PaidBetweenAsync(long accountNo, DateTime fromKenya, DateTime toKenya);
        Task<bool> StartAsync(SelfRestructure plan, DateTime dateAgreedKenya);
        Task SetStatusAsync(int id, string from, string to, string? note = null);
        Task<List<SelfRestructure>> ListAsync(bool toReviewOnly);
        Task<bool> MarkReviewedAsync(int id, int userId);
        Task<string?> RevertAsync(int id, int userId, string note);
    }

    public class PlanRow
    {
        public DateTime DateAgreed { get; set; }
        public decimal AmountRes { get; set; }
    }

    public class SelfRestructureRepository : ISelfRestructureRepository
    {
        private readonly IDbConnection _db;

        public SelfRestructureRepository(IDbConnection db)
        {
            _db = db;
        }

        public async Task<int?> GetOpenContractIdAsync(long accountNo) =>
            await _db.QueryFirstOrDefaultAsync<int?>(@"
                SELECT TOP 1 ContractID FROM Contract_Info
                WHERE ID = @AccountNo AND EndDate IS NULL ORDER BY ContractID DESC", new { AccountNo = accountNo });

        // Their one go: a plan that started, even if an admin undid it.
        public async Task<bool> HasUsedAsync(int contractId) =>
            await _db.ExecuteScalarAsync<bool>(@"
                SELECT CASE WHEN EXISTS (SELECT 1 FROM SelfRestructures
                    WHERE ContractId = @ContractId AND Status IN ('Active', 'Reverted')) THEN 1 ELSE 0 END",
                new { ContractId = contractId });

        // An open recovery: the phone's lock is being sorted out by staff.
        public async Task<bool> InRecoveryAsync(long accountNo) =>
            await _db.ExecuteScalarAsync<bool>(@"
                SELECT CASE WHEN EXISTS (SELECT 1 FROM LockRecoveries
                    WHERE AccountNo = @AccountNo AND Status NOT IN ('Closed', 'NotCatchable', 'MovedToTranssion', 'MovedToKnox'))
                THEN 1 ELSE 0 END", new { AccountNo = accountNo });

        public async Task<SelfRestructure?> GetLatestAsync(long accountNo) =>
            await _db.QueryFirstOrDefaultAsync<SelfRestructure>(@"
                SELECT TOP 1 * FROM SelfRestructures WHERE AccountNo = @AccountNo
                ORDER BY ChosenAtUtc DESC, Id DESC", new { AccountNo = accountNo });

        // False when the contract already has a pending or used plan (the
        // unique indexes refuse it, even if two requests race).
        public async Task<bool> AddPendingAsync(SelfRestructure p)
        {
            try
            {
                await _db.ExecuteAsync(@"
                    INSERT INTO SelfRestructures (AccountNo, ContractId, Status, OptionName, Daily, BalanceAtChoice, BehindAtChoice,
                                                  ChosenAtUtc, ExpiresAtUtc, PrevHadPlan)
                    VALUES (@AccountNo, @ContractId, 'Pending', @OptionName, @Daily, @BalanceAtChoice, @BehindAtChoice,
                            @ChosenAtUtc, @ExpiresAtUtc, 0)", p);
                return true;
            }
            catch (SqlException ex) when (ex.Number is 2601 or 2627)
            {
                return false;
            }
        }

        public async Task<List<SelfRestructure>> GetPendingAsync() =>
            (await _db.QueryAsync<SelfRestructure>("SELECT * FROM SelfRestructures WHERE Status = 'Pending'")).ToList();

        // Payments on the account (re-pointed ones included) in the window, Kenya time.
        public async Task<decimal> PaidBetweenAsync(long accountNo, DateTime fromKenya, DateTime toKenya) =>
            await _db.ExecuteScalarAsync<decimal>(@"
                SELECT ISNULL(SUM(p.AmountValue), 0) FROM " + AccountPaymentsSql.For("@AccountNo") + @" p
                WHERE p.PaymentDateValue > @From AND p.PaymentDateValue <= @To",
                new { AccountNo = accountNo, From = fromKenya, To = toKenya });

        // Saves the plan as staff plans are saved (one plan per account,
        // overwriting any earlier one -- InsertRestructured), keeping that
        // earlier one on this row for an undo. Only if it's still Pending.
        public async Task<bool> StartAsync(SelfRestructure plan, DateTime dateAgreedKenya)
        {
            if (_db.State != ConnectionState.Open) _db.Open();
            using var tx = _db.BeginTransaction();

            var claimed = await _db.ExecuteAsync(@"
                UPDATE SelfRestructures SET Status = 'Active', StartedAtUtc = SYSUTCDATETIME()
                WHERE Id = @Id AND Status = 'Pending'", new { plan.Id }, tx);
            if (claimed != 1)
            {
                tx.Rollback();
                return false;
            }

            var prev = await _db.QueryFirstOrDefaultAsync<PlanRow>(@"
                SELECT TOP 1 Date_Agreed AS DateAgreed, Amount_Res AS AmountRes FROM RestructuredRecords WITH (UPDLOCK, HOLDLOCK)
                WHERE AccountNo = @AccountNo ORDER BY Date_Agreed DESC, Id DESC", new { plan.AccountNo }, tx);

            await _db.ExecuteAsync(@"
                UPDATE SelfRestructures SET PrevHadPlan = @Had, PrevDateAgreed = @PrevDate, PrevAmountRes = @PrevAmount
                WHERE Id = @Id",
                new { plan.Id, Had = prev != null, PrevDate = prev?.DateAgreed, PrevAmount = prev?.AmountRes }, tx);

            if (prev != null)
            {
                await _db.ExecuteAsync(@"
                    UPDATE RestructuredRecords SET Date_Agreed = @DateAgreed, Amount_Res = @AmountRes
                    WHERE AccountNo = @AccountNo",
                    new { plan.AccountNo, DateAgreed = dateAgreedKenya, AmountRes = plan.Daily }, tx);
            }
            else
            {
                await _db.ExecuteAsync(@"
                    INSERT INTO RestructuredRecords (AccountNo, Date_Agreed, Amount_Res, Days_Restructured, Total_Due_R, Total_Paid_R, Arrears_R, Auto_lock_date_pmt_R)
                    VALUES (@AccountNo, @DateAgreed, @AmountRes, 0, 0, 0, 0, @AutoLock)",
                    new { plan.AccountNo, DateAgreed = dateAgreedKenya, AmountRes = plan.Daily, AutoLock = DateTime.UtcNow.AddHours(24) }, tx);
            }

            tx.Commit();
            return true;
        }

        public async Task SetStatusAsync(int id, string from, string to, string? note = null) =>
            await _db.ExecuteAsync(@"
                UPDATE SelfRestructures SET Status = @To, Note = COALESCE(LEFT(@Note, 400), Note)
                WHERE Id = @Id AND Status = @From", new { Id = id, From = from, To = to, Note = note });

        public async Task<List<SelfRestructure>> ListAsync(bool toReviewOnly) =>
            (await _db.QueryAsync<SelfRestructure>(@"
                SELECT TOP 300 s.*, ru.KnownAs AS ReviewedBy, vu.KnownAs AS RevertedBy, ci.First_Name AS FirstName
                FROM SelfRestructures s
                LEFT JOIN Users ru ON ru.UserId = s.ReviewedByUserId
                LEFT JOIN Users vu ON vu.UserId = s.RevertedByUserId
                LEFT JOIN Contract_Info ci ON ci.ContractID = s.ContractId
                WHERE (@ToReview = 0 AND s.Status <> 'Expired')
                   OR (@ToReview = 1 AND s.Status = 'Active' AND s.ReviewedAtUtc IS NULL)
                ORDER BY s.ChosenAtUtc DESC", new { ToReview = toReviewOnly })).ToList();

        public async Task<bool> MarkReviewedAsync(int id, int userId) =>
            await _db.ExecuteAsync(@"
                UPDATE SelfRestructures SET ReviewedAtUtc = SYSUTCDATETIME(), ReviewedByUserId = @UserId
                WHERE Id = @Id AND Status = 'Active' AND ReviewedAtUtc IS NULL", new { Id = id, UserId = userId }) == 1;

        // Puts the previous plan back. Refused if the plan has been changed
        // since (staff agreed a new one): that one is left alone.
        public async Task<string?> RevertAsync(int id, int userId, string note)
        {
            if (_db.State != ConnectionState.Open) _db.Open();
            using var tx = _db.BeginTransaction();

            var plan = await _db.QueryFirstOrDefaultAsync<SelfRestructure>(
                "SELECT * FROM SelfRestructures WITH (UPDLOCK) WHERE Id = @Id", new { Id = id }, tx);
            if (plan == null || plan.Status != SelfRestructureStatus.Active)
            {
                tx.Rollback();
                return "Only a plan that has started can be undone.";
            }

            var current = await _db.QueryFirstOrDefaultAsync<PlanRow>(@"
                SELECT TOP 1 Date_Agreed AS DateAgreed, Amount_Res AS AmountRes FROM RestructuredRecords WITH (UPDLOCK, HOLDLOCK)
                WHERE AccountNo = @AccountNo ORDER BY Date_Agreed DESC, Id DESC", new { plan.AccountNo }, tx);
            var expectedAgreed = plan.ChosenAtUtc.AddHours(3);
            if (current == null || current.AmountRes != plan.Daily || Math.Abs((current.DateAgreed - expectedAgreed).TotalSeconds) > 5)
            {
                tx.Rollback();
                return "The account's plan has been changed since; nothing was undone.";
            }

            if (plan.PrevHadPlan)
            {
                await _db.ExecuteAsync(@"
                    UPDATE RestructuredRecords SET Date_Agreed = @DateAgreed, Amount_Res = @AmountRes WHERE AccountNo = @AccountNo",
                    new { plan.AccountNo, DateAgreed = plan.PrevDateAgreed, AmountRes = plan.PrevAmountRes }, tx);
            }
            else
            {
                await _db.ExecuteAsync("DELETE FROM RestructuredRecords WHERE AccountNo = @AccountNo", new { plan.AccountNo }, tx);
            }

            await _db.ExecuteAsync(@"
                UPDATE SelfRestructures SET Status = 'Reverted', RevertedAtUtc = SYSUTCDATETIME(), RevertedByUserId = @UserId,
                       ReviewedAtUtc = COALESCE(ReviewedAtUtc, SYSUTCDATETIME()), ReviewedByUserId = COALESCE(ReviewedByUserId, @UserId),
                       Note = LEFT(@Note, 400)
                WHERE Id = @Id", new { Id = id, UserId = userId, Note = note }, tx);

            tx.Commit();
            return null;
        }
    }
}
