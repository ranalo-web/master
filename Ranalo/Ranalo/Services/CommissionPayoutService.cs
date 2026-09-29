using Ranalo.DataStore;
using Ranalo.Models;

namespace Ranalo.Services
{
    public interface ICommissionPayoutService
    {
        Task<CommissionPayeesViewModel> GetPayeesAsync(string? payeeTypeFilter, string? search = null);
        Task<CommissionPayViewModel?> GetPayAsync(string payeeType, int payeeId);
        Task<(bool Ok, string Message)> RecordPayoutAsync(
            string payeeType, int payeeId, IReadOnlyCollection<long> contractIds, decimal amount, bool recordPastPayment,
            DateTime paidDate, string method, string? reference, string? notes, int recordedByUserId);
        Task<List<CommissionPayoutRecord>> GetPayoutsAsync(string? payeeType = null, int? dealerId = null, int? agentUserId = null, int? top = null);
        Task<bool> ConfirmReceiptAsync(int payoutId, string payeeType, int payeeId, int confirmedByUserId);
    }

    // Pay Commissions: works out each dealer's and agent's position from the
    // same per-account figures as every other commission page
    // (CommissionCalculator), applies CommissionPayoutRules (suspension,
    // payable, allocation) and records payouts.
    public class CommissionPayoutService : ICommissionPayoutService
    {
        private readonly IDashboardReportRepository _reportRepository;
        private readonly ICommissionPayoutRepository _payoutRepository;
        private readonly ILogger<CommissionPayoutService> _logger;

        public CommissionPayoutService(IDashboardReportRepository reportRepository, ICommissionPayoutRepository payoutRepository, ILogger<CommissionPayoutService> logger)
        {
            _reportRepository = reportRepository;
            _payoutRepository = payoutRepository;
            _logger = logger;
        }

        public async Task<CommissionPayeesViewModel> GetPayeesAsync(string? payeeTypeFilter, string? search = null)
        {
            var type = CommissionPayeeType.Normalize(payeeTypeFilter);
            var accounts = await _reportRepository.GetCommissionAccountsAsync(null);

            var groups = new List<(string Type, int Id, List<CommissionAccount> Accounts)>();
            if (type != CommissionPayeeType.Dealer)
            {
                groups.AddRange(accounts
                    .Where(a => a.AgentId.HasValue)
                    .GroupBy(a => a.AgentId!.Value)
                    .Select(g => (CommissionPayeeType.Agent, g.Key, g.ToList())));
            }
            if (type != CommissionPayeeType.Agent)
            {
                groups.AddRange(accounts
                    .GroupBy(a => a.DealerId)
                    .Select(g => (CommissionPayeeType.Dealer, g.Key, g.ToList())));
            }

            // Search: a payee matches on their own name (the whole payee is
            // shown) or on any of their accounts -- account number, contract
            // id or customer name -- in which case those accounts are listed
            // so the Pay screen can open with them ticked.
            var term = search?.Trim();
            var payees = new List<CommissionPayeeSummary>();
            foreach (var (payeeType, id, payeeAccounts) in groups)
            {
                var summary = Summarize(payeeType, id, payeeAccounts);
                if (!string.IsNullOrEmpty(term))
                {
                    var nameMatch = Contains(summary.Name, term) || Contains(summary.DealerName, term);
                    summary.MatchedAccounts = payeeAccounts
                        .Where(a => a.AccountId.ToString() == term || a.ContractId == term || Contains(a.CustomerName, term))
                        .Select(a => new CommissionMatchedAccount
                        {
                            AccountId = a.AccountId,
                            ContractId = long.TryParse(a.ContractId, out var cid) ? cid : 0,
                            CustomerName = a.CustomerName,
                        })
                        .ToList();
                    if (!nameMatch && summary.MatchedAccounts.Count == 0)
                    {
                        continue;
                    }
                }
                payees.Add(summary);
            }

            return new CommissionPayeesViewModel
            {
                PayeeTypeFilter = type,
                Search = term,
                Payees = payees
                    .OrderByDescending(p => p.Payable)
                    .ThenByDescending(p => p.Pool.Owed)
                    .ThenBy(p => p.Name)
                    .ToList(),
            };
        }

        public async Task<CommissionPayViewModel?> GetPayAsync(string payeeType, int payeeId)
        {
            var accounts = await GetPayeeAccountsAsync(payeeType, payeeId);
            if (accounts.Count == 0)
            {
                return null;
            }

            var isAgent = payeeType == CommissionPayeeType.Agent;
            var rows = accounts
                .Select(a =>
                {
                    var p = ToPayoutAccount(payeeType, a);
                    return new CommissionPayAccountRow
                    {
                        ContractId = p.ContractId,
                        AccountId = a.AccountId,
                        CustomerName = a.CustomerName,
                        AgentName = a.AgentName,
                        StartDate = a.StartDate,
                        DaysSinceStart = a.DaysSinceStart,
                        InArrears = InArrears(a),
                        MissingBuyingPrice = !isAgent && !a.BuyingPrice.HasValue,
                        Earned = p.Earned,
                        Deducted = p.Deducted,
                        Paid = p.Paid,
                        Unpaid = CommissionPayoutRules.Unpaid(p),
                        OwnNet = CommissionPayoutRules.OwnNet(p),
                        ProductName = a.ProductName,
                        Imei = a.Imei,
                        Deposit = a.Deposit,
                        BuyingPrice = a.BuyingPrice,
                        ContractValue = a.TotalCost,
                        TotalPaid = a.TotalPaid,
                        DaysPastLock = (int)Math.Max(0, Math.Floor(a.DaysPastLock)),
                        Breakdown = a.Commission,
                    };
                })
                .OrderByDescending(r => r.Unpaid > 0)
                .ThenBy(r => r.StartDate)
                .ToList();

            try
            {
                var payments = (await _payoutRepository.GetAccountPaymentsAsync(payeeType, rows.Where(r => r.ContractId > 0).Select(r => r.ContractId).ToList()))
                    .ToLookup(x => x.ContractId);
                foreach (var row in rows)
                {
                    row.Payments = payments[row.ContractId].OrderByDescending(x => x.PaidDate).ToList();
                }
            }
            catch (Exception ex)
            {
                // Details only -- the Pay screen still works without them.
                _logger.LogError(ex, "Per-account commission payments unavailable for {PayeeType} {PayeeId}", payeeType, payeeId);
            }

            var recent = await GetPayoutsAsync(payeeType, isAgent ? null : payeeId, isAgent ? payeeId : null, top: 10);

            return new CommissionPayViewModel
            {
                Payee = Summarize(payeeType, payeeId, accounts),
                Accounts = rows,
                RecentPayouts = recent,
            };
        }

        public async Task<(bool Ok, string Message)> RecordPayoutAsync(
            string payeeType, int payeeId, IReadOnlyCollection<long> contractIds, decimal amount, bool recordPastPayment,
            DateTime paidDate, string method, string? reference, string? notes, int recordedByUserId)
        {
            if (contractIds.Count == 0)
            {
                return (false, "Tick at least one account to pay.");
            }
            if (amount <= 0)
            {
                return (false, "Enter an amount greater than 0.");
            }
            if (!CommissionPayoutMethod.All.Contains(method))
            {
                return (false, "Choose a payment method.");
            }
            if (paidDate.Date > DateTime.Now.Date)
            {
                return (false, "The payment date can't be in the future.");
            }

            var accounts = await GetPayeeAccountsAsync(payeeType, payeeId);
            var all = accounts.Select(a => ToPayoutAccount(payeeType, a)).Where(p => p.ContractId > 0).ToList();
            var selected = all.Where(p => contractIds.Contains(p.ContractId)).ToList();
            if (selected.Count != contractIds.Count)
            {
                return (false, "Some ticked accounts don't belong to this payee. Reload the page and try again.");
            }

            var summary = Summarize(payeeType, payeeId, accounts);
            var payable = CommissionPayoutRules.Payable(all, contractIds, summary.IsSuspended);
            amount = Math.Round(amount, 2);

            // Rounding slack: the page shows whole shillings.
            if (amount > payable + 1 && !recordPastPayment)
            {
                return (false, summary.IsSuspended
                    ? $"Commissions are suspended: the default rate is {summary.DefaultRatePct:0.#}% (above {CommissionPayoutRules.SuspensionThresholdPct:0}%). Nothing is payable. To record a payment that was already made, tick \"Record a payment already made\"."
                    : $"KES {amount:N0} is more than the KES {payable:N0} payable on the ticked accounts after arrears. To record a payment that was already made, tick \"Record a payment already made\".");
            }

            var payout = new NewCommissionPayout
            {
                PayeeType = payeeType,
                DealerId = payeeType == CommissionPayeeType.Dealer ? payeeId : null,
                AgentUserId = payeeType == CommissionPayeeType.Agent ? payeeId : null,
                Amount = amount,
                PaidDate = paidDate,
                Method = method,
                Reference = string.IsNullOrWhiteSpace(reference) ? null : reference.Trim(),
                Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
                RecordedByUserId = recordedByUserId,
                Lines = CommissionPayoutRules.Allocate(selected, amount),
            };

            try
            {
                var id = await _payoutRepository.RecordAsync(payout);
                _logger.LogInformation("Commission payout {PayoutId}: KES {Amount} to {PayeeType} {PayeeId} over {Lines} account(s) by user {UserId}",
                    id, amount, payeeType, payeeId, payout.Lines.Count, recordedByUserId);
                return (true, $"Recorded KES {amount:N0} paid to {summary.Name} across {payout.Lines.Count} account(s).");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Recording commission payout failed for {PayeeType} {PayeeId}", payeeType, payeeId);
                return (false, "The payment could not be saved. Has Database/Commissions/002_create_commission_payouts.sql been run? Nothing was recorded.");
            }
        }

        public async Task<List<CommissionPayoutRecord>> GetPayoutsAsync(string? payeeType = null, int? dealerId = null, int? agentUserId = null, int? top = null)
        {
            try
            {
                return await _payoutRepository.GetPayoutsAsync(payeeType, dealerId, agentUserId, top);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Commission payout history unavailable -- has Database/Commissions/002 been run?");
                return new List<CommissionPayoutRecord>();
            }
        }

        public Task<bool> ConfirmReceiptAsync(int payoutId, string payeeType, int payeeId, int confirmedByUserId) =>
            _payoutRepository.ConfirmReceiptAsync(payoutId, payeeType, payeeId, confirmedByUserId);

        private async Task<List<CommissionAccount>> GetPayeeAccountsAsync(string payeeType, int payeeId) =>
            payeeType == CommissionPayeeType.Agent
                ? (await _reportRepository.GetCommissionAccountsAsync(null, payeeId)).Where(a => a.AgentId == payeeId).ToList()
                : await _reportRepository.GetCommissionAccountsAsync(payeeId);

        private static bool Contains(string? value, string term) =>
            value != null && value.Contains(term, StringComparison.OrdinalIgnoreCase);

        // The dashboards' Arrears tier: more than 7 days past the lock date.
        private static bool InArrears(CommissionAccount a) => a.DaysPastLock > 7;

        private static PayoutAccount ToPayoutAccount(string payeeType, CommissionAccount a)
        {
            var c = a.Commission;
            var isAgent = payeeType == CommissionPayeeType.Agent;
            return new PayoutAccount
            {
                ContractId = long.TryParse(a.ContractId, out var id) ? id : 0,
                StartDate = a.StartDate,
                Earned = isAgent ? c.AgentEarned : c.DealerCommission ?? 0,
                Deducted = isAgent ? c.AgentArrearsDeducted : c.DealerArrearsDeducted,
                Paid = isAgent ? c.AgentPaid : c.DealerPaid,
            };
        }

        private static CommissionPayeeSummary Summarize(string payeeType, int payeeId, List<CommissionAccount> accounts)
        {
            var isAgent = payeeType == CommissionPayeeType.Agent;
            var inArrears = accounts.Count(InArrears);
            var rate = CommissionPayoutRules.DefaultRatePct(accounts.Count, inArrears);
            var suspended = CommissionPayoutRules.IsSuspended(rate);
            var pool = isAgent
                ? CommissionCalculator.PoolAgent(accounts.Select(a => a.Commission))
                : CommissionCalculator.PoolDealer(accounts.Select(a => a.Commission));

            var dealerNames = accounts.Select(a => a.DealerName).Distinct().ToList();
            return new CommissionPayeeSummary
            {
                PayeeType = payeeType,
                PayeeId = payeeId,
                Name = isAgent ? accounts.Select(a => a.AgentName).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)) ?? $"Agent {payeeId}" : dealerNames.FirstOrDefault() ?? $"Dealer {payeeId}",
                DealerName = isAgent ? string.Join(", ", dealerNames) : null,
                Accounts = accounts.Count,
                AccountsInArrears = inArrears,
                DefaultRatePct = rate,
                IsSuspended = suspended,
                Pool = pool,
                Payable = suspended ? 0 : pool.Owed,
            };
        }
    }
}
