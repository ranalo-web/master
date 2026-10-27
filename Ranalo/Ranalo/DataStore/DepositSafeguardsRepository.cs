using System.Data;
using Dapper;
using Ranalo.Services;

namespace Ranalo.DataStore
{
    // Reads for DepositSafeguards: a customer's recent payments, and approved
    // enrolments still without a contract.
    public interface IDepositSafeguardsRepository
    {
        Task<List<AccountPayment>> GetAccountPaymentsAsync(long accountNo, DateTime since);
        Task<List<ApprovedWithoutContract>> GetApprovedWithoutContractAsync(DateTime approvedBefore, DateTime approvedAfter);
    }

    public class DepositSafeguardsRepository : IDepositSafeguardsRepository
    {
        private readonly IDbConnection _db;

        public DepositSafeguardsRepository(IDbConnection db)
        {
            _db = db;
        }

        // On the account the payment belongs to: where it was assigned
        // (Assign Payments) if it was, else the account typed on M-Pesa --
        // as the contract job decides it.
        public async Task<List<AccountPayment>> GetAccountPaymentsAsync(long accountNo, DateTime since)
        {
            var rows = await _db.QueryAsync<AccountPayment>(@"
                SELECT TOP 10
                       CAST(kp.MpesaCode AS NVARCHAR(100)) AS MpesaCode,
                       CAST(ISNULL(kp.AmountValue, 0) AS DECIMAL(18,2)) AS Amount,
                       kp.PaymentDateValue AS PaidAt,
                       (SELECT TOP 1 CAST(wo.OrderID AS BIGINT) FROM Woo_Orders wo
                         WHERE wo.MpesaDepositRef = kp.MpesaCode) AS DepositOnOrderId
                FROM KosePayments kp
                OUTER APPLY (SELECT TOP 1 o.AccountNoBigint FROM OrphanedPayments o
                             WHERE o.MpesaCode = kp.MpesaCode
                             ORDER BY o.DateCreated DESC) op
                WHERE COALESCE(op.AccountNoBigint, kp.AccountNoBigint) = @AccountNo
                  AND kp.PaymentDateValue >= @Since
                ORDER BY kp.PaymentDateValue DESC",
                new { AccountNo = accountNo, Since = since });
            return rows.ToList();
        }

        public async Task<List<ApprovedWithoutContract>> GetApprovedWithoutContractAsync(DateTime approvedBefore, DateTime approvedAfter)
        {
            var rows = await _db.QueryAsync<ApprovedWithoutContract>(@"
                SELECT CAST(e.AccountId AS BIGINT) AS AccountId,
                       CAST(e.OrderId AS BIGINT) AS OrderId,
                       e.FirstName, e.LastName, e.DeviceBrand, e.ApprovedDate,
                       NULLIF(LTRIM(RTRIM(wo.MpesaDepositRef)), '') AS DepositCode,
                       CAST(CASE WHEN kp.MpesaCode IS NULL THEN 0 ELSE 1 END AS BIT) AS DepositReceived,
                       CAST(COALESCE(op.AccountNo, kp.AccountNo) AS NVARCHAR(50)) AS DepositPaidToAccount,
                       d.[Status] AS DeviceStatus
                FROM Enrolments e
                JOIN Woo_Orders wo ON wo.OrderID = e.OrderId
                OUTER APPLY (SELECT TOP 1 k.MpesaCode, k.AccountNo FROM KosePayments k
                             WHERE k.MpesaCode = wo.MpesaDepositRef) kp
                OUTER APPLY (SELECT TOP 1 o.AccountNo FROM OrphanedPayments o
                             WHERE o.MpesaCode = kp.MpesaCode
                             ORDER BY o.DateCreated DESC) op
                LEFT JOIN Devices d ON d.Id = e.AccountId
                WHERE e.[Status] = 2
                  AND e.ApprovedDate <= @ApprovedBefore
                  AND e.ApprovedDate >= @ApprovedAfter
                  AND wo.ContractId IS NULL
                  AND wo.[Status] NOT IN ('cancelled', 'rejected', 'failed', 'refunded')
                  AND NOT EXISTS (SELECT 1 FROM Contract_Info ci
                                  WHERE ci.ID = e.AccountId AND ci.EndDate IS NULL)
                ORDER BY e.ApprovedDate",
                new { ApprovedBefore = approvedBefore, ApprovedAfter = approvedAfter });
            return rows.ToList();
        }
    }
}
