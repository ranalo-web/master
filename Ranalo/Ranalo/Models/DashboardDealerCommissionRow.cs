namespace Ranalo.Models
{
    // One row per dealer -- produced by
    // IDashboardReportRepository.GetDealerCommissionPaidThisMonthByDealerAsync().
    // DealerName is Dealers.CompanyName, same source/spelling as
    // DashboardAccountDetailRow.DealerName and DashboardDealerRevenueRow, so
    // it can be joined directly by name.
    public class DashboardDealerCommissionRow
    {
        public string DealerName { get; set; } = "";
        public decimal CommissionPaidThisMonth { get; set; }
    }
}
