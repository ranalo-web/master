using Ranalo.DataStore.DataModels;
using Ranalo.Models;

namespace Ranalo.Services
{
    public interface IAccountWatchlistService
    {
        // Scoped by the viewer's role: Admin sees everything, Dealer sees
        // their dealer's accounts, Agent sees their assigned accounts.
        Task<List<AccountWatchlistListItem>> GetWatchlistForViewerAsync(User viewer);

        Task<bool> AddToWatchlistAsync(long accountId, User addedBy);

        // Returns false if the entry doesn't exist / is already removed, or
        // the current user isn't permitted to remove it (see CanRemove).
        Task<bool> RemoveFromWatchlistAsync(int watchlistId, User removedBy);
    }
}
