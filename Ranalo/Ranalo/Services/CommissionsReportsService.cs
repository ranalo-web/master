using Ranalo.DataStore;
using Ranalo.Models.Reports;
using Ranalo.Models;

namespace Ranalo.Services
{
    public class CommissionsReportsService : ICommissionsReportsService
    {
        private readonly ICommissionsRepository _commissionsRepo;

        public CommissionsReportsService(ICommissionsRepository commissionsRepo)
        {
            _commissionsRepo = commissionsRepo;
        }

        public async Task<PagedResult<MainCommissionsSummaryReport>> FullCommissionsReportAsync(
           CommissionsFilter filter)
        {
            return await _commissionsRepo.FullCommissionsReport(filter);
        }

        public async Task<PagedResult<OutstandingDealerCommissionReport>>
        OutstandingDealerCommissionsAsync(
        CommissionsFilter filter)
        {
            return await _commissionsRepo.OutstandingDealerCommissions(filter);
        }

        public async Task<PagedResult<DealerCommissionReadyToPayReport>>
        DealerCommissionsReadyToPayAsync(
         CommissionsFilter filter)
        {
            return await _commissionsRepo.DealerCommissionsReadyToPay(filter);
        }

        public async Task<PagedResult<AgentsTotalSummaryReport>>
    AgentsTotalSummaryAsync(
        CommissionsFilter filter)
        {
            return await _commissionsRepo.AgentsTotalSummary(filter);
        }
    }
}
