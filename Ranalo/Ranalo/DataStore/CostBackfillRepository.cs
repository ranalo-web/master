using System.Data;
using Dapper;
using Ranalo.Models;

namespace Ranalo.DataStore
{
    public interface ICostBackfillRepository
    {
        Task<List<CostBackfillContractRow>> GetContractsMissingCostAsync();
        Task<List<CostBackfillKnownCost>> GetKnownCostsAsync();
        Task<List<CostBackfillDeviceRow>> GetDevicesWithoutContractsAsync();
        Task<int> ApplyBuyingPricesAsync(IEnumerable<(int ContractId, decimal BuyingPrice)> prices);
    }

    // One-off device-cost backfill for Contract_Info.BuyingPrice. A contract
    // links to what was sold through Woo_Orders.ContractId ->
    // Woo_OrderProduct (OrderId = Woo_Orders.Id); contracts with no order
    // (e.g. recovered accounts) fall back to the device's make/model.
    public class CostBackfillRepository : ICostBackfillRepository
    {
        private readonly IDbConnection _db;

        public CostBackfillRepository(IDbConnection db)
        {
            _db = db;
        }

        private const string ContractProductSql = @"
                SELECT
                    ci.ContractID AS ContractId,
                    CAST(ci.ID AS BIGINT) AS AccountNo,
                    ci.First_Name AS CustomerName,
                    ci.StartDate,
                    ci.EndDate,
                    ci.BuyingPrice,
                    d.Make AS DeviceMake,
                    d.Model AS DeviceModel,
                    wop.ProductId,
                    wop.ProductName,
                    wop.ProductRam,
                    wop.ProductStorage,
                    wop.BuyingPrice AS OrderCost
                FROM Contract_Info ci
                LEFT JOIN Devices d ON d.Id = ci.ID
                OUTER APPLY (
                    SELECT TOP 1 p.ProductId, p.ProductName, p.ProductRam, p.ProductStorage, p.BuyingPrice
                    FROM Woo_Orders wo
                    INNER JOIN Woo_OrderProduct p ON p.OrderId = wo.Id
                    WHERE wo.ContractId = ci.ContractID
                    ORDER BY p.Id
                ) wop";

        public async Task<List<CostBackfillContractRow>> GetContractsMissingCostAsync()
        {
            var rows = await _db.QueryAsync<CostBackfillContractRow>(
                ContractProductSql + " WHERE ci.BuyingPrice IS NULL ORDER BY ci.StartDate DESC, ci.ContractID DESC");
            return rows.ToList();
        }

        public async Task<List<CostBackfillKnownCost>> GetKnownCostsAsync()
        {
            var rows = await _db.QueryAsync<CostBackfillKnownCost>(
                ContractProductSql + " WHERE ci.BuyingPrice IS NOT NULL");
            return rows.ToList();
        }

        public async Task<List<CostBackfillDeviceRow>> GetDevicesWithoutContractsAsync()
        {
            const string sql = @"
                SELECT
                    d.Id AS DeviceId,
                    d.Name AS DeviceName,
                    d.Make,
                    d.Model,
                    d.CustomerName,
                    dl.CompanyName AS DealerName,
                    d.CreatedAt
                FROM Devices d
                LEFT JOIN Dealers dl ON dl.DealerReference = d.DeviceGroupId
                WHERE NOT EXISTS (SELECT 1 FROM Contract_Info ci WHERE ci.ID = d.Id)
                ORDER BY d.CreatedAt DESC";

            var rows = await _db.QueryAsync<CostBackfillDeviceRow>(sql);
            return rows.ToList();
        }

        // Only fills contracts that still have no BuyingPrice -- a cost
        // entered by hand (or by someone else meanwhile) is never overwritten.
        public async Task<int> ApplyBuyingPricesAsync(IEnumerable<(int ContractId, decimal BuyingPrice)> prices)
        {
            const string sql = @"
                UPDATE Contract_Info
                SET BuyingPrice = @BuyingPrice
                WHERE ContractID = @ContractId AND BuyingPrice IS NULL";

            var updated = 0;
            foreach (var (contractId, buyingPrice) in prices)
            {
                updated += await _db.ExecuteAsync(sql, new { ContractId = contractId, BuyingPrice = buyingPrice });
            }
            return updated;
        }
    }
}
