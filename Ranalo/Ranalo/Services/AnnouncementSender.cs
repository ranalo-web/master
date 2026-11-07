using Ranalo.DataStore;
using Ranalo.Models;
using Ranalo.SumsungKnox;

namespace Ranalo.Services
{
    public record AnnouncementProgress(bool Running, string? MessageType, int Total, int Done,
        DateTime? StartedUtc, DateTime? FinishedUtc, string? StartedBy, string? Error);

    public interface IAnnouncementSender
    {
        AnnouncementProgress Progress { get; }

        // false = one is already going.
        bool TryStart(string title, string body, string startedBy);
    }

    // Sends an announcement to everyone in the background: several hundred
    // phones take longer than a web request may run. In batches of 25 so
    // the logs (and the progress on the page) build up as it goes; one
    // batch failing doesn't stop the rest. One at a time.
    public class AnnouncementSender : IAnnouncementSender
    {
        public const int BatchSize = 25;

        private readonly IServiceScopeFactory _scopes;
        private readonly ILogger<AnnouncementSender> _logger;
        private readonly object _gate = new();
        private AnnouncementProgress _progress = new(false, null, 0, 0, null, null, null, null);

        public AnnouncementSender(IServiceScopeFactory scopes, ILogger<AnnouncementSender> logger)
        {
            _scopes = scopes;
            _logger = logger;
        }

        public AnnouncementProgress Progress
        {
            get { lock (_gate) return _progress; }
        }

        public bool TryStart(string title, string body, string startedBy)
        {
            var type = AnnouncementRules.TypeFor(DateTime.UtcNow);
            lock (_gate)
            {
                if (_progress.Running) return false;
                _progress = new(true, type, 0, 0, DateTime.UtcNow, null, startedBy, null);
            }

            _ = Task.Run(() => RunAsync(type, title, body, startedBy));
            return true;
        }

        private async Task RunAsync(string type, string title, string body, string startedBy)
        {
            var done = 0;
            try
            {
                using var scope = _scopes.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<IAnnouncementsRepository>();
                var sender = scope.ServiceProvider.GetRequiredService<IPaymentReminderService>();
                var payments = scope.ServiceProvider.GetRequiredService<IPaymentsRepository>();
                var knox = scope.ServiceProvider.GetRequiredService<IKnoxGuardClient>();

                var recipients = await repo.GetRecipientsAsync(type);
                Update(p => p with { Total = recipients.Count });
                _logger.LogInformation("Announcement {Type} by {User}: sending to {Count} phones.", type, startedBy, recipients.Count);

                foreach (var batch in recipients.Chunk(BatchSize))
                {
                    var records = batch.Select(a =>
                    {
                        var message = AnnouncementRules.For(title, body, a.FirstName, a.AccountId);
                        return new AccountSendMessage
                        {
                            AccountId = a.AccountId, FirstName = a.FirstName ?? "", Imei = a.Imei, LockGroup = a.LockGroup,
                            MessageTitle = message.Title, MessageText = message.Content,
                        };
                    }).ToList();

                    try
                    {
                        await sender.SendToEachProviderAsync(records, type, payments, knox);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Announcement {Type}: a batch of {Count} failed; carrying on.", type, records.Count);
                    }

                    done += records.Count;
                    Update(p => p with { Done = done });
                }

                _logger.LogInformation("Announcement {Type} finished: {Done} phones.", type, done);
                Update(p => p with { Running = false, FinishedUtc = DateTime.UtcNow });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Announcement {Type} stopped after {Done} phones.", type, done);
                Update(p => p with { Running = false, FinishedUtc = DateTime.UtcNow, Error = ex.Message });
            }
        }

        private void Update(Func<AnnouncementProgress, AnnouncementProgress> change)
        {
            lock (_gate) _progress = change(_progress);
        }
    }
}
