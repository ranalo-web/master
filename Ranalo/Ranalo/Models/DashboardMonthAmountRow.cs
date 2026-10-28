namespace Ranalo.Models
{
    // One row per calendar month -- produced by
    // IDashboardReportRepository.GetRevenueByMonthAsync/GetCommissionsPaidByMonthAsync
    // for the Financials page's monthly comparison chart. Only months with
    // at least one matching payment get a row; callers fill any gaps.
    public class DashboardMonthAmountRow
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public decimal Total { get; set; }
    }

    // One contract's device cost by start date -- Financials' Cost of
    // devices (IDashboardReportRepository.GetDeviceCostsAsync).
    public class DashboardDeviceCostRow
    {
        public DateTime StartDate { get; set; }
        public decimal BuyingPrice { get; set; }
    }

    // One contract's loss-given-default inputs beyond the account row
    // (IDashboardReportRepository.GetContractLossCostsAsync).
    public class ContractLossCostRow
    {
        public long ContractId { get; set; }
        public decimal CommissionsPaid { get; set; }
        public decimal RepossessionCost { get; set; }
        public decimal ResaleValue { get; set; }
        public bool IsWrittenOff { get; set; }
    }
}
