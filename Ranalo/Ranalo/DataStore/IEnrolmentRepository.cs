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
        GetDealerEnrolmentsAsync(int dealerId, int pageNumber, int pageSize);

        Task<(IEnumerable<Enrolment> Items, int TotalCount)>
        GetAllEnrolmentsAsync(int pageNumber, int pageSize);

        Task<bool> DeleteEnrolmentAsync(Enrolment enrolment);
    }
}