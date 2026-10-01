using System.Data.SqlClient;
using Ranalo.DataStore;
using Ranalo.DataStore.DataModels;
using Ranalo.Models;

namespace Ranalo.Services
{
    public interface ICollectionsService
    {
        Task<CollectionsAdminViewModel> GetAdminAsync(string? tab, string? search, int? dealerId, int? collectorUserId);
        Task<CollectorDashboardViewModel> GetCollectorDashboardAsync(int collectorUserId);

        // Hands pool accounts to a collector (opens a case on each).
        Task<(bool Ok, string Message)> AssignAsync(IReadOnlyCollection<long> accountNos, int collectorUserId, int byUserId);
        Task<(bool Ok, string Message)> ReassignAsync(IReadOnlyCollection<int> caseIds, int collectorUserId, int byUserId);
        Task<(bool Ok, string Message)> ReturnAsync(IReadOnlyCollection<int> caseIds, int byUserId);

        Task<(bool Ok, string Message)> AddFlagAsync(int caseId, int collectorUserId, string? note);
        Task<bool> ResolveFlagAsync(int flagId, int byUserId);

        // Dealer / Agent dashboards: their accounts that went to collections.
        Task<InCollectionsSection> GetInCollectionsAsync(int? dealerId, int? agentUserId);

        // Pay Commissions: what each collector has earned, been paid and is due.
        Task<List<CollectorCaseEarning>> GetEarningsAsync(int? collectorUserId = null);
        Task<List<CollectorOption>> GetCollectorsAsync();

        // Customer Details: the account's standing, contacts, latest payments
        // and its open collections case (null when not in collections).
        Task<CollectionContractStanding?> GetStandingAsync(long accountNo);
        Task<AccountContact?> GetContactAsync(long accountNo);
        Task<List<KosePayments>> GetRecentPaymentsAsync(long accountNo, int take = 10);
        Task<CollectionCaseRow?> GetOpenCaseAsync(long accountNo);
    }

    // Applies CollectionsRules to the collections tables. See CollectionsRules
    // for the rules and Database/Collections/001 for the tables.
    public class CollectionsService : ICollectionsService
    {
        private readonly ICollectionsRepository _repository;
        private readonly IDashboardReportRepository _reportRepository;
        private readonly IDealerAllocationRepository _dealers;
        private readonly ILogger<CollectionsService> _logger;

        public CollectionsService(ICollectionsRepository repository, IDashboardReportRepository reportRepository,
            IDealerAllocationRepository dealers, ILogger<CollectionsService> logger)
        {
            _repository = repository;
            _reportRepository = reportRepository;
            _dealers = dealers;
            _logger = logger;
        }

        // A user who can be handed accounts: a Collector login, or another
        // login with Collector as an extra role.
        public static bool IsCollector(User? user) =>
            user != null && (user.RoleId == UserRole.Collector
                             || (user.OtherSelectedRoles?.Any(r => string.Equals(r, "Collector", StringComparison.OrdinalIgnoreCase)) ?? false));

        public async Task<CollectionsAdminViewModel> GetAdminAsync(string? tab, string? search, int? dealerId, int? collectorUserId)
        {
            var model = new CollectionsAdminViewModel
            {
                Tab = tab is "pool" or "flags" or "closed" ? tab : "cases",
                Search = string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
                DealerId = dealerId,
                CollectorUserId = collectorUserId,
                Dealers = await _dealers.GetDealersAsync(),
            };

            try
            {
                model.CollectorOptions = await _repository.GetCollectorsAsync();
                var standings = await _repository.GetStandingsAsync();
                var cases = await BuildCasesAsync(standings.ToDictionary(s => s.AccountNo, s => s));
                var flags = await _repository.GetFlagsAsync(openOnly: true);
                var earnings = await GetEarningsAsync();

                model.Collectors = Summaries(cases, earnings, flags, model.CollectorOptions);
                model.OpenCaseCount = cases.Count(c => c.Status == CollectionCaseStatus.Open);
                model.DueForReassignmentCount = cases.Count(c => c.DueForReassignment);
                model.OpenFlagCount = flags.Count;

                var pool = standings
                    .Where(s => CollectionsRules.IsEligible(s.Shortfall, s.DaysSinceLastPayment))
                    .Where(s => !cases.Any(c => c.Status == CollectionCaseStatus.Open && c.ContractId == s.ContractId))
                    .ToList();
                model.PoolCount = pool.Count;

                // Exposure: what agents and dealers carry for collections
                // accounts (latest case per contract).
                var latest = cases.GroupBy(c => c.ContractId).Select(g => g.OrderByDescending(c => c.CaseId).First()).ToList();
                model.DealerExposure = latest.Where(c => c.DealerId.HasValue)
                    .GroupBy(c => c.DealerId!.Value)
                    .Select(g => new CollectionsExposureRow
                    {
                        PayeeType = CommissionPayeeType.Dealer, PayeeId = g.Key, Name = g.First().DealerName ?? $"Dealer {g.Key}",
                        Accounts = g.Count(), Deduction = g.Sum(c => c.CurrentDeduction),
                    })
                    .OrderByDescending(r => r.Deduction).ToList();
                model.AgentExposure = latest.Where(c => c.AgentUserId.HasValue)
                    .GroupBy(c => c.AgentUserId!.Value)
                    .Select(g => new CollectionsExposureRow
                    {
                        PayeeType = CommissionPayeeType.Agent, PayeeId = g.Key, Name = g.First().AgentName ?? $"Agent {g.Key}",
                        Accounts = g.Count(), Deduction = g.Sum(c => c.CurrentDeduction),
                    })
                    .OrderByDescending(r => r.Deduction).ToList();

                bool Matches(string? value) => value != null && model.Search != null && value.Contains(model.Search, StringComparison.OrdinalIgnoreCase);
                bool SearchHit(long accountNo, long contractId, params string?[] names) =>
                    model.Search == null || accountNo.ToString() == model.Search || contractId.ToString() == model.Search || names.Any(Matches);

                model.Cases = cases
                    .Where(c => model.Tab == "closed" ? c.Status != CollectionCaseStatus.Open : c.Status == CollectionCaseStatus.Open)
                    .Where(c => dealerId == null || c.DealerId == dealerId)
                    .Where(c => collectorUserId == null || c.CollectorUserId == collectorUserId)
                    .Where(c => SearchHit(c.AccountNo, c.ContractId, c.CustomerName, c.CollectorName, c.AgentName, c.DealerName))
                    .OrderByDescending(c => c.DueForReassignment)
                    .ThenByDescending(c => c.OpenFlagCount)
                    .ThenByDescending(c => c.HandoverAt)
                    .ToList();

                model.Pool = pool
                    .Where(s => dealerId == null || s.DealerId == dealerId)
                    .Where(s => SearchHit(s.AccountNo, s.ContractId, s.CustomerName, s.AgentName, s.DealerName))
                    .OrderByDescending(s => s.Shortfall)
                    .ToList();

                model.Flags = flags
                    .Where(f => collectorUserId == null || f.CollectorUserId == collectorUserId)
                    .ToList();
            }
            catch (SqlException ex) when (ex.Number is 208 or 207)
            {
                _logger.LogWarning(ex, "Collections tables missing -- run Database/Collections/001_create_collections_tables.sql");
                model.SetupMissing = true;
            }

            return model;
        }

        public async Task<CollectorDashboardViewModel> GetCollectorDashboardAsync(int collectorUserId)
        {
            var model = new CollectorDashboardViewModel();
            try
            {
                var periods = await _repository.GetPeriodsAsync(collectorUserId);
                var caseIds = periods.Select(p => p.CaseId).ToHashSet();
                var allCases = await _repository.GetCasesAsync();
                var held = allCases.Where(c => caseIds.Contains(c.CaseId)).ToList();
                var standings = (await _repository.GetStandingsAsync(held.Select(c => c.AccountNo).ToList()))
                    .ToDictionary(s => s.AccountNo, s => s);

                var cases = await EnrichAsync(held, periods, standings);
                var contacts = await _repository.GetContactsAsync(held.Select(c => c.AccountNo).ToList());
                foreach (var c in cases)
                {
                    c.Contact = contacts.GetValueOrDefault(c.AccountNo);
                }
                var flags = (await _repository.GetFlagsAsync(openOnly: true)).Where(f => f.CollectorUserId == collectorUserId).ToList();
                var earnings = await GetEarningsAsync(collectorUserId);

                // Held now = their stint on it is still open. (Only their own
                // stints are loaded, so a case reassigned away still shows
                // them as its last collector.)
                var heldNow = periods.Where(p => p.EndAt == null).Select(p => p.CaseId).ToHashSet();
                model.Cases = cases
                    .Where(c => c.Status == CollectionCaseStatus.Open && heldNow.Contains(c.CaseId))
                    .OrderByDescending(c => c.DueForReassignment)
                    .ThenByDescending(c => c.DaysWithoutPaymentWhileHeld)
                    .ToList();
                model.Earnings = earnings.OrderByDescending(e => e.HeldNow).ThenByDescending(e => e.FirstHeld).ToList();
                model.Summary = Summaries(model.Cases, earnings, flags, new List<CollectorOption>())
                    .FirstOrDefault(s => s.CollectorUserId == collectorUserId) ?? new CollectorSummary { CollectorUserId = collectorUserId };
            }
            catch (SqlException ex) when (ex.Number is 208 or 207)
            {
                _logger.LogWarning(ex, "Collections tables missing -- run Database/Collections/001_create_collections_tables.sql");
                model.SetupMissing = true;
            }
            return model;
        }

        public async Task<(bool Ok, string Message)> AssignAsync(IReadOnlyCollection<long> accountNos, int collectorUserId, int byUserId)
        {
            if (accountNos.Count == 0)
            {
                return (false, "Tick at least one account.");
            }

            var collector = (await _repository.GetCollectorsAsync()).FirstOrDefault(c => c.UserId == collectorUserId);
            if (collector == null)
            {
                return (false, "Choose an active collector.");
            }

            var standings = (await _repository.GetStandingsAsync(accountNos)).ToDictionary(s => s.AccountNo, s => s);
            var cases = await _repository.GetCasesAsync();
            var terms = await _reportRepository.GetCollectionTermsAsync();
            var candidate = new CollectorCandidate { UserId = collector.UserId, DealerId = collector.DealerId, IsDealerUser = collector.IsDealerUser };

            int opened = 0;
            var skipped = new List<string>();
            foreach (var accountNo in accountNos.Distinct())
            {
                if (!standings.TryGetValue(accountNo, out var s))
                {
                    skipped.Add($"{accountNo}: no open contract");
                    continue;
                }

                var openCase = cases.FirstOrDefault(c => c.AccountNo == accountNo && c.Status == CollectionCaseStatus.Open);
                if (openCase != null && openCase.ContractId == s.ContractId)
                {
                    skipped.Add($"{accountNo}: already in collections (use Reassign)");
                    continue;
                }
                if (!CollectionsRules.IsEligible(s.Shortfall, s.DaysSinceLastPayment))
                {
                    skipped.Add($"{accountNo}: not eligible (needs arrears and no payment for {CollectionsRules.EligibleDaysWithoutPayment} days)");
                    continue;
                }
                if (CollectionsRules.IsConflicted(candidate, s.AgentUserId, s.DealerId))
                {
                    skipped.Add($"{accountNo}: {collector.Name} is this account's agent or dealer");
                    continue;
                }

                // Back in collections after a return: re-frozen at what the
                // agent and dealer were being deducted at that moment.
                var previous = terms.GetValueOrDefault(s.ContractId);
                var frozen = previous != null ? CollectionsRules.Deduction(previous, s.Shortfall) : s.Shortfall;
                var basis = previous != null ? CollectionsRules.DealerCountedPaid(previous, s.TotalPaid) : s.TotalPaid;

                // The device's open case on its previous contract (before a
                // resale) ends: never two collectors on one device.
                CaseClosure? supersede = null;
                if (openCase != null)
                {
                    var (recovered, earned) = await CaseRecoveryAsync(openCase.CaseId);
                    supersede = new CaseClosure
                    {
                        CaseId = openCase.CaseId, AccountNo = openCase.AccountNo, ContractId = openCase.ContractId,
                        Status = CollectionCaseStatus.Superseded, Recovered = recovered, CollectorEarned = earned, ClosedByUserId = byUserId,
                    };
                }

                await _repository.OpenCaseAsync(new NewCollectionCase
                {
                    AccountNo = s.AccountNo,
                    ContractId = s.ContractId,
                    DealerId = s.DealerId,
                    AgentUserId = s.AgentUserId,
                    ShortfallAtHandover = s.Shortfall,
                    FrozenDeduction = Math.Round(frozen, 2),
                    TotalPaidAtHandover = s.TotalPaid,
                    CommissionPaidBasis = Math.Round(basis, 2),
                    CollectorUserId = collectorUserId,
                    OpenedByUserId = byUserId,
                }, supersede);
                opened++;
            }

            _logger.LogInformation("Collections: {Opened} account(s) handed to collector {CollectorUserId} by user {UserId}; {Skipped} skipped",
                opened, collectorUserId, byUserId, skipped.Count);
            return (opened > 0, Message($"Handed {opened} account(s) to {collector.Name}.", skipped));
        }

        public async Task<(bool Ok, string Message)> ReassignAsync(IReadOnlyCollection<int> caseIds, int collectorUserId, int byUserId)
        {
            if (caseIds.Count == 0)
            {
                return (false, "Tick at least one account.");
            }

            var collector = (await _repository.GetCollectorsAsync()).FirstOrDefault(c => c.UserId == collectorUserId);
            if (collector == null)
            {
                return (false, "Choose an active collector.");
            }

            var candidate = new CollectorCandidate { UserId = collector.UserId, DealerId = collector.DealerId, IsDealerUser = collector.IsDealerUser };
            var cases = (await _repository.GetCasesAsync()).ToDictionary(c => c.CaseId);
            var holders = (await _repository.GetPeriodsAsync()).Where(p => p.EndAt == null).ToDictionary(p => p.CaseId, p => p.CollectorUserId);

            int moved = 0;
            var skipped = new List<string>();
            foreach (var id in caseIds.Distinct())
            {
                if (!cases.TryGetValue(id, out var c) || c.Status != CollectionCaseStatus.Open)
                {
                    skipped.Add($"case {id}: not open");
                    continue;
                }
                if (holders.GetValueOrDefault(id) == collectorUserId)
                {
                    skipped.Add($"{c.AccountNo}: already with {collector.Name}");
                    continue;
                }
                if (CollectionsRules.IsConflicted(candidate, c.AgentUserId, c.DealerId))
                {
                    skipped.Add($"{c.AccountNo}: {collector.Name} is this account's agent or dealer");
                    continue;
                }

                await _repository.ReassignAsync(id, c.AccountNo, c.ContractId, collectorUserId, byUserId);
                moved++;
            }

            return (moved > 0, Message($"Reassigned {moved} account(s) to {collector.Name}. They earn on payments from now on.", skipped));
        }

        public async Task<(bool Ok, string Message)> ReturnAsync(IReadOnlyCollection<int> caseIds, int byUserId)
        {
            if (caseIds.Count == 0)
            {
                return (false, "Tick at least one account.");
            }

            var cases = (await _repository.GetCasesAsync()).ToDictionary(c => c.CaseId);
            var picked = caseIds.Distinct().Where(cases.ContainsKey).Select(id => cases[id]).ToList();
            var standings = (await _repository.GetStandingsAsync(picked.Select(c => c.AccountNo).ToList()))
                .ToDictionary(s => s.AccountNo, s => s);

            int returned = 0;
            var skipped = new List<string>();
            foreach (var c in picked)
            {
                if (c.Status != CollectionCaseStatus.Open)
                {
                    skipped.Add($"{c.AccountNo}: not open");
                    continue;
                }
                if (!standings.TryGetValue(c.AccountNo, out var s) || s.ContractId != c.ContractId)
                {
                    skipped.Add($"{c.AccountNo}: the contract has ended (device repossessed) -- it stays in collections");
                    continue;
                }

                var (recovered, earned) = await CaseRecoveryAsync(c.CaseId);
                await _repository.ReturnAsync(new CaseClosure
                {
                    CaseId = c.CaseId,
                    AccountNo = c.AccountNo,
                    ContractId = c.ContractId,
                    Status = CollectionCaseStatus.Returned,
                    Recovered = recovered,
                    CollectorEarned = earned,
                    ShortfallAtReturn = s.Shortfall,
                    TotalPaidAtReturn = s.TotalPaid,
                    ClosedByUserId = byUserId,
                });
                returned++;
            }

            return (returned > 0, Message(
                $"Returned {returned} account(s) to their agent and dealer. They keep the frozen arrears plus the collection costs.", skipped));
        }

        public async Task<(bool Ok, string Message)> AddFlagAsync(int caseId, int collectorUserId, string? note)
        {
            if (string.IsNullOrWhiteSpace(note))
            {
                return (false, "Add a note saying what you tried.");
            }

            var holder = (await _repository.GetPeriodsAsync(collectorUserId, caseId)).FirstOrDefault(p => p.EndAt == null);
            if (holder == null)
            {
                return (false, "That account isn't assigned to you.");
            }

            await _repository.AddFlagAsync(caseId, collectorUserId, note.Trim().Length > 500 ? note.Trim()[..500] : note.Trim());
            return (true, "Flagged for the admin team.");
        }

        public Task<bool> ResolveFlagAsync(int flagId, int byUserId) => _repository.ResolveFlagAsync(flagId, byUserId);

        public async Task<InCollectionsSection> GetInCollectionsAsync(int? dealerId, int? agentUserId)
        {
            var section = new InCollectionsSection { IsAgentView = agentUserId.HasValue };
            try
            {
                var accounts = await _reportRepository.GetCommissionAccountsAsync(dealerId, agentUserId);
                var mine = accounts
                    .Where(a => a.Commission.InCollections && (!agentUserId.HasValue || a.AgentId == agentUserId))
                    .ToList();
                if (mine.Count == 0)
                {
                    return section;
                }

                var cases = (await _repository.GetCasesAsync())
                    .GroupBy(c => c.ContractId)
                    .ToDictionary(g => g.Key, g => g.OrderByDescending(c => c.CaseId).First());
                var terms = await _reportRepository.GetCollectionTermsAsync();

                section.Rows = mine
                    .Select(a =>
                    {
                        var cid = long.TryParse(a.ContractId, out var id) ? id : 0;
                        var c = cases.GetValueOrDefault(cid);
                        var t = terms.GetValueOrDefault(cid);
                        var deduction = agentUserId.HasValue ? a.Commission.AgentArrearsDeducted : a.Commission.DealerArrearsDeducted;
                        return new InCollectionsAccountRow
                        {
                            AccountNo = a.AccountId,
                            ContractId = cid,
                            CustomerName = a.CustomerName,
                            AgentName = a.AgentName,
                            HandoverAt = c?.HandoverAt ?? DateTime.MinValue,
                            Status = c == null ? "" : c.Status == CollectionCaseStatus.Returned ? "Returned to you" : "With collections",
                            FrozenDeduction = t?.FrozenDeduction ?? 0,
                            CollectionCosts = t is { IsReturned: true } ? t.CollectorEarned + t.RepossessionCost : 0,
                            CurrentDeduction = deduction,
                        };
                    })
                    .OrderByDescending(r => r.HandoverAt)
                    .ToList();
            }
            catch (SqlException ex) when (ex.Number is 208 or 207)
            {
                // Collections not set up yet: nothing to show.
            }
            return section;
        }

        public async Task<List<CollectorCaseEarning>> GetEarningsAsync(int? collectorUserId = null)
        {
            var periods = await _repository.GetPeriodsAsync(collectorUserId);
            if (periods.Count == 0)
            {
                return new List<CollectorCaseEarning>();
            }

            var paid = await _repository.GetCollectorPaidAsync(collectorUserId);
            var cases = (await _repository.GetCasesAsync()).ToDictionary(c => c.CaseId);

            return periods
                .GroupBy(p => (p.CaseId, p.CollectorUserId))
                .Select(g =>
                {
                    var c = cases.GetValueOrDefault(g.Key.CaseId);
                    var recovered = g.Sum(p => p.Recovered);
                    return new CollectorCaseEarning
                    {
                        CaseId = g.Key.CaseId,
                        CollectorUserId = g.Key.CollectorUserId,
                        AccountNo = c?.AccountNo ?? 0,
                        ContractId = c?.ContractId ?? 0,
                        CustomerName = c?.CustomerName ?? "",
                        HeldNow = g.Any(p => p.EndAt == null),
                        FirstHeld = g.Min(p => p.StartAt),
                        Recovered = recovered,
                        RecoveredThisMonth = g.Sum(p => p.RecoveredThisMonth),
                        Earned = CollectionsRules.CollectorShare(recovered),
                        Paid = paid.GetValueOrDefault(g.Key),
                    };
                })
                .ToList();
        }

        public Task<List<CollectorOption>> GetCollectorsAsync() => _repository.GetCollectorsAsync();

        public async Task<CollectionContractStanding?> GetStandingAsync(long accountNo) =>
            (await _repository.GetStandingsAsync(new[] { accountNo })).FirstOrDefault();

        public async Task<AccountContact?> GetContactAsync(long accountNo) =>
            (await _repository.GetContactsAsync(new[] { accountNo })).GetValueOrDefault(accountNo);

        public Task<List<KosePayments>> GetRecentPaymentsAsync(long accountNo, int take = 10) =>
            _repository.GetRecentPaymentsAsync(accountNo, take);

        public async Task<CollectionCaseRow?> GetOpenCaseAsync(long accountNo)
        {
            try
            {
                var open = (await _repository.GetCasesAsync())
                    .FirstOrDefault(c => c.AccountNo == accountNo && c.Status == CollectionCaseStatus.Open);
                if (open == null)
                {
                    return null;
                }

                var periods = await _repository.GetPeriodsAsync(caseId: open.CaseId);
                var standings = (await _repository.GetStandingsAsync(new[] { accountNo })).ToDictionary(s => s.AccountNo, s => s);
                return (await EnrichAsync(new List<CollectionCaseRow> { open }, periods, standings)).First();
            }
            catch (SqlException ex) when (ex.Number is 208 or 207)
            {
                return null;
            }
        }

        // Every case with its stints, recoveries, flags and current deduction.
        private async Task<List<CollectionCaseRow>> BuildCasesAsync(Dictionary<long, CollectionContractStanding> standings)
        {
            var cases = await _repository.GetCasesAsync();
            var periods = await _repository.GetPeriodsAsync();
            return await EnrichAsync(cases, periods, standings);
        }

        private async Task<List<CollectionCaseRow>> EnrichAsync(List<CollectionCaseRow> cases, List<CollectionPeriodRow> periods,
            Dictionary<long, CollectionContractStanding> standings)
        {
            var byCase = periods.ToLookup(p => p.CaseId);
            var flags = (await _repository.GetFlagsAsync(openOnly: true)).ToLookup(f => f.CaseId);
            var terms = await _reportRepository.GetCollectionTermsAsync();
            var now = DateTime.Now;

            foreach (var c in cases)
            {
                var stints = byCase[c.CaseId].ToList();
                var current = stints.FirstOrDefault(p => p.EndAt == null);
                c.Recovered = stints.Sum(p => p.Recovered);
                c.RecoveredThisMonth = stints.Sum(p => p.RecoveredThisMonth);
                c.LastPaymentAt = stints.Max(p => p.LastPaymentAt);
                if (current != null)
                {
                    c.CollectorUserId = current.CollectorUserId;
                    c.CollectorName = current.CollectorName;
                    c.HeldSince = current.StartAt;
                    c.RecoveredByHolder = current.Recovered;
                    var since = current.LastPaymentAt.HasValue && current.LastPaymentAt > current.StartAt ? current.LastPaymentAt.Value : current.StartAt;
                    c.DaysWithoutPaymentWhileHeld = Math.Max(0, (int)(now - since).TotalDays);
                    c.DueForReassignment = c.Status == CollectionCaseStatus.Open && CollectionsRules.IsDueForReassignment(c.DaysWithoutPaymentWhileHeld);
                }
                else
                {
                    var last = stints.OrderByDescending(p => p.StartAt).FirstOrDefault();
                    c.CollectorUserId = last?.CollectorUserId;
                    c.CollectorName = last?.CollectorName;
                }

                var caseFlags = flags[c.CaseId].ToList();
                c.OpenFlagCount = caseFlags.Count;
                c.LatestFlagNote = caseFlags.OrderByDescending(f => f.CreatedAt).FirstOrDefault()?.Note;

                var t = terms.GetValueOrDefault(c.ContractId);
                if (t != null && t.CaseId == c.CaseId)
                {
                    var live = standings.TryGetValue(c.AccountNo, out var s) && s.ContractId == c.ContractId ? s.Shortfall : t.ShortfallAtReturn;
                    c.CurrentDeduction = CollectionsRules.Deduction(t, live);
                    c.CollectionCosts = t.IsReturned ? t.CollectorEarned + t.RepossessionCost : 0;
                }
                else
                {
                    // An earlier case on a contract that has been back in
                    // collections since: its figures moved to the newer case.
                    c.CurrentDeduction = 0;
                }
            }

            return cases;
        }

        private static List<CollectorSummary> Summaries(List<CollectionCaseRow> cases, List<CollectorCaseEarning> earnings,
            List<CollectionFlagRow> flags, List<CollectorOption> collectors)
        {
            var ids = cases.Where(c => c.Status == CollectionCaseStatus.Open && c.CollectorUserId.HasValue).Select(c => c.CollectorUserId!.Value)
                .Concat(earnings.Select(e => e.CollectorUserId))
                .Concat(collectors.Select(c => c.UserId))
                .Distinct();

            return ids.Select(id =>
                {
                    var held = cases.Where(c => c.Status == CollectionCaseStatus.Open && c.CollectorUserId == id).ToList();
                    var mine = earnings.Where(e => e.CollectorUserId == id).ToList();
                    var shortfall = held.Sum(c => c.ShortfallAtHandover);
                    var recoveredOnHeld = held.Sum(c => c.RecoveredByHolder);
                    return new CollectorSummary
                    {
                        CollectorUserId = id,
                        Name = collectors.FirstOrDefault(c => c.UserId == id)?.Name
                               ?? held.Select(c => c.CollectorName).FirstOrDefault(n => n != null)
                               ?? $"Collector {id}",
                        CasesHeld = held.Count,
                        ShortfallAtHandover = shortfall,
                        Recovered = mine.Sum(e => e.Recovered),
                        RecoveredThisMonth = mine.Sum(e => e.RecoveredThisMonth),
                        RecoveryRatePct = shortfall > 0 ? Math.Round(recoveredOnHeld * 100m / shortfall, 1) : 0,
                        Earned = mine.Sum(e => e.Earned),
                        Paid = mine.Sum(e => e.Paid),
                        Due = mine.Sum(e => e.Due),
                        DueForReassignment = held.Count(c => c.DueForReassignment),
                        OpenFlags = flags.Count(f => f.CollectorUserId == id),
                    };
                })
                .OrderByDescending(s => s.CasesHeld)
                .ThenBy(s => s.Name)
                .ToList();
        }

        // What was recovered on a case so far and the collectors' share of it.
        private async Task<(decimal Recovered, decimal CollectorEarned)> CaseRecoveryAsync(int caseId)
        {
            var stints = await _repository.GetPeriodsAsync(caseId: caseId);
            var recovered = stints.Sum(p => p.Recovered);
            var earned = stints.GroupBy(p => p.CollectorUserId).Sum(g => CollectionsRules.CollectorShare(g.Sum(p => p.Recovered)));
            return (recovered, earned);
        }

        private static string Message(string done, List<string> skipped) =>
            skipped.Count == 0
                ? done
                : $"{done} {skipped.Count} skipped: " + string.Join("; ", skipped.Take(8)) + (skipped.Count > 8 ? $"; and {skipped.Count - 8} more" : "") + ".";
    }
}
