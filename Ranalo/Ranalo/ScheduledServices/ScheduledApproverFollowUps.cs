using Ranalo.DataStore;
using Ranalo.Models;
using Ranalo.Services;

namespace Ranalo.ScheduledServices
{
    // Every 4 hours (ApproverFollowUpRules): new pool accounts go to the
    // Approvers; active follow-ups that are finished -- paying again, a
    // collections case opened, the contract ended -- are closed. Uses the
    // same standings and cases as the Collections pages.
    public class ScheduledApproverFollowUps : BackgroundService
    {
        private readonly ILogger<ScheduledApproverFollowUps> _logger;
        private readonly IServiceScopeFactory _scopeFactory;

        public ScheduledApproverFollowUps(ILogger<ScheduledApproverFollowUps> logger, IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var (added, closed) = await SyncAsync(
                        scope.ServiceProvider.GetRequiredService<ICollectionsRepository>(),
                        scope.ServiceProvider.GetRequiredService<IApproverFollowUpRepository>());
                    if (added + closed > 0) _logger.LogInformation("Approver follow-ups: {Added} added, {Closed} closed", added, closed);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Approver follow-ups sync failed.");
                }

                try
                {
                    await Task.Delay(ApproverFollowUpRules.JobInterval, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    // Shutting down.
                }
            }
        }

        public static async Task<(int Added, int Closed)> SyncAsync(ICollectionsRepository collections, IApproverFollowUpRepository followUps)
        {
            var standings = (await collections.GetStandingsAsync()).GroupBy(s => s.ContractId).ToDictionary(g => g.Key, g => g.First());
            var openCaseIds = (await collections.GetCasesAsync())
                .Where(c => c.Status == CollectionCaseStatus.Open)
                .GroupBy(c => c.ContractId).ToDictionary(g => g.Key, g => g.Max(c => c.CaseId));
            var openCases = openCaseIds.Keys.ToHashSet();
            var active = await followUps.GetActiveAsync();

            var closed = 0;
            foreach (var f in active)
            {
                var reason = ApproverFollowUpRules.CloseReason(standings.GetValueOrDefault(f.ContractId), openCases.Contains(f.ContractId));
                if (reason != null)
                {
                    // Gone to a collector: the approvers' notes go onto the case first.
                    if (reason == ApproverFollowUpRules.InCollections)
                    {
                        await followUps.CopyNotesToCaseAsync(f.Id, openCaseIds[f.ContractId]);
                    }
                    await followUps.CloseAsync(f.Id, reason);
                    closed++;
                }
            }

            var stillActive = active.Where(f => ApproverFollowUpRules.CloseReason(standings.GetValueOrDefault(f.ContractId), openCases.Contains(f.ContractId)) == null)
                .Select(f => f.ContractId).ToHashSet();
            var added = 0;
            foreach (var s in standings.Values)
            {
                if (ApproverFollowUpRules.ShouldAdd(s, openCases.Contains(s.ContractId), stillActive.Contains(s.ContractId))
                    && await followUps.AddAsync(s.AccountNo, s.ContractId, s.Shortfall, s.DaysSinceLastPayment))
                {
                    added++;
                }
            }
            return (added, closed);
        }
    }
}
