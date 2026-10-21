using Ranalo.DataStore;
using Ranalo.DataStore.DataModels;
using Ranalo.Models;

namespace Ranalo.Services.DeviceLock
{
    // Approving an enrolment: by an approver/admin on the Enrolments page, or
    // automatically once the order passed screening (DeviceLockRules.AutoApproves):
    // straight after enrolling, then every 5 minutes (ScheduledEnrolmentAutoApproval)
    // while a Knox phone works its way through Veritech.
    public interface IEnrolmentApprovalService
    {
        Task<WorkflowResult> ApproveKnoxAsync(Enrolment enrolment, User actor);
        Task<WorkflowResult?> TryAutoApproveAsync(Enrolment enrolment);
        Task<bool> WillAutoApproveAsync(Enrolment enrolment);
        Task<int> SweepAsync();
    }

    public class EnrolmentApprovalService : IEnrolmentApprovalService
    {
        // Who approved, when nobody did. UserId 0 = the system.
        public static readonly User Automatic = new() { UserId = 0, RoleId = UserRole.Admin, Name = "Auto (screened)" };

        private readonly IEnrolmentRepository _enrolments;
        private readonly IEnrolmentService _enrolmentService;
        private readonly IStockPhoneService _stock;
        private readonly ITranssionEnrolmentWorkflow _transsion;
        private readonly IOrderContractService _contracts;
        private readonly ICustomerScreeningRepository _screening;
        private readonly ILogger<EnrolmentApprovalService> _logger;

        public EnrolmentApprovalService(IEnrolmentRepository enrolments, IEnrolmentService enrolmentService,
            IStockPhoneService stock, ITranssionEnrolmentWorkflow transsion, IOrderContractService contracts,
            ICustomerScreeningRepository screening, ILogger<EnrolmentApprovalService> logger)
        {
            _enrolments = enrolments;
            _enrolmentService = enrolmentService;
            _stock = stock;
            _transsion = transsion;
            _contracts = contracts;
            _screening = screening;
            _logger = logger;
        }

        // Knox: accept the upload in Knox (or, for a sold stock phone, send
        // its first unlock), then the device row and the contract.
        public async Task<WorkflowResult> ApproveKnoxAsync(Enrolment enrolment, User actor)
        {
            var decision = DeviceLockRules.CanApproveEnrolment(actor.RoleId, actor.UserId, enrolment);
            if (!decision.Allowed)
            {
                return new WorkflowResult(false, decision.Reason!);
            }

            WorkflowResult result;
            if (enrolment.Status == EnrolmentStatus.Pending && enrolment.StockKnoxApprovedAt.HasValue)
            {
                result = await _stock.UnlockSoldKnoxAsync(enrolment, actor);
            }
            else if (enrolment.Status != EnrolmentStatus.Pending)
            {
                result = new WorkflowResult(false, "This enrolment isn't waiting for Knox approval.");
            }
            else
            {
                enrolment.CallApprovedByUserId = actor.UserId;
                enrolment.CallApprovedAt = DateTime.UtcNow;
                result = await _enrolmentService.ApproveEnrolment(enrolment)
                    ? new WorkflowResult(true, $"Phone {enrolment.IMEI} approved in Knox.")
                    : new WorkflowResult(false, $"Knox hasn't accepted phone {enrolment.IMEI} yet: {enrolment.KnoxResponse}. "
                        + "It usually reaches Knox a few minutes after enrolling -- try Approve again shortly.");
            }

            var updated = await _enrolmentService.GetByImeiNumberAsync(enrolment.IMEI);
            if (updated != null && updated.Status == EnrolmentStatus.Approved)
            {
                await _enrolmentService.CreateDeviceFromKnox(updated);
                await _contracts.TryCreateAsync(updated.OrderId);
            }

            return result;
        }

        // null = not one to approve automatically (or not yet).
        public async Task<WorkflowResult?> TryAutoApproveAsync(Enrolment enrolment)
        {
            if (!DeviceLockRules.AutoApproves(enrolment, await ScreeningPassedAsync(enrolment.OrderId), DateTime.Now))
            {
                return null;
            }

            var result = DeviceLockRules.IsTranssion(enrolment.DeviceBrand)
                ? await _transsion.ApproveCallAsync(enrolment.Id, Automatic)
                : await ApproveKnoxAsync(enrolment, Automatic);
            _logger.LogInformation("Automatic approval of {Imei} (order {OrderId}): {Success} {Message}",
                enrolment.IMEI, enrolment.OrderId, result.Success, result.Message);
            return result;
        }

        // Approved automatically, now or (Knox) once it's had time to reach Knox.
        public async Task<bool> WillAutoApproveAsync(Enrolment enrolment) =>
            DeviceLockRules.AutoApproves(enrolment, await ScreeningPassedAsync(enrolment.OrderId),
                enrolment.Created.Add(DeviceLockRules.KnoxApprovalDelay));

        public async Task<int> SweepAsync()
        {
            var approved = 0;
            foreach (var enrolment in await _enrolments.GetAwaitingApprovalAsync())
            {
                try
                {
                    if ((await TryAutoApproveAsync(enrolment))?.Success == true)
                    {
                        approved++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Automatic approval of {Imei} failed", enrolment.IMEI);
                }
            }
            return approved;
        }

        private async Task<bool> ScreeningPassedAsync(long orderId) =>
            ScreeningRules.Status(true, await _screening.GetScreeningCallsAsync(orderId)) == ScreeningStatus.Passed;
    }
}
