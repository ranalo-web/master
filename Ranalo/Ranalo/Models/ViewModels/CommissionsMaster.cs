using Ranalo.Models.Reports;

namespace Ranalo.Models.ViewModels
{
    public class CommissionsMaster
    {
        public PagedResult<MainCommissionsSummaryReport> FullCommissions { get; set; }
        public PagedResult<OutstandingDealerCommissionReport> OutstandingDealerCommissions { get; set; }
        public PagedResult<DealerCommissionReadyToPayReport> DealerReadyToPayCommissions { get; set; }
        public PagedResult<AgentsTotalSummaryReport> AgentTotalsCommissions { get; set; }
    }
}
