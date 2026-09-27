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

        // Dealer commission payable now, after arrears are deducted.
        public async Task<PagedResult<DealerCommissionReadyToPayReport>> DealerCommissionsReadyToPayAsync(CommissionsFilter filter)
        {
            var rows = (await LoadAsync(filter))
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
                    Status = a.Commission.DealerPaid > 0 ? "PARTIALLY PAID" : "READY TO PAY",
                })
                .OrderByDescending(r => r.AmountReadyToPay)
                .ToList();
            return Page(rows, filter);
        }

        // Per assigned agent, pooled like the Agent Commissions card.
        public async Task<PagedResult<AgentsTotalSummaryReport>> AgentsTotalSummaryAsync(CommissionsFilter filter)
        {
            var rows = (await LoadAsync(filter))
                .Where(a => a.AgentId.HasValue)
                .GroupBy(a => a.AgentId!.Value)
                .Select(g =>
                {
                    var pool = CommissionCalculator.PoolAgent(g.Select(a => a.Commission));
                    return new AgentsTotalSummaryReport
                    {
                        AgentId = g.Key,
                        AgentName = g.First().AgentName ?? "",
                        DealerName = g.First().DealerName,
                        TotalContracts = g.Count(),
                        TotalDeposits = g.Sum(a => a.Deposit),
                        TotalAgentCommission = pool.Earned,
                        Withheld = pool.Withheld,
                        Paid = pool.Paid,
                        Owed = pool.Owed,
                    };
                })
                .OrderByDescending(r => r.TotalAgentCommission)
                .ToList();
            return Page(rows, filter);
        }
    }
}
