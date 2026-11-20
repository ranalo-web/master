using Ranalo.DataStore;

namespace Ranalo.ScheduledServices
{
    // Every 10 minutes: accounts whose phone an agent enrolled are assigned
    // to that agent once their contract exists, so the agent's reports and
    // commission show their own sales (AccountAssignmentRepository.AssignAgentSalesAsync).
    public class ScheduledAgentSalesAssignment : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);

        private readonly ILogger<ScheduledAgentSalesAssignment> _logger;
        private readonly IServiceScopeFactory _scopeFactory;

        public ScheduledAgentSalesAssignment(ILogger<ScheduledAgentSalesAssignment> logger, IServiceScopeFactory scopeFactory)
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
                    await Task.Delay(Interval, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }

                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var assigned = await scope.ServiceProvider.GetRequiredService<IAccountAssignmentRepository>().AssignAgentSalesAsync();
                    if (assigned > 0)
                    {
                        _logger.LogInformation("Agent sales: {assigned} account(s) assigned to the agent who enrolled them", assigned);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error assigning agent sales.");
                }
            }
        }
    }
}
