using Ranalo.Models;

namespace Ranalo.DataStore
{
    public interface IEnrolmentRepository
    {
        Task<Enrolment> CreateEnrolmentAsync(Enrolment newEnrolment);
        Task<IEnumerable<Enrolment>> GetAllEnrolmentsAsync();
        Task<Enrolment?> GetByAccountIdAsync(long accountId);
        Task<Enrolment> GetByEnrolmentIdAsync(Guid enrolmentId);
        Task<Enrolment?> GetByImeiNumberAsync(string imei);
        Task<IEnumerable<Enrolment>> GetEnrolmentsByDealerIdAsync(int dealerId);
        Task SaveAsync();
        Task<Enrolment> UpdateEnrolmentAsync(Enrolment updateEnrolment);
        Task<Enrolment> UpdateEnrolmentPasswordAsync(int userId, string newPasswordHash);
        Task<(IEnumerable<Enrolment> Items, int TotalCount)>
        GetDealerEnrolmentsAsync(int dealerId, int pageNumber, int pageSize, string? searchTerm = null, int? enrolledByUserId = null);

        Task<(IEnumerable<Enrolment> Items, int TotalCount)>
        GetAllEnrolmentsAsync(int pageNumber, int pageSize, string? searchTerm = null);

        Task<bool> DeleteEnrolmentAsync(Enrolment enrolment);

        Task<List<Enrolment>> GetStockAsync(bool unverifiedOnly, string? searchTerm = null);
        // Not approved yet and could be approved automatically (DeviceLockRules.AutoApproves).
        Task<List<Enrolment>> GetAwaitingApprovalAsync();
        // Pre-enrolled in the last 14 days, still waiting to be switched on
        // (DeviceLockRules.AwaitingActivation narrows it to Transsion).
        Task<List<Enrolment>> GetAwaitingActivationAsync();
    }
}