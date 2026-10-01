using Ranalo.DataStore;
using Ranalo.Models;

namespace Ranalo.Services
{
    public interface ICommissionPayoutService
    {
        Task<CommissionPayeesViewModel> GetPayeesAsync(string? payeeTypeFilter, string? search = null);
        Task<CommissionPayViewModel?> GetPayAsync(string payeeType, int payeeId);
        Task<(bool Ok, string Message)> RecordPayoutAsync(
            string payeeType, int payeeId, IReadOnlyCollection<long> contractIds, string paymentType, decimal amount, bool recordPastPayment,
            DateTime paidDate, string method, string? reference, string? notes, int recordedByUserId);
        Task<List<CommissionPayoutRecord>> GetPayoutsAsync(string? payeeType = null, int? dealerId = null, int? agentUserId = null, int? top = null,
            int? collectorUserId = null);
        Task<bool> ConfirmReceiptAsync(int payoutId, string payeeType, int payeeId, int confirmedByUserId);

        // Collectors: 20% of what they recovered, per collections case.
        Task<CollectorPayViewModel?> GetCollectorPayAsync(int collectorUserId);
        Task<(bool Ok, string Message)> RecordCollectorPayoutAsync(
            int collectorUserId, IReadOnlyCollection<int> caseIds, decimal amount, bool recordPastPayment,
            DateTime paidDate, string method, string? reference, string? notes, int recordedByUserId);
    }

    // Pay Commissions: works out each dealer's and agent's position from the
    // same per-account figures as every other commission page
    // (CommissionCalculator), applies CommissionPayoutRules (suspension,
    // payable, allocation) and records payouts. Collectors are paid what
    // they earned on their collections cases (CollectionsRules), never suspended.
    public class CommissionPayoutService : ICommissionPayoutService
    {
        private readonly IDashboardReportRepository _reportRepository;
        private readonly ICommissionPayoutRepository _payoutRepository;
        private readonly ICollectionsService _collections;
        private readonly ILogger<CommissionPayoutService> _logger;

        public CommissionPayoutService(IDashboardReportRepository reportRepository, ICommissionPayoutRepository payoutRepository,
            ICollectionsService collections, ILogger<CommissionPayoutService> logger)
        {
            _reportRepository = reportRepository;
            _payoutRepository = payoutRepository;
            _collections = collections;
            _logger = logger;
        }

        public async Task<CommissionPayeesViewModel> GetPayeesAsync(string? payeeTypeFilter, string? search = null)
        {
            var type = CommissionPayeeType.Normalize(payeeTypeFilter);
            var accounts = type == CommissionPayeeType.Collector
                ? new List<CommissionAccount>()
                : await _reportRepository.GetCommissionAccountsAsync(null);

            var groups = new List<(string Type, int Id, List<CommissionAccount> Accounts)>();
            if (type is null or CommissionPayeeType.Agent)
            {
                groups.AddRange(accounts
                    .Where(a => a.AgentId.HasValue)
                    .GroupBy(a => a.AgentId!.Value)
                    .Select(g => (CommissionPayeeType.Agent, g.Key, g.ToList())));
            }
            if (type is null or CommissionPayeeType.Dealer)
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
                var summary = CommissionPayees.Summarize(payeeType, id, payeeAccounts);
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

            if (type is null or CommissionPayeeType.Collector)
            {
                payees.AddRange(await CollectorPayeesAsync(term));
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
                    var p = CommissionPayees.ToPayoutAccount(payeeType, a);
                    return new CommissionPayAccountRow
                    {
                        ContractId = p.ContractId,
                        AccountId = a.AccountId,
                        CustomerName = a.CustomerName,
                        AgentName = a.AgentName,
                        StartDate = a.StartDate,
                        DaysSinceStart = a.DaysSinceStart,
                        InArrears = CommissionPayees.InArrears(a),
                        MissingBuyingPrice = !isAgent && !a.BuyingPrice.HasValue,
                        Earned = p.Earned,
                        Deducted = p.Deducted,
                        Paid = p.Paid,
                        Unpaid = CommissionPayoutRules.Unpaid(p),
                        Upfront = isAgent ? Math.Min(p.Upfront, p.Earned) : 0,
                        UpfrontPaid = isAgent ? p.UpfrontPaid : 0,
                        UpfrontDue = isAgent ? CommissionPayoutRules.UnpaidUpfront(p) : 0,
                        BonusEarned = isAgent ? Math.Max(0, p.Earned - p.Upfront) : 0,
                        BonusPaid = isAgent ? p.BonusPaid : 0,
                        BonusDue = isAgent ? CommissionPayoutRules.UnpaidBonus(p) : 0,
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
                Payee = CommissionPayees.Summarize(payeeType, payeeId, accounts),
                Accounts = rows,
                RecentPayouts = recent,
            };
        }

        public async Task<(bool Ok, string Message)> RecordPayoutAsync(
            string payeeType, int payeeId, IReadOnlyCollection<long> contractIds, string paymentType, decimal amount, bool recordPastPayment,
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

            // Agents choose Upfront / Bonus / Auto; dealer commission has no parts.
            if (payeeType == CommissionPayeeType.Dealer)
            {
                paymentType = CommissionPart.Commission;
            }
            else if (!CommissionPart.AgentTypes.Contains(paymentType))
            {
                return (false, "Choose what this payment is for: Upfront, Bonus or Auto.");
            }

            var accounts = await GetPayeeAccountsAsync(payeeType, payeeId);
            var all = accounts.Select(a => CommissionPayees.ToPayoutAccount(payeeType, a)).Where(p => p.ContractId > 0).ToList();
            var selected = all.Where(p => contractIds.Contains(p.ContractId)).ToList();
            if (selected.Count != contractIds.Count)
            {
                return (false, "Some ticked accounts don't belong to this payee. Reload the page and try again.");
            }

            var summary = CommissionPayees.Summarize(payeeType, payeeId, accounts);
            var payable = CommissionPayoutRules.Payable(all, contractIds, summary.IsSuspended, paymentType);
            amount = Math.Round(amount, 2);

            if (amount > payable && !recordPastPayment)
            {
                // Up to KES 1 over is whole-shilling rounding on the page: pay
                // exactly what's payable rather than leave a stray remainder.
                if (amount <= payable + 1 && payable > 0)
                {
                    amount = payable;
                }
                else
                {
                    var what = paymentType switch
                    {
                        CommissionPart.Upfront => "upfront commission still due",
                        CommissionPart.Bonus => "bonus earned and still due",
                        _ => "commission payable",
                    };
                    return (false, summary.IsSuspended
                        ? $"Commissions are suspended: the default rate is {summary.DefaultRatePct:0.#}% (above {CommissionPayoutRules.SuspensionThresholdPct:0}%). Nothing is payable. To record a payment that was already made, tick \"Record a payment already made\"."
                        : $"KES {amount:N0} is more than the KES {payable:N0} {what} on the ticked accounts after arrears. To record a payment that was already made, tick \"Record a payment already made\".");
                }
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
                PaymentType = paymentType,
                Lines = CommissionPayoutRules.Allocate(selected, amount, paymentType),
            };

            var accountCount = payout.Lines.Select(l => l.ContractId).Distinct().Count();
            try
            {
                var id = await _payoutRepository.RecordAsync(payout);
                _logger.LogInformation("Commission payout {PayoutId}: KES {Amount} ({PaymentType}) to {PayeeType} {PayeeId} over {Accounts} account(s) by user {UserId}",
                    id, amount, paymentType, payeeType, payeeId, accountCount, recordedByUserId);
                var typeLabel = payeeType == CommissionPayeeType.Agent ? $" {paymentType.ToLowerInvariant()} commission" : "";
                return (true, $"Recorded KES {amount:N0}{typeLabel} paid to {summary.Name} across {accountCount} account(s).");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Recording commission payout failed for {PayeeType} {PayeeId}", payeeType, payeeId);
                return (false, "The payment could not be saved. Have Database/Commissions/002 and 004 been run? Nothing was recorded.");
            }
        }

        public async Task<CollectorPayViewModel?> GetCollectorPayAsync(int collectorUserId)
        {
            var cases = await _collections.GetEarningsAsync(collectorUserId);
            var payee = (await CollectorPayeesAsync(null)).FirstOrDefault(p => p.PayeeId == collectorUserId);
            if (payee == null)
            {
                return null;
            }

            return new CollectorPayViewModel
            {
                Payee = payee,
                Cases = cases.OrderByDescending(c => c.Due > 0).ThenBy(c => c.FirstHeld).ToList(),
                RecentPayouts = await GetPayoutsAsync(CommissionPayeeType.Collector, collectorUserId: collectorUserId, top: 10),
            };
        }

        public async Task<(bool Ok, string Message)> RecordCollectorPayoutAsync(
            int collectorUserId, IReadOnlyCollection<int> caseIds, decimal amount, bool recordPastPayment,
            DateTime paidDate, string method, string? reference, string? notes, int recordedByUserId)
        {
            if (caseIds.Count == 0)
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

            var earnings = await _collections.GetEarningsAsync(collectorUserId);
            var selected = earnings.Where(e => caseIds.Contains(e.CaseId)).OrderBy(e => e.FirstHeld).ThenBy(e => e.CaseId).ToList();
            if (selected.Count != caseIds.Distinct().Count())
            {
                return (false, "Some ticked accounts aren't this collector's. Reload the page and try again.");
            }

            amount = Math.Round(amount, 2);
            var payable = selected.Sum(e => e.Due);
            if (amount > payable && !recordPastPayment)
            {
                if (amount <= payable + 1 && payable > 0)
                {
                    amount = payable;
                }
                else
                {
                    return (false, $"KES {amount:N0} is more than the KES {payable:N0} due on the ticked accounts. To record a payment that was already made, tick \"Record a payment already made\".");
                }
            }

            // Oldest account first, each up to what is due on it; anything
            // over (a recorded past payment) lands on the newest ticked one.
            var lines = new List<CollectorPayoutLine>();
            var remaining = amount;
            foreach (var e in selected)
            {
                var take = Math.Min(Math.Round(e.Due, 2), remaining);
                if (take > 0)
                {
                    lines.Add(new CollectorPayoutLine(e.CaseId, take));
                    remaining -= take;
                }
            }
            if (remaining > 0)
            {
                lines.Add(new CollectorPayoutLine(selected[^1].CaseId, remaining));
            }

            var name = (await _collections.GetCollectorsAsync()).FirstOrDefault(c => c.UserId == collectorUserId)?.Name ?? $"collector {collectorUserId}";
            try
            {
                var id = await _payoutRepository.RecordAsync(new NewCommissionPayout
                {
                    PayeeType = CommissionPayeeType.Collector,
                    CollectorUserId = collectorUserId,
                    Amount = amount,
                    PaidDate = paidDate,
                    Method = method,
                    Reference = string.IsNullOrWhiteSpace(reference) ? null : reference.Trim(),
                    Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
                    RecordedByUserId = recordedByUserId,
                    PaymentType = CommissionPart.Commission,
                    CollectorLines = lines
                        .GroupBy(l => l.CaseId)
                        .Select(g => new CollectorPayoutLine(g.Key, g.Sum(l => l.Amount)))
                        .ToList(),
                });
                _logger.LogInformation("Collector payout {PayoutId}: KES {Amount} to collector {CollectorUserId} by user {UserId}",
                    id, amount, collectorUserId, recordedByUserId);
                return (true, $"Recorded KES {amount:N0} paid to {name} across {lines.Select(l => l.CaseId).Distinct().Count()} account(s).");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Recording collector payout failed for {CollectorUserId}", collectorUserId);
                return (false, "The payment could not be saved. Has Database/Collections/001 been run? Nothing was recorded.");
            }
        }

        // One Pay Commissions row per collector who has earned anything.
        private async Task<List<CommissionPayeeSummary>> CollectorPayeesAsync(string? term)
        {
            List<CollectorCaseEarning> earnings;
            List<CollectorOption> collectors;
            try
            {
                earnings = await _collections.GetEarningsAsync();
                collectors = await _collections.GetCollectorsAsync();
            }
            catch (System.Data.SqlClient.SqlException ex) when (ex.Number is 207 or 208)
            {
                return new List<CommissionPayeeSummary>();
            }

            var result = new List<CommissionPayeeSummary>();
            foreach (var g in earnings.GroupBy(e => e.CollectorUserId))
            {
                var name = collectors.FirstOrDefault(c => c.UserId == g.Key)?.Name ?? $"Collector {g.Key}";
                var earned = g.Sum(e => e.Earned);
                var paid = g.Sum(e => e.Paid);
                var summary = new CommissionPayeeSummary
                {
                    PayeeType = CommissionPayeeType.Collector,
                    PayeeId = g.Key,
                    Name = name,
                    Accounts = g.Count(),
                    Pool = new CommissionPool { Earned = earned, Paid = paid, Owed = Math.Max(0, earned - paid) },
                    Payable = g.Sum(e => e.Due),
                };

                if (!string.IsNullOrEmpty(term))
                {
                    // For collectors the matched "contract" is the collections case.
                    summary.MatchedAccounts = g
                        .Where(e => e.AccountNo.ToString() == term || e.ContractId.ToString() == term || Contains(e.CustomerName, term))
                        .Select(e => new CommissionMatchedAccount { AccountId = e.AccountNo, ContractId = e.CaseId, CustomerName = e.CustomerName })
                        .ToList();
                    if (!Contains(name, term) && summary.MatchedAccounts.Count == 0)
                    {
                        continue;
                    }
                }
                result.Add(summary);
            }
            return result;
        }

        public async Task<List<CommissionPayoutRecord>> GetPayoutsAsync(string? payeeType = null, int? dealerId = null, int? agentUserId = null, int? top = null,
            int? collectorUserId = null)
        {
            try
            {
                return await _payoutRepository.GetPayoutsAsync(payeeType, dealerId, agentUserId, top, collectorUserId);
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
    }
}
