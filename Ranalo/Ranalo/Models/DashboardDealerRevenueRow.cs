namespace Ranalo.Models
{
    // One row per dealer -- produced by
    // IDashboardReportRepository.GetRevenueThisMonthByDealerAsync(). DealerName
    // is Dealers.CompanyName, the same source and spelling as
    // DashboardAccountDetailRow.DealerName, so the two can be joined directly
    // by name without a DealerId lookup.
    public class DashboardDealerRevenueRow
    {
        public string DealerName { get; set; } = "";
        public decimal RevenueThisMonth { get; set; }
    }
}
