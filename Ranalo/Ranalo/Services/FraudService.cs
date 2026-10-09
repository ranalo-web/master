using Ranalo.DataStore;
using Ranalo.Models;

namespace Ranalo.Services
{
    public interface IFraudService
    {
        Task<FraudViewModel> GetAsync(string? check, int? dealerId, string status);
        Task SaveReviewAsync(string checkCode, string subjectKey, string status, string? notes, int userId);
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
