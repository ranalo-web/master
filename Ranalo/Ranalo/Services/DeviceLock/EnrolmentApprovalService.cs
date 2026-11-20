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
        // Transsion phones still waiting to be switched on: asks Transsion
        // (for when its activation webhook never arrived). Returns how many were active.
        Task<int> CheckActivationsAsync();
        // The Enrolments page's note by each approve button (DeviceLockRules.ApprovalNote).
        Task<Dictionary<Guid, string>> ApprovalNotesAsync(IReadOnlyCollection<Enrolment> enrolments);
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
        private readonly IDashboardReportRepository _reports;
        private readonly ILogger<EnrolmentApprovalService> _logger;

        public EnrolmentApprovalService(IEnrolmentRepository enrolments, IEnrolmentService enrolmentService,
            IStockPhoneService stock, ITranssionEnrolmentWorkflow transsion, IOrderContractService contracts,
            ICustomerScreeningRepository screening, IDashboardReportRepository reports, ILogger<EnrolmentApprovalService> logger)
        {
            _reports = reports;
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
                await _contracts.TryCreateAsync(updated.OrderId, updated.AccountId);
            }

            return result;
        }

        // null = not one to approve automatically (or not yet).
        public async Task<WorkflowResult?> TryAutoApproveAsync(Enrolment enrolment)
        {
            if (!DeviceLockRules.AutoApproves(enrolment, await ScreeningPassedAsync(enrolment.OrderId), DateTime.Now,
                    await SellerDefaultRateAsync(enrolment)))
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
                enrolment.Created.Add(DeviceLockRules.KnoxApprovalDelay), await SellerDefaultRateAsync(enrolment));

        public async Task<int> SweepAsync()
        {
            _dealerAccounts.Clear();
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

        public async Task<int> CheckActivationsAsync()
        {
            var activated = 0;
            foreach (var enrolment in (await _enrolments.GetAwaitingActivationAsync()).Where(DeviceLockRules.AwaitingActivation))
            {
                try
                {
                    if (await _transsion.CheckActivationAsync(enrolment))
                    {
                        activated++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Transsion activation check of {Imei} failed", enrolment.IMEI);
                }
            }
            return activated;
        }

        public async Task<Dictionary<Guid, string>> ApprovalNotesAsync(IReadOnlyCollection<Enrolment> enrolments)
        {
            var orderIds = enrolments.Where(e => e.OrderId > 0).Select(e => (long)e.OrderId).Distinct().ToList();
            var calls = (await _screening.GetScreeningCallsForOrdersAsync(orderIds)).ToLookup(c => c.OrderId);
            var notes = new Dictionary<Guid, string>();
            foreach (var e in enrolments)
            {
                var passed = ScreeningRules.Status(true, calls[e.OrderId].ToList()) == ScreeningStatus.Passed;
                var note = DeviceLockRules.ApprovalNote(e, passed, DateTime.Now, await SellerDefaultRateAsync(e));
                if (note != null)
                {
                    notes[e.Id] = note;
                }
            }
            return notes;
        }

        // The dealer's commission accounts (from the 3-a-day commission
        // snapshot), kept for this request / sweep.
        private readonly Dictionary<int, List<CommissionAccount>> _dealerAccounts = new();

        // The worse of the dealer's and (an agent's sale) the agent's default
        // rate, as on the commission pages. null = couldn't be worked out.
        private async Task<decimal?> SellerDefaultRateAsync(Enrolment enrolment)
        {
            try
            {
                if (!_dealerAccounts.TryGetValue(enrolment.DealerId, out var accounts))
                {
                    accounts = await _reports.GetCommissionAccountsAsync(enrolment.DealerId);
                    _dealerAccounts[enrolment.DealerId] = accounts;
                }
                return SellerDefaultRate(enrolment, accounts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Couldn't work out the default rate for enrolment {Imei}", enrolment.IMEI);
                return null;
            }
        }

        public static decimal SellerDefaultRate(Enrolment enrolment, IReadOnlyCollection<CommissionAccount> dealerAccounts)
        {
            var rate = CommissionPayees.Summarize(CommissionPayeeType.Dealer, enrolment.DealerId, dealerAccounts).DefaultRatePct;
            if (DeviceLockRules.EnrolledByAgent(enrolment) && enrolment.EnrolledByUserId is { } agentId)
            {
                var agentAccounts = dealerAccounts.Where(a => a.AgentId == agentId).ToList();
                rate = Math.Max(rate, CommissionPayees.Summarize(CommissionPayeeType.Agent, agentId, agentAccounts).DefaultRatePct);
            }
            return rate;
        }

        private async Task<bool> ScreeningPassedAsync(long orderId) =>
            ScreeningRules.Status(true, await _screening.GetScreeningCallsAsync(orderId)) == ScreeningStatus.Passed;
    }
}
