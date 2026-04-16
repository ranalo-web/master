using Ranalo.Models;
using Ranalo.SumsungKnox.Models;

namespace Ranalo.Services
{
    public interface IEnrolmentService
    {
        Task<Enrolment> CreateEnrolmentasync(Enrolment newEnrolment, CustomerDetails? order);

        Task<(IEnumerable<Enrolment> Items, int TotalCount)>
        GetAllEnrolmentsAsync(int pageNumber, int pageSize);

        Task<(IEnumerable<Enrolment> Items, int TotalCount)>
        GetDealerEnrolmentsAsync(int dealerId, int pageNumber, int pageSize);

        Task<Enrolment?> GetByImeiNumberAsync(string imei);
        Task ApproveEnrolment(Enrolment existingEnrolment);
        Task<Enrolment> GetByEnrolmentIdNumberAsync(Guid enrolmentId);
        Task DeleteNewEnrolmentEnrolment(Enrolment existingEnrolment);

        Task CreateDeviceFromKnox(Enrolment newEnrolment);
        Task LockDevicesKnox(List<LockTransaction> devicesToLockKnox);

        Task<Enrolment> StartEnrolmentasync(Enrolment newEnrolment, CustomerDetails? order);

        Task<Enrolment> UpdateEnrolmentasync(Enrolment newEnrolment);

        Task<ListDevicesResponse> DoFilterDevicesFromKnox(string imei);
        Task SendReminderMessage(string imei);
    }
}