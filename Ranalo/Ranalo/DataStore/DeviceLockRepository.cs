using System.Data;
using System.Data.SqlClient;
using Dapper;
using Ranalo.Services.DeviceLock;

namespace Ranalo.DataStore
{
    // Lookups for the enrolment checks (brand keywords, TACs, deposit
    // payments, order products). Tables: Database/DeviceLock/001.
    public interface IDeviceLockRepository
    {
        Task<List<BrandKeyword>> GetBrandKeywordsAsync();
        Task<string?> GetTacBrandAsync(string tac);

        // Records a TAC's brand the first time it's confirmed. Never
        // overwrites an existing TAC -- a conflict is returned so it can be
        // logged rather than silently changing what blocks enrolments.
        Task<string?> RecordTacAsync(string tac, string brand, string source, string? imei);

        Task<List<DepositPayment>> GetPaymentsByMpesaCodeAsync(string mpesaCode);
        Task<List<string>> GetOrderProductNamesAsync(long orderId);
        Task<decimal?> GetOrderDailySalePriceAsync(long orderId);
        Task<decimal?> GetOrderDepositAmountAsync(long orderId);
        Task<int?> GetDealerIdByReferenceAsync(string dealerReference);

        // IMEI of a phone still on this account (Devices.Id = account), if any.
        Task<string?> GetLiveDeviceImeiAsync(long accountId);
    }

    public class DeviceLockRepository : IDeviceLockRepository
    {
        private readonly IDbConnection _db;

        public DeviceLockRepository(IDbConnection db)
        {
            _db = db;
        }

        public async Task<List<BrandKeyword>> GetBrandKeywordsAsync()
        {
            var rows = await _db.QueryAsync<BrandKeyword>(
                "SELECT CAST(Keyword AS NVARCHAR(100)) AS Keyword, CAST(Brand AS NVARCHAR(50)) AS Brand FROM dbo.DeviceBrandKeywords");
            return rows.ToList();
        }

        public async Task<string?> GetTacBrandAsync(string tac)
        {
            return await _db.QueryFirstOrDefaultAsync<string?>(
                "SELECT Brand FROM dbo.DeviceTacCodes WHERE Tac = @Tac", new { Tac = tac });
        }

        public async Task<string?> RecordTacAsync(string tac, string brand, string source, string? imei)
        {
            const string sql = @"
                IF NOT EXISTS (SELECT 1 FROM dbo.DeviceTacCodes WHERE Tac = @Tac)
                    INSERT INTO dbo.DeviceTacCodes (Tac, Brand, Source, SourceImei)
                    VALUES (@Tac, @Brand, @Source, @Imei);

                SELECT Brand FROM dbo.DeviceTacCodes WHERE Tac = @Tac;";

            try
            {
                var stored = await _db.QueryFirstOrDefaultAsync<string>(sql,
                    new { Tac = tac, Brand = brand, Source = source, Imei = imei });
                return string.Equals(stored, brand, StringComparison.OrdinalIgnoreCase) ? null : stored;
            }
            catch (SqlException ex) when (ex.Number is 2627 or 2601)
            {
                // Inserted concurrently by another request -- fine.
                return null;
            }
        }

        public async Task<List<DepositPayment>> GetPaymentsByMpesaCodeAsync(string mpesaCode)
        {
            var rows = await _db.QueryAsync<DepositPayment>(@"
                -- Casts pin the column types to DepositPayment's constructor,
                -- which Dapper matches exactly.
                SELECT CAST(MpesaCode AS NVARCHAR(100)) AS MpesaCode,
                       CAST(AccountNo AS NVARCHAR(100)) AS AccountNo,
                       CAST(ISNULL(AmountValue, 0) AS DECIMAL(18,2)) AS Amount
                FROM dbo.KosePayments
                WHERE LTRIM(RTRIM(MpesaCode)) = @MpesaCode",
                new { MpesaCode = mpesaCode.Trim() });
            return rows.ToList();
        }

        // orderId is the WooCommerce order number (Woo_Orders.OrderID);
        // Woo_OrderProduct.OrderId holds Woo_Orders.Id, our own key. Matching
        // the order number against it found nothing, so no order had a brand
        // and every enrolment from 2026-10-07 to 10-15 was blocked.
        public async Task<List<string>> GetOrderProductNamesAsync(long orderId)
        {
            var rows = await _db.QueryAsync<string>(@"
                SELECT p.ProductName
                FROM dbo.Woo_OrderProduct p
                INNER JOIN dbo.Woo_Orders wo ON wo.Id = p.OrderId
                WHERE wo.OrderID = @OrderId AND p.ProductName IS NOT NULL",
                new { OrderId = orderId });
            return rows.ToList();
        }

        // The deposit the website quoted (_rc_deposit), if the order has one.
        public async Task<decimal?> GetOrderDepositAmountAsync(long orderId)
        {
            return await _db.QueryFirstOrDefaultAsync<decimal?>(
                "SELECT TOP 1 DepositAmount FROM dbo.Woo_Orders WHERE OrderID = @OrderId",
                new { OrderId = orderId });
        }

        public async Task<decimal?> GetOrderDailySalePriceAsync(long orderId)
        {
            return await _db.QueryFirstOrDefaultAsync<decimal?>(
                "SELECT TOP 1 DailySalePrice FROM dbo.Woo_Orders WHERE OrderID = @OrderId",
                new { OrderId = orderId });
        }

        public async Task<string?> GetLiveDeviceImeiAsync(long accountId)
        {
            return await _db.QueryFirstOrDefaultAsync<string?>(@"
                SELECT TOP 1 CAST(ImeiNo AS NVARCHAR(50))
                FROM dbo.Devices
                WHERE Id = @AccountId
                  AND ISNULL(Status, '') <> 'removed'
                  AND ISNULL(EnrollmentStatus, '') <> 'Removed'",
                new { AccountId = accountId });
        }

        public async Task<int?> GetDealerIdByReferenceAsync(string dealerReference)
        {
            return await _db.QueryFirstOrDefaultAsync<int?>(
                "SELECT TOP 1 DealerId FROM dbo.Dealers WHERE LTRIM(RTRIM(DealerReference)) = @Ref",
                new { Ref = dealerReference.Trim() });
        }
    }
}
