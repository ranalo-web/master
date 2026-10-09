using Ranalo.DataStore;
using Ranalo.Models;

namespace Ranalo.Services
{
    public interface IFraudService
    {
        Task<FraudViewModel> GetAsync(string? check, int? dealerId, string status);
        Task SaveReviewAsync(string checkCode, string subjectKey, string status, string? notes, int userId);
        Task<VerificationViewModel> GetVerificationAsync(int? dealerId, long? agentUserId);
    }

    // Runs the fraud checks (FraudRules) on live data and joins in the
    // admin's reviews. Nothing is cached: ~600 contracts and ~1,100 orders.
    public class FraudService : IFraudService
    {
        private readonly IFraudRepository _repository;

        public FraudService(IFraudRepository repository)
        {
            _repository = repository;
        }

        public async Task<FraudViewModel> GetAsync(string? check, int? dealerId, string status)
        {
            var today = DateTime.Today;
            var (contracts, _, flags) = await RunChecksAsync(today);

            var shown = flags
                .Where(f => check == null || f.CheckCode == check)
                .Where(f => dealerId == null || f.DealerId == dealerId)
                .Where(f => MatchesStatus(f, status))
                .OrderByDescending(f => f.FlaggedOn)
                .ToList();

            return new FraudViewModel
            {
                Check = check,
                DealerId = dealerId,
                Status = status,
                OverallDepositOnlyRate = FraudRules.DepositOnlyRate(contracts, today),
                Checks = FraudRules.Summaries(flags, today),
                Dealers = FraudRules.DealerRisks(contracts, flags, today),
                Flags = shown,
            };
        }

        // A dealer's or agent's own flagged accounts and orders, neutral.
        public async Task<VerificationViewModel> GetVerificationAsync(int? dealerId, long? agentUserId)
        {
            var (contracts, orders, flags) = await RunChecksAsync(DateTime.Today);
            var items = FraudRules.VerificationItems(flags, contracts, orders)
                .Where(i => (dealerId == null || i.DealerId == dealerId) && (agentUserId == null || i.AgentUserId == agentUserId))
                .ToList();
            return new VerificationViewModel { Items = items };
        }

        private async Task<(List<FraudContractRow>, List<FraudOrderRow>, List<FraudFlag>)> RunChecksAsync(DateTime today)
        {
            var contracts = await _repository.GetOpenContractsAsync();
            var orders = await _repository.GetOrdersAsync();
            var nextOfKin = await _repository.GetNextOfKinAsync();
            var devices = await _repository.GetDevicesOnOpenContractsAsync();
            var reviews = (await _repository.GetReviewsAsync())
                .GroupBy(r => (r.CheckCode, r.SubjectKey))
                .ToDictionary(g => g.Key, g => g.First());

            var flags = FraudRules.Evaluate(contracts, orders, nextOfKin, devices, today)
                .GroupBy(f => (f.CheckCode, f.SubjectKey))
                .Select(g => g.First())
                .ToList();
            foreach (var f in flags)
            {
                f.Review = reviews.GetValueOrDefault((f.CheckCode, f.SubjectKey));
            }
            return (contracts, orders, flags);
        }

        public Task SaveReviewAsync(string checkCode, string subjectKey, string status, string? notes, int userId) =>
            _repository.SaveReviewAsync(checkCode, subjectKey, status, notes, userId);

        // "active" = everything not cleared.
        private static bool MatchesStatus(FraudFlag f, string status) => status switch
        {
            "all" => true,
            "active" => f.ReviewStatus != FraudReviewStatus.Cleared,
            _ => string.Equals(f.ReviewStatus, status, StringComparison.OrdinalIgnoreCase),
        };
    }
}
