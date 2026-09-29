using Ranalo.DataStore;
using Ranalo.Models;
using Ranalo.Models.Reports;

namespace Ranalo.Services
{
    // Admin Commissions pages. Built from the same per-account data and
    // CommissionCalculator as the dashboards and Account Commissions page,
    // so every commission figure in the app agrees.
    public class CommissionsReportsService : ICommissionsReportsService
    {
        private readonly IDashboardReportRepository _repository;

        public CommissionsReportsService(IDashboardReportRepository repository)
        {
            _repository = repository;
        }

        private Task<List<CommissionAccount>> LoadAsync(CommissionsFilter filter) =>
            _repository.GetCommissionAccountsAsync(filter.DealerId, filter.AgentId);

        private static PagedResult<T> Page<T>(List<T> all, CommissionsFilter filter) => new()
        {
            Items = all.Skip((Math.Max(1, filter.PageNumber) - 1) * filter.PageSize).Take(filter.PageSize).ToList(),
            TotalCount = all.Count,
            PageNumber = filter.PageNumber,
            PageSize = filter.PageSize,
        };

        public async Task<PagedResult<MainCommissionsSummaryReport>> FullCommissionsReportAsync(CommissionsFilter filter)
        {
            var rows = (await LoadAsync(filter))
                .Select(a => new MainCommissionsSummaryReport
                {
                    ContractID = a.ContractId,
                    AccountNo = a.AccountId,
                    First_Name = a.CustomerName,
                    StartDate = a.StartDate,
                    DealerName = a.DealerName,
                    AgentName = a.AgentId.HasValue ? a.AgentName : null,
                    TotalAmount = a.TotalCost,
                    Deposit = a.Deposit,
                    TotalPaid = a.TotalPaid,
                    BuyingPrice = a.BuyingPrice,
                    AgentCommission = a.Commission.AgentEarned,
                    AgentArrearsDeducted = a.Commission.AgentArrearsDeducted,
                    DealerCommission = a.Commission.DealerCommission,
                    DealerArrearsDeducted = a.Commission.DealerArrearsDeducted,
                    DealerBalance = a.Commission.DealerBalance,
                    DealerEligible = a.Commission.DealerBalance > 0,
                })
                .Where(r => filter.DealerEligible == null || r.DealerEligible == filter.DealerEligible)
                .OrderByDescending(r => r.StartDate)
                .ToList();
            return Page(rows, filter);
        }

        // Dealer commission not fully paid yet, including amounts held for arrears.
        public async Task<PagedResult<OutstandingDealerCommissionReport>> OutstandingDealerCommissionsAsync(CommissionsFilter filter)
        {
            var rows = (await LoadAsync(filter))
                .Where(a => a.Commission.DealerCommission.HasValue
                            && a.Commission.DealerCommission.Value - a.Commission.DealerPaid > 0)
                .Select(a => new OutstandingDealerCommissionReport
                {
                    AccountNo = a.AccountId,
                    First_Name = a.CustomerName,
                    DealerName = a.DealerName,
                    TotalPaid = a.TotalPaid,
                    BuyingPrice = a.BuyingPrice!.Value,
                    EarnedDealerCommission = a.Commission.DealerCommission!.Value,
                    TotalDealerPaid = a.Commission.DealerPaid,
                    Outstanding = a.Commission.DealerCommission!.Value - a.Commission.DealerPaid,
                    ArrearsDeducted = a.Commission.DealerArrearsDeducted,
                    RemainingDealerBalance = a.Commission.DealerBalance!.Value,
                })
                .OrderByDescending(r => r.Outstanding)
                .ToList();
            return Page(rows, filter);
        }

        // Dealer commission payable now, after arrears are deducted. Accounts
        // of a suspended dealer are listed as SUSPENDED (held, not payable).
        public async Task<PagedResult<DealerCommissionReadyToPayReport>> DealerCommissionsReadyToPayAsync(CommissionsFilter filter)
        {
            var accounts = await LoadAsync(filter);
            var suspendedDealers = CommissionPayees.Dealers(accounts).Where(d => d.IsSuspended).Select(d => d.PayeeId).ToHashSet();
            var rows = accounts
                .Where(a => a.Commission.DealerBalance > 0)
                .Select(a => new DealerCommissionReadyToPayReport
                {
                    AccountNo = a.AccountId,
                    First_Name = a.CustomerName,
                    DealerName = a.DealerName,
                    TotalPaid = a.TotalPaid,
                    BuyingPrice = a.BuyingPrice!.Value,
                    EarnedDealerCommission = a.Commission.DealerCommission!.Value,
                    ArrearsDeducted = a.Commission.DealerArrearsDeducted,
                    TotalDealerPaid = a.Commission.DealerPaid,
                    AmountReadyToPay = a.Commission.DealerBalance!.Value,
                    DealerSuspended = suspendedDealers.Contains(a.DealerId),
                    Status = suspendedDealers.Contains(a.DealerId) ? "SUSPENDED"
                        : a.Commission.DealerPaid > 0 ? "PARTIALLY PAID" : "READY TO PAY",
                })
                .OrderByDescending(r => r.AmountReadyToPay)
                .ToList();
            return Page(rows, filter);
        }

        // Per assigned agent, pooled like the Agent Commissions card, with
        // suspension and the upfront / bonus still due (CommissionPayees).
        public async Task<PagedResult<AgentsTotalSummaryReport>> AgentsTotalSummaryAsync(CommissionsFilter filter)
        {
            var accounts = await LoadAsync(filter);
            var deposits = accounts.Where(a => a.AgentId.HasValue)
                .GroupBy(a => a.AgentId!.Value)
                .ToDictionary(g => g.Key, g => g.Sum(a => a.Deposit));
            var rows = CommissionPayees.Agents(accounts)
                .Select(s => new AgentsTotalSummaryReport
                {
                    AgentId = s.PayeeId,
                    AgentName = s.Name,
                    DealerName = s.DealerName ?? "",
                    TotalContracts = s.Accounts,
                    TotalDeposits = deposits.TryGetValue(s.PayeeId, out var dep) ? dep : 0,
                    TotalAgentCommission = s.Pool.Earned,
                    Withheld = s.Pool.Withheld,
                    Paid = s.Pool.Paid,
                    Owed = s.Pool.Owed,
                    DefaultRatePct = s.DefaultRatePct,
                    IsSuspended = s.IsSuspended,
                    Payable = s.Payable,
                    UpfrontDue = s.UpfrontDue,
                    BonusDue = s.BonusDue,
                })
                .OrderByDescending(r => r.TotalAgentCommission)
                .ToList();
            return Page(rows, filter);
        }
    }
}
