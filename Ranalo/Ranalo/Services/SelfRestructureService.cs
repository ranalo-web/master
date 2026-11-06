using Ranalo.DataStore;
using Ranalo.Models;

namespace Ranalo.Services
{
    public interface ISelfRestructureService
    {
        Task<SelfRestructureRules.Facts> FactsAsync(MyAccountView view);
        Task<(bool Ok, string Message)> ChooseAsync(MyAccountView view, string? optionName);
        // Starts or expires pending plans: one account (the app, so it's
        // instant once paid) or all of them (the scheduled job).
        Task<int> ProcessPendingAsync(long? accountNo = null);
    }

    // Self-service restructure (SelfRestructureRules): every rule is checked
    // again at each step -- when the options are shown, when one is chosen,
    // and when the first payment starts it.
    public class SelfRestructureService : ISelfRestructureService
    {
        private readonly ISelfRestructureRepository _plans;
        private readonly ILostStolenRepository _lostStolen;
        private readonly IAccountRefreshQueue _refresh;
        private readonly IPaymentReminderService? _messages;
        private readonly ICustomerAccessRepository? _access;
        private readonly IPaymentsRepository? _paymentsRepository;
        private readonly Ranalo.SumsungKnox.IKnoxGuardClient? _knox;
        private readonly ILogger<SelfRestructureService> _logger;

        public SelfRestructureService(ISelfRestructureRepository plans, ILostStolenRepository lostStolen,
            IAccountRefreshQueue refresh, ILogger<SelfRestructureService> logger,
            IPaymentReminderService? messages = null, ICustomerAccessRepository? access = null,
            IPaymentsRepository? paymentsRepository = null, Ranalo.SumsungKnox.IKnoxGuardClient? knox = null)
        {
            _plans = plans;
            _lostStolen = lostStolen;
            _refresh = refresh;
            _messages = messages;
            _access = access;
            _paymentsRepository = paymentsRepository;
            _knox = knox;
            _logger = logger;
        }

        public async Task<SelfRestructureRules.Facts> FactsAsync(MyAccountView view)
        {
            var contractId = await _plans.GetOpenContractIdAsync(view.AccountNo);
            return new SelfRestructureRules.Facts
            {
                HasOpenContract = contractId != null,
                FullyPaid = view.FullyPaid,
                Balance = view.Balance,
                Behind = view.Behind,
                DailyAll = view.ContractDaily,
                UsedBefore = contractId != null && await _plans.HasUsedAsync(contractId.Value),
                OnPlan = view.RestructuredOn.HasValue,
                LostOrStolen = (await _lostStolen.GetLockedAccountNosAsync()).Contains(view.AccountNo),
                InRecovery = await _plans.InRecoveryAsync(view.AccountNo),
            };
        }

        public async Task<(bool Ok, string Message)> ChooseAsync(MyAccountView view, string? optionName)
        {
            var facts = await FactsAsync(view);
            var refusal = SelfRestructureRules.Refusal(facts);
            if (refusal != null) return (false, refusal);

            var option = SelfRestructureRules.Find(facts, optionName);
            if (option == null) return (false, "Choose one of the plans shown.");

            var contractId = await _plans.GetOpenContractIdAsync(view.AccountNo);
            var now = DateTime.UtcNow;
            var added = await _plans.AddPendingAsync(new SelfRestructure
            {
                AccountNo = view.AccountNo,
                ContractId = contractId!.Value,
                OptionName = option.Name,
                Daily = option.Daily,
                BalanceAtChoice = view.Balance,
                BehindAtChoice = view.Behind,
                ChosenAtUtc = now,
                ExpiresAtUtc = now.Add(SelfRestructureRules.PayWithin),
            });
            if (!added) return (false, "You already have a plan waiting for its first payment, or you've used your plan on this contract.");

            _logger.LogInformation("Self-service plan chosen: {Account} {Option} Ksh {Daily}/day", view.AccountNo, option.Name, option.Daily);
            return (true, $"Plan chosen. Pay Ksh {CustomerMessages.Money(option.Daily)} within 24 hours to start it.");
        }

        public async Task<int> ProcessPendingAsync(long? accountNo = null)
        {
            var started = 0;
            var now = DateTime.UtcNow;
            var lostStolen = await _lostStolen.GetLockedAccountNosAsync();

            foreach (var plan in (await _plans.GetPendingAsync()).Where(p => accountNo == null || p.AccountNo == accountNo))
            {
                try
                {
                    // Only payments after choosing and within the 24 hours count.
                    var until = now < plan.ExpiresAtUtc ? now : plan.ExpiresAtUtc;
                    var paid = await _plans.PaidBetweenAsync(plan.AccountNo, plan.ChosenAtUtc.AddHours(3), until.AddHours(3));

                    switch (SelfRestructureRules.Decide(plan, paid, now))
                    {
                        case SelfRestructureRules.PendingOutcome.Expire:
                            await _plans.SetStatusAsync(plan.Id, SelfRestructureStatus.Pending, SelfRestructureStatus.Expired, "First payment not made within 24 hours.");
                            break;

                        case SelfRestructureRules.PendingOutcome.Start:
                            // Still the same open contract, and the phone not reported lost or stolen.
                            // Still the same open contract, not reported lost or stolen, and
                            // no plan agreed with staff since they chose (never overwritten).
                            if (await _plans.GetOpenContractIdAsync(plan.AccountNo) != plan.ContractId || lostStolen.Contains(plan.AccountNo)
                                || await _plans.HasPlanAsync(plan.AccountNo))
                            {
                                await _plans.SetStatusAsync(plan.Id, SelfRestructureStatus.Pending, SelfRestructureStatus.Cancelled, "No longer allowed when the payment came.");
                                break;
                            }
                            // Agreed from the moment they chose (Kenya time, as staff plans and payments are).
                            if (await _plans.StartAsync(plan, plan.ChosenAtUtc.AddHours(3)))
                            {
                                started++;
                                _logger.LogInformation("Self-service plan started: {Account} Ksh {Daily}/day", plan.AccountNo, plan.Daily);
                                _refresh.Enqueue(plan.AccountNo.ToString());
                                await TellThePhoneAsync(plan);
                            }
                            break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Self-service plan {Id} for {Account} not processed; tried again next time.", plan.Id, plan.AccountNo);
                }
            }
            return started;
        }

        // "Your payment plan has started", to the phone through its own
        // provider; logged like every message, so it's in the app's Inbox.
        // The plan has started whatever happens here.
        private async Task TellThePhoneAsync(SelfRestructure plan)
        {
            if (_messages == null || _access == null || _paymentsRepository == null || _knox == null) return;
            try
            {
                var target = await _access.GetTargetAsync(plan.AccountNo);
                if (target == null) return;
                var message = CustomerMessages.PlanStarted(target.FirstName, plan.AccountNo, plan.Daily);
                await _messages.SendToEachProviderAsync(new List<AccountSendMessage>
                {
                    new()
                    {
                        AccountId = plan.AccountNo, FirstName = target.FirstName, Imei = target.Imei, LockGroup = target.LockGroup,
                        MessageTitle = message.Title, MessageText = message.Content,
                    },
                }, PlanStartedType, _paymentsRepository, _knox);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Plan started on {Account}, but the message to the phone failed.", plan.AccountNo);
            }
        }

        public const string PlanStartedType = "PlanStarted";
    }
}
