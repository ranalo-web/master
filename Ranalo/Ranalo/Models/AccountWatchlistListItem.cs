using Ranalo.DataStore.DataModels;

namespace Ranalo.Models
{
    // A row in the "Watchlist" page/list -- a manually-flagged account, not a
    // computed classification. See Database/Watchlist/001_create_account_watchlist.sql.
    public class AccountWatchlistListItem
    {
        public int Id { get; set; }
        public long AccountId { get; set; }
        public string CustomerName { get; set; } = "";
        public int? DealerId { get; set; }
        public string? DealerName { get; set; }
        public int? AssignedAgentId { get; set; }
        public string? AgentName { get; set; }
        public int AddedByUserId { get; set; }
        public string AddedByName { get; set; } = "";
        public UserRole AddedByRole { get; set; }
        public DateTime AddedAtUtc { get; set; }

        // Computed server-side per viewer -- not stored. See
        // AccountWatchlistService.CanRemove for the permission rule.
        public bool CanCurrentUserRemove { get; set; }
    }
}
