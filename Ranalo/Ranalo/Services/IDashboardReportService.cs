using Ranalo.Models;

namespace Ranalo.Services
{
    public interface IDashboardReportService
    {
        Task<AdminDashboardViewModel> GetAdminDashboardAsync();

        Task<DealerDashboardViewModel> GetDealerDashboardAsync(int dealerId);
    }
}
