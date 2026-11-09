using System.Data;
using Dapper;

namespace Ranalo.DataStore
{
    public class BirthdayAccount : MilestoneAccount
    {
        public string? Dob { get; set; }
    }

    public class UpgradeAccount : MilestoneAccount
    {
        public decimal Paid { get; set; }
        public decimal Owed { get; set; }
        public decimal Behind { get; set; }
        public bool Restructured { get; set; }
    }

    public interface ICelebrationsRepository
    {
        Task<List<UpgradeAccount>> GetNearlyPaidAsync(decimal from, int maxFailures);
        Task<List<MilestoneAccount>> GetOpenAsync(string messageType, int maxFailures);
        Task<List<BirthdayAccount>> GetWithDobAsync(string messageType, int maxFailures);
        Task<List<MilestoneAccount>> GetHalfwayAsync(decimal from, decimal to, int maxFailures);
    }

    // Who a greeting goes to (CelebrationRules): open contracts on enrolled
    // phones that aren't TVs -- the announcement list -- not yet sent this
    // greeting, and not failed maxFailures times. Knox logs carry the IMEI,
    // the others the account number, so both are checked.
    public class CelebrationsRepository : ICelebrationsRepository
    {
        private readonly IDbConnection _db;

        public CelebrationsRepository(IDbConnection db)
        {
            _db = db;
        }

        private const string Columns = @"
                SELECT ci.ID AS AccountId, ci.First_Name AS FirstName, d.ImeiNo AS Imei,
                       ISNULL(d.LockGroup, 1) AS LockGroup, ISNULL(ci.Daily, 0) AS Daily";

        private const string OpenNotSent = @"
                FROM Contract_Info ci
                JOIN Devices d ON d.Id = ci.ID
                WHERE ci.EndDate IS NULL AND d.Status = 'enrolled' AND ISNULL(d.IsTv, 0) = 0
                  AND NOT EXISTS (SELECT 1 FROM MessageLogs m
                                  WHERE m.MessageType = @MessageType AND m.MessageStatus = 'sent'
                                    AND m.AccountNo IN (CAST(ci.ID AS NVARCHAR(50)), d.ImeiNo))
                  AND (SELECT COUNT(*) FROM MessageLogs m
                        WHERE m.MessageType = @MessageType AND m.MessageStatus = 'failed'
                          AND m.AccountNo IN (CAST(ci.ID AS NVARCHAR(50)), d.ImeiNo)) < @MaxFailures";

        public async Task<List<MilestoneAccount>> GetOpenAsync(string messageType, int maxFailures) =>
            (await _db.QueryAsync<MilestoneAccount>(Columns + OpenNotSent + " ORDER BY ci.ID",
                new { MessageType = messageType, MaxFailures = maxFailures })).ToList();

        // The date of birth typed on the WooCommerce application, found the
        // way the orders pages find an account: deposit M-Pesa code -> payment
        // -> device. The newest order with one wins.
        public async Task<List<BirthdayAccount>> GetWithDobAsync(string messageType, int maxFailures) =>
            (await _db.QueryAsync<BirthdayAccount>(Columns + @",
                       (SELECT TOP 1 LTRIM(RTRIM(wo.DOB))
                        FROM Woo_Orders wo
                        JOIN KosePayments kp ON kp.MpesaCode = wo.MpesaDepositRef
                        WHERE kp.AccountNoBigint = ci.ID AND NULLIF(LTRIM(RTRIM(wo.DOB)), '') IS NOT NULL
                        ORDER BY wo.Id DESC) AS Dob" + OpenNotSent + " ORDER BY ci.ID",
                new { MessageType = messageType, MaxFailures = maxFailures }))
            .Where(a => !string.IsNullOrWhiteSpace(a.Dob)).ToList();

        // Paid against the contract's total (deposit plus the whole term), as
        // MilestoneMessagesRepository counts "fully paid".
        public async Task<List<MilestoneAccount>> GetHalfwayAsync(decimal from, decimal to, int maxFailures) =>
            (await _db.QueryAsync<MilestoneAccount>(@"
                WITH owed AS (
                    SELECT ci.ID, ci.First_Name, d.ImeiNo, ISNULL(d.LockGroup, 1) AS LockGroup, ISNULL(ci.Daily, 0) AS Daily,
                           ISNULL(ci.Deposit, 0) + ISNULL(ci.Daily, 0) * 30 * ci.Term_in_Months
                             + ISNULL(ci.Weekly, 0) * (30.0 / 7.0) * ci.Term_in_Months
                             + ISNULL(ci.Monthly, 0) * ci.Term_in_Months AS Owed,
                           (SELECT ISNULL(SUM(p.AmountValue), 0) FROM " + AccountPaymentsSql.For("ci.ID") + @" p) AS Paid" +
                    OpenNotSent + @"
                )
                SELECT ID AS AccountId, First_Name AS FirstName, ImeiNo AS Imei, LockGroup, Daily
                FROM owed
                WHERE Owed > 0 AND Paid >= Owed * @From AND Paid < Owed * @To
                ORDER BY ID",
                new { MessageType = Ranalo.Services.CelebrationRules.HalfwayType, MaxFailures = maxFailures, From = from, To = to },
                commandTimeout: 120)).ToList();

        // For the upgrade offer: accounts past `from` of the contract, with
        // how far behind they are today (the Collections arrears sum: deposit
        // plus each day since the start, up to the term) and whether they
        // have ever been restructured. CelebrationRules picks who's offered.
        public async Task<List<UpgradeAccount>> GetNearlyPaidAsync(decimal from, int maxFailures) =>
            (await _db.QueryAsync<UpgradeAccount>(@"
                WITH a AS (
                    SELECT ci.ID, ci.First_Name, d.ImeiNo, ISNULL(d.LockGroup, 1) AS LockGroup,
                           ISNULL(ci.Daily, 0) + ISNULL(ci.Weekly, 0) / 7.0 + ISNULL(ci.Monthly, 0) / 30.0 AS Daily,
                           ISNULL(ci.Deposit, 0) + ISNULL(ci.Daily, 0) * 30 * ci.Term_in_Months
                             + ISNULL(ci.Weekly, 0) * (30.0 / 7.0) * ci.Term_in_Months
                             + ISNULL(ci.Monthly, 0) * ci.Term_in_Months AS Owed,
                           ISNULL(ci.Deposit, 0)
                             + (ISNULL(ci.Daily, 0) + ISNULL(ci.Weekly, 0) / 7.0 + ISNULL(ci.Monthly, 0) / 30.0)
                               * CASE WHEN DATEDIFF(DAY, ci.StartDate, GETDATE()) < CAST(ci.Term_in_Months * 30 AS INT)
                                      THEN DATEDIFF(DAY, ci.StartDate, GETDATE()) ELSE CAST(ci.Term_in_Months * 30 AS INT) END AS DueToday,
                           (SELECT ISNULL(SUM(p.AmountValue), 0) FROM " + AccountPaymentsSql.For("ci.ID") + @" p) AS Paid,
                           CASE WHEN EXISTS (SELECT 1 FROM RestructuredRecords rr WHERE rr.AccountNo = ci.ID) THEN 1 ELSE 0 END AS Restructured" +
                    OpenNotSent + @"
                      AND ci.StartDate IS NOT NULL
                )
                SELECT ID AS AccountId, First_Name AS FirstName, ImeiNo AS Imei, LockGroup, Daily,
                       Paid, Owed, CASE WHEN DueToday > Paid THEN DueToday - Paid ELSE 0 END AS Behind,
                       CAST(Restructured AS BIT) AS Restructured
                FROM a
                WHERE Owed > 0 AND Paid >= Owed * @From
                ORDER BY ID",
                new { MessageType = Ranalo.Services.CelebrationRules.UpgradeType, MaxFailures = maxFailures, From = from },
                commandTimeout: 120)).ToList();
    }
}
