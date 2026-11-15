using Ranalo.Services.DeviceLock;

namespace Ranalo.ScheduledServices
{
    // Every 5 minutes: Transsion phones still waiting to be switched on are
    // checked with Transsion (CheckActivationsAsync), then enrolments on
    // screened orders are approved without a person once their phone has
    // reached Knox (EnrolmentApprovalService.SweepAsync).
    public class ScheduledEnrolmentAutoApproval : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

        private readonly ILogger<ScheduledEnrolmentAutoApproval> _logger;
        private readonly IServiceScopeFactory _scopeFactory;

        public ScheduledEnrolmentAutoApproval(ILogger<ScheduledEnrolmentAutoApproval> logger, IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Enrolment auto-approval started at {time} (UTC)", DateTime.UtcNow);

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
                    var approvals = scope.ServiceProvider.GetRequiredService<IEnrolmentApprovalService>();

                    // First: Transsion phones that are on but whose activation
                    // webhook never reached us, so this run can unlock them.
                    var activated = await approvals.CheckActivationsAsync();
                    if (activated > 0)
                    {
                        _logger.LogInformation("Enrolment auto-approval: {activated} Transsion phone(s) found active without a webhook", activated);
                    }

                    var approved = await approvals.SweepAsync();
                    if (approved > 0)
                    {
                        _logger.LogInformation("Enrolment auto-approval: {approved} approved", approved);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error running the enrolment auto-approval.");
                }
            }

            _logger.LogInformation("Enrolment auto-approval stopped at {time} (UTC)", DateTime.UtcNow);
        }
    }
}
