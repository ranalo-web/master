using Ranalo.Services;

namespace Ranalo.ScheduledServices
{
    // Works out the default Financials page (this month) every day at 04:00
    // Kenya time, when the database is quiet, and two minutes after the app
    // starts (a minute after the Admin Dashboard's start-up build, so the two
    // don't run together). See FinancialsCache.
    public class ScheduledFinancialsRefresh : BackgroundService
    {
        private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(2);
        public static readonly TimeSpan KenyaRunTime = TimeSpan.FromHours(4);
        private static readonly TimeSpan KenyaOffset = TimeSpan.FromHours(3);

        private readonly IFinancialsCache _financials;
        private readonly ILogger<ScheduledFinancialsRefresh> _logger;

        public ScheduledFinancialsRefresh(IFinancialsCache financials, ILogger<ScheduledFinancialsRefresh> logger)
        {
            _financials = financials;
            _logger = logger;
        }

        // The next 04:00 Kenya time strictly after nowUtc.
        public static DateTime NextRunUtc(DateTime nowUtc)
        {
            var kenyaNow = nowUtc + KenyaOffset;
            var run = kenyaNow.Date + KenyaRunTime;
            if (run <= kenyaNow) run = run.AddDays(1);
            return DateTime.SpecifyKind(run - KenyaOffset, DateTimeKind.Utc);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var delay = StartupDelay;
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(delay, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }

                try
                {
                    var started = DateTime.UtcNow;
                    await _financials.GetAsync(refresh: true);
                    _logger.LogInformation("Financials rebuilt in {Seconds:0.0}s", (DateTime.UtcNow - started).TotalSeconds);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Financials rebuild failed; the page keeps the previous copy");
                }

                var now = DateTime.UtcNow;
                delay = NextRunUtc(now) - now;
            }
        }
    }
}
