using Ranalo.Models;
using Ranalo.Models.Reports;

namespace Ranalo.Services
{
    public interface ICommissionsReportsService
    {
        Task<PagedResult<AgentsTotalSummaryReport>> AgentsTotalSummaryAsync(CommissionsFilter filter);
        Task<PagedResult<DealerCommissionReadyToPayReport>> DealerCommissionsReadyToPayAsync(CommissionsFilter filter);
        Task<PagedResult<MainCommissionsSummaryReport>> FullCommissionsReportAsync(CommissionsFilter filter);
        Task<PagedResult<OutstandingDealerCommissionReport>> OutstandingDealerCommissionsAsync(CommissionsFilter filter);
    }
}