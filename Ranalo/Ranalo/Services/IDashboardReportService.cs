using Ranalo.Models;

namespace Ranalo.Services
{
    public interface IDashboardReportService
    {
        Task<AdminDashboardViewModel> GetAdminDashboardAsync();

        Task<DealerDashboardViewModel> GetDealerDashboardAsync(int dealerId);

        // Backs the Dealer Dashboard's Revenue card date-range filter.
        // `period` is one of "week", "month", "ytd", "year" (see
        // DashboardReportService.TryResolvePeriodWindow); returns null for an
        // unrecognized value so the controller can 400 instead of silently
        // defaulting.
        Task<DealerRevenuePeriodResult?> GetDealerRevenueForPeriodAsync(int dealerId, string period);
    }
}
