using Ranalo.DataStore.DataModels;
using Ranalo.Models;

namespace Ranalo.DataStore
{
    public interface IAccountWatchlistRepository
    {
        // dealerId/agentUserId scope the list: both null = everything (Admin),
        // dealerId set = that dealer's accounts (Dealer), agentUserId set =
        // that agent's assigned accounts (Agent).
        Task<List<AccountWatchlistListItem>> GetActiveWatchlistAsync(int? dealerId, int? agentUserId);

        // For the permission check before removal -- who added this entry, and
        // with what role, without pulling the full display-joined row.
        Task<(int AddedByUserId, UserRole AddedByRole)?> GetActiveEntryOwnerAsync(int watchlistId);

        // Returns false if the account already has an active entry (unique
        // index violation caught here, not left to the caller).
        Task<bool> AddAsync(long accountId, int addedByUserId, UserRole addedByRole);

        Task<bool> RemoveAsync(int watchlistId, int removedByUserId);
    }
}
