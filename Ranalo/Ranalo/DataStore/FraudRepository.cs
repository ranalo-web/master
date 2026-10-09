using System.Data;
using Dapper;
using Ranalo.Models;

namespace Ranalo.DataStore
{
    public interface IFraudRepository
    {
        Task<List<FraudContractRow>> GetOpenContractsAsync();
        Task<List<FraudOrderRow>> GetOrdersAsync();
        Task<List<FraudNextOfKinRow>> GetNextOfKinAsync();
        Task<List<FraudDeviceRow>> GetDevicesOnOpenContractsAsync();
        Task<List<FraudReview>> GetReviewsAsync();
        Task SaveReviewAsync(string checkCode, string subjectKey, string status, string? notes, int userId);
    }

    // Loads what the fraud checks (Services/FraudRules.cs) look at. Reviews
    // live in FraudFlagReviews (Database/Fraud/001).
    public class FraudRepository : IFraudRepository
    {
        private readonly IDbConnection _db;

        public FraudRepository(IDbConnection db)
        {
            _db = db;
        }

        // Same payment and arrears sums as CollectionsRepository.StandingsSql.
        public async Task<List<FraudContractRow>> GetOpenContractsAsync()
        {
            const string sql = @"
                ;WITH ValidPayments AS (
                    SELECT COALESCE(op.AccountNoBigint, kp.AccountNoBigint) AS AccountNo, kp.AmountValue, kp.PaymentDateValue
                    FROM KosePayments kp
                    LEFT JOIN OrphanedPayments op ON op.MpesaCode = kp.MpesaCode
                ),
                PaymentTotals AS (
                    SELECT AccountNo, SUM(AmountValue) AS TotalPaid, MAX(PaymentDateValue) AS LastPaymentDate
                    FROM ValidPayments
                    GROUP BY AccountNo
                )
                SELECT
                    ci.ID AS AccountNo,
                    ci.ContractID AS ContractId,
                    ci.First_Name AS CustomerName,
                    dl.DealerId,
                    dl.CompanyName AS DealerName,
                    ci.StartDate,
                    ISNULL(ci.Deposit, 0) AS Deposit,
                    CAST(ci.Deposit + ci.Daily * 30 * ci.Term_in_Months
                         + ci.Weekly * (30.0 / 7.0) * ci.Term_in_Months
                         + ci.Monthly * ci.Term_in_Months AS DECIMAL(18,2)) AS ContractValue,
                    ISNULL(pt.TotalPaid, 0) AS TotalPaid,
                    CAST(CASE WHEN x.Arrears < 0 THEN -x.Arrears ELSE 0 END AS DECIMAL(18,2)) AS Shortfall,
                    pt.LastPaymentDate,
                    CAST(CASE WHEN d.Id IS NULL THEN 0 ELSE 1 END AS BIT) AS HasDevice,
                    NULLIF(LTRIM(RTRIM(ISNULL(d.Make, '') + ' ' + ISNULL(d.Model, ''))), '') AS DeviceName,
                    d.Locked,
                    d.NextLockDate,
                    d.LastConnectedAt
                FROM Contract_Info ci
                LEFT JOIN Devices d ON d.Id = ci.ID
                LEFT JOIN Dealers dl ON dl.DealerReference = d.DeviceGroupId
                LEFT JOIN PaymentTotals pt ON pt.AccountNo = ci.ID
                CROSS APPLY (
                    SELECT CASE
                        WHEN DATEDIFF(DAY, ci.StartDate, GETDATE()) < CAST(ci.Term_in_Months * 30 AS INT)
                            THEN DATEDIFF(DAY, ci.StartDate, GETDATE())
                        ELSE CAST(ci.Term_in_Months * 30 AS INT)
                    END AS Days
                ) DaysAccrued
                CROSS APPLY (
                    SELECT ISNULL(pt.TotalPaid, 0)
                        - (ci.Deposit
                           + ci.Daily * DaysAccrued.Days
                           + ci.Weekly * (DaysAccrued.Days / 7.0)
                           + ci.Monthly * (DaysAccrued.Days / 30.0)) AS Arrears
                ) x
                WHERE ci.StartDate IS NOT NULL AND ci.EndDate IS NULL";

            return (await _db.QueryAsync<FraudContractRow>(sql, commandTimeout: 90)).ToList();
        }

        // One row per WooCommerce order (the table can hold re-synced copies).
        public async Task<List<FraudOrderRow>> GetOrdersAsync()
        {
            const string sql = @"
                ;WITH Latest AS (
                    SELECT o.*, ROW_NUMBER() OVER (PARTITION BY o.OrderID ORDER BY o.Id DESC) AS rn
                    FROM Woo_Orders o
                )
                SELECT
                    o.OrderID AS OrderId,
                    o.Status,
                    o.DateCreated,
                    LTRIM(RTRIM(ISNULL(o.FirstName, '') + ' ' + ISNULL(o.LastName, ''))) AS CustomerName,
                    o.NationalId,
                    COALESCE(NULLIF(LTRIM(RTRIM(o.Phone)), ''), o.CustPhone) AS Phone,
                    o.IMEI AS Imei,
                    o.MpesaDepositRef,
                    o.DealerRef,
                    ci.ID AS AccountNo,
                    dl.DealerId,
                    dl.CompanyName AS DealerName
                FROM Latest o
                LEFT JOIN Contract_Info ci ON ci.ContractID = o.ContractId
                LEFT JOIN Devices d ON d.Id = ci.ID
                LEFT JOIN Dealers dl ON dl.DealerReference = d.DeviceGroupId
                WHERE o.rn = 1
                  AND ISNULL(o.Status, '') NOT IN ('cancelled', 'failed', 'checkout-draft', 'rejected', 'trash', 'refunded')";

            return (await _db.QueryAsync<FraudOrderRow>(sql, commandTimeout: 60)).ToList();
        }

        public async Task<List<FraudNextOfKinRow>> GetNextOfKinAsync()
        {
            const string sql = "SELECT OrderId, Phone, IdNumber FROM Woo_Orders_NextOfKin";
            return (await _db.QueryAsync<FraudNextOfKinRow>(sql, commandTimeout: 60)).ToList();
        }

        public async Task<List<FraudDeviceRow>> GetDevicesOnOpenContractsAsync()
        {
            const string sql = @"
                SELECT
                    d.Id AS AccountNo,
                    d.ImeiNo AS Imei,
                    d.ImeiNo2 AS Imei2,
                    ci.First_Name AS CustomerName,
                    lo.NationalId AS ContractOrderNationalId
                FROM Devices d
                INNER JOIN Contract_Info ci ON ci.ID = d.Id AND ci.EndDate IS NULL AND ci.StartDate IS NOT NULL
                OUTER APPLY (
                    SELECT TOP 1 x.NationalId FROM Woo_Orders x
                    WHERE x.ContractId = ci.ContractID
                    ORDER BY x.Id DESC
                ) lo";

            return (await _db.QueryAsync<FraudDeviceRow>(sql, commandTimeout: 60)).ToList();
        }

        // Empty until Database/Fraud/001 has been run, so the page still opens.
        public async Task<List<FraudReview>> GetReviewsAsync()
        {
            const string sql = @"
                IF OBJECT_ID('dbo.FraudFlagReviews') IS NOT NULL
                    SELECT r.CheckCode, r.SubjectKey, r.Status, r.Notes,
                           LTRIM(RTRIM(ISNULL(u.[Name], '') + ' ' + ISNULL(u.[LastName], ''))) AS ReviewedByName,
                           r.ReviewedAtUtc
                    FROM FraudFlagReviews r
                    LEFT JOIN Users u ON u.UserId = r.ReviewedByUserId
                ELSE
                    SELECT CAST(NULL AS VARCHAR(40)) AS CheckCode, CAST(NULL AS NVARCHAR(100)) AS SubjectKey,
                           CAST(NULL AS VARCHAR(20)) AS Status, CAST(NULL AS NVARCHAR(1000)) AS Notes,
                           CAST(NULL AS NVARCHAR(200)) AS ReviewedByName, CAST(NULL AS DATETIME2) AS ReviewedAtUtc
                    WHERE 1 = 0";

            return (await _db.QueryAsync<FraudReview>(sql)).ToList();
        }

        // Status Open removes the review, putting the flag back as new.
        public async Task SaveReviewAsync(string checkCode, string subjectKey, string status, string? notes, int userId)
        {
            const string sql = @"
                IF @Status = 'Open'
                    DELETE FROM FraudFlagReviews WHERE CheckCode = @CheckCode AND SubjectKey = @SubjectKey
                ELSE IF EXISTS (SELECT 1 FROM FraudFlagReviews WHERE CheckCode = @CheckCode AND SubjectKey = @SubjectKey)
                    UPDATE FraudFlagReviews
                    SET Status = @Status, Notes = @Notes, ReviewedByUserId = @UserId, ReviewedAtUtc = SYSUTCDATETIME()
                    WHERE CheckCode = @CheckCode AND SubjectKey = @SubjectKey
                ELSE
                    INSERT INTO FraudFlagReviews (CheckCode, SubjectKey, Status, Notes, ReviewedByUserId)
                    VALUES (@CheckCode, @SubjectKey, @Status, @Notes, @UserId)";

            await _db.ExecuteAsync(sql, new { CheckCode = checkCode, SubjectKey = subjectKey, Status = status, Notes = notes, UserId = userId });
        }
    }
}
