using Ranalo.Models;

namespace Ranalo.Services
{
    public interface IDashboardReportService
    {
        Task<AdminDashboardViewModel> GetAdminDashboardAsync();

        // agentUserId narrows every account-level query down to just that
        // agent's own book (Contract_Info.AssignedAgentId) instead of the
        // whole dealer -- used to render this same dashboard/report set for
        // an Agent's own view. Null (the default) preserves the original
        // dealer-wide behavior. Device Stock and Completed Contracts stay
        // dealer-wide either way (rollup-backed, not yet agent-scopable);
        // Dealer Commissions also stays dealer-wide (it isn't an agent figure).
        Task<DealerDashboardViewModel> GetDealerDashboardAsync(int dealerId, int? agentUserId = null);

        // Backs the Dealer Dashboard's Revenue card date-range filter.
        // `period` is one of "week", "month", "ytd", "year" (see
        // DashboardReportService.TryResolvePeriodWindow); returns null for an
        // unrecognized value so the controller can 400 instead of silently
        // defaulting.
        Task<DealerRevenuePeriodResult?> GetDealerRevenueForPeriodAsync(int dealerId, string period, int? agentUserId = null);

        // Individually-callable versions of the sections GetDealerDashboardAsync
        // bundles onto DealerDashboardViewModel, for the dedicated report pages
        // linked from the Dealer Dashboard's summary cards (each fetches only
        // what its own page needs, not the whole dashboard). All six of the
        // account-detail-backed methods classify accounts through the exact
        // same rule as the top KPI cards -- see
        // IDashboardReportRepository.GetDealerAccountDetailsAsync.
        Task<List<DealerWatchlistEntry>> GetDealerNonPayersAsync(int dealerId, int? agentUserId = null);
        Task<List<DealerWatchlistEntry>> GetDealerSlowPayersAsync(int dealerId, int? agentUserId = null);
        Task<List<DealerWatchlistEntry>> GetDealerGoodPayersAsync(int dealerId, int? agentUserId = null);
        Task<List<DealerAgentPerformance>> GetDealerAgentPerformanceAsync(int dealerId, int? agentUserId = null);
        Task<List<DealerContract>> GetDealerContractsAsync(int dealerId, int? agentUserId = null);
        Task<List<DealerContract>> GetDealerContractsEndingSoonAsync(int dealerId, int? agentUserId = null);
        Task<List<DealerDeviceStock>> GetDealerDeviceStockReportAsync(int dealerId);
        Task<List<DealerCompletedContract>> GetDealerCompletedContractsReportAsync(int dealerId);
    }
}
