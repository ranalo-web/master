using System.Data;
using Dapper;

namespace Ranalo.DataStore
{
    public interface IWriteOffRepository
    {
        Task<int> ReinstatePaidAsync();
        Task<int> ProposeNoPaymentWriteOffsAsync(DateTime today);
    }

    // Maintains the WriteOffs register (Database/WriteOffs/001). See that
    // script for the policy. The nightly job (ScheduledWriteOffCheck) calls
    // ReinstatePaidAsync first, then ProposeNoPaymentWriteOffsAsync, so an
    // account that paid starts a fresh 360-day clock before new proposals
    // are made.
    public class WriteOffRepository : IWriteOffRepository
    {
        public const int DaysWithoutPayment = 360;

        private readonly IDbConnection _db;

        public WriteOffRepository(IDbConnection db)
        {
            _db = db;
        }

        // A contract's payments: its device's payments (the current contract's
        // -- a previous customer's were renamed "_R" on recovery) plus
        // orphaned payments assigned to the device, and only up to the
        // contract's EndDate so a new customer's payments on the same device
        // never count for an old contract.
        private const string ContractPaymentsWhere = @"
                (kp.AccountNoBigint = ci.ID
                 OR EXISTS (SELECT 1 FROM OrphanedPayments op
                            WHERE op.MpesaCode = kp.MpesaCode AND op.AccountNoBigint = ci.ID))
                AND (ci.EndDate IS NULL OR kp.PaymentDateValue < ci.EndDate)";

        // Any payment on or after the write-off date brings a "no payment"
        // write-off back, whatever its status: an Approved one is reinstated
        // (full balance back, dated on the payment day), and a Pending/Held
        // proposal is closed the same way so it drops off the review list.
        // Repossessed write-offs are never reinstated -- the device has
        // gone to a new customer.
        public async Task<int> ReinstatePaidAsync()
        {
            var sql = @"
                UPDATE wo
                SET ReinstatedDate = CAST(p.PaymentDateValue AS DATE),
                    ReinstatedMpesaCode = p.MpesaCode
                FROM WriteOffs wo
                INNER JOIN Contract_Info ci ON ci.ContractID = wo.ContractId
                CROSS APPLY (
                    SELECT TOP 1 kp.PaymentDateValue, kp.MpesaCode
                    FROM KosePayments kp
                    WHERE " + ContractPaymentsWhere + @"
                      AND kp.PaymentDateValue >= wo.WrittenOffDate
                    ORDER BY kp.PaymentDateValue
                ) p
                WHERE wo.ReinstatedDate IS NULL
                  AND wo.Reason = 'NoPayment360'";

            return await _db.ExecuteAsync(sql);
        }

        // Proposes (Status = Pending) a write-off for every open contract
        // that still owes money and has gone DaysWithoutPayment days since
        // its last payment -- or since its start date if it never paid --
        // dated on the day it crossed that line. Skips contracts that
        // already have an open (not reinstated) write-off of any status.
        public async Task<int> ProposeNoPaymentWriteOffsAsync(DateTime today)
        {
            var sql = @"
                ;WITH contracts AS (
                    SELECT
                        ci.ContractID,
                        CAST(ci.ID AS BIGINT) AS AccountNo,
                        ci.StartDate,
                        CAST(ci.Deposit + ci.Daily * 30 * ci.Term_in_Months
                             + ci.Weekly * (30.0 / 7.0) * ci.Term_in_Months
                             + ci.Monthly * ci.Term_in_Months AS DECIMAL(18,2)) AS ContractValue,
                        ISNULL(paid.TotalPaid, 0) AS TotalPaid,
                        paid.LastPaymentDate
                    FROM Contract_Info ci
                    OUTER APPLY (
                        SELECT SUM(kp.AmountValue) AS TotalPaid, MAX(kp.PaymentDateValue) AS LastPaymentDate
                        FROM KosePayments kp
                        WHERE " + ContractPaymentsWhere + @"
                    ) paid
                    WHERE ci.EndDate IS NULL
                      AND ci.StartDate IS NOT NULL
                      AND ci.ContractID IS NOT NULL
                ),
                -- One row per ContractID (the latest start) so a duplicated
                -- ContractID can never trip UX_WriteOffs_Contract_Date and
                -- fail the whole insert.
                due AS (
                    SELECT *,
                        DATEADD(DAY, @Days, CAST(COALESCE(LastPaymentDate, StartDate) AS DATE)) AS CrossedDate,
                        ROW_NUMBER() OVER (PARTITION BY ContractID ORDER BY StartDate DESC) AS rn
                    FROM contracts
                    WHERE TotalPaid < ContractValue
                )
                INSERT INTO WriteOffs
                    (ContractId, AccountNo, Reason, WrittenOffDate, LastPaymentDate, ContractValue, TotalPaid, OutstandingBalance, Status)
                SELECT
                    ContractID, AccountNo, 'NoPayment360', CrossedDate, CAST(LastPaymentDate AS DATE),
                    ContractValue, TotalPaid, ContractValue - TotalPaid, 'Pending'
                FROM due
                WHERE rn = 1
                  AND CrossedDate <= @Today
                  AND NOT EXISTS (SELECT 1 FROM WriteOffs wo
                                  WHERE wo.ContractId = due.ContractID
                                    AND (wo.ReinstatedDate IS NULL OR wo.WrittenOffDate = due.CrossedDate))";

            return await _db.ExecuteAsync(sql, new { Days = DaysWithoutPayment, Today = today.Date });
        }
    }
}
