using Ranalo.Models;
using Ranalo.Models.Reports;

namespace Ranalo.DataStore
{
    public interface ICommissionsRepository
    {
        Task<PagedResult<AgentsTotalSummaryReport>> AgentsTotalSummary(CommissionsFilter filter);
        Task<PagedResult<DealerCommissionReadyToPayReport>> DealerCommissionsReadyToPay(CommissionsFilter filter);
        Task<PagedResult<MainCommissionsSummaryReport>> FullCommissionsReport(CommissionsFilter filter);
        Task<PagedResult<OutstandingDealerCommissionReport>> OutstandingDealerCommissions(CommissionsFilter filter);
    }
}