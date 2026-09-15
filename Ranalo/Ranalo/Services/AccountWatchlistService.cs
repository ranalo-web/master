using Ranalo.DataStore;
using Ranalo.DataStore.DataModels;
using Ranalo.Models;

namespace Ranalo.Services
{
    public class AccountWatchlistService : IAccountWatchlistService
    {
        private readonly IAccountWatchlistRepository _repository;

        public AccountWatchlistService(IAccountWatchlistRepository repository)
        {
            _repository = repository;
        }

        public async Task<List<AccountWatchlistListItem>> GetWatchlistForViewerAsync(User viewer)
        {
            int? dealerId = viewer.RoleId == UserRole.Dealer ? viewer.DealerId : null;
            int? agentUserId = viewer.RoleId == UserRole.Agent ? viewer.UserId : null;

            var items = await _repository.GetActiveWatchlistAsync(dealerId, agentUserId);

            foreach (var item in items)
            {
                item.CanCurrentUserRemove = CanRemove(viewer.RoleId, viewer.UserId, item.AddedByRole, item.AddedByUserId);
            }

            return items;
        }

        public Task<bool> AddToWatchlistAsync(long accountId, User addedBy) =>
            _repository.AddAsync(accountId, addedBy.UserId, addedBy.RoleId);

        public async Task<bool> RemoveFromWatchlistAsync(int watchlistId, User removedBy)
        {
            var owner = await _repository.GetActiveEntryOwnerAsync(watchlistId);
            if (owner == null)
            {
                return false;
            }

            if (!CanRemove(removedBy.RoleId, removedBy.UserId, owner.Value.AddedByRole, owner.Value.AddedByUserId))
            {
                return false;
            }

            return await _repository.RemoveAsync(watchlistId, removedBy.UserId);
        }

        // An entry can be removed by whoever added it, or by a strictly
        // higher rank (Admin > Dealer > Agent). A same-rank user who isn't
        // the original adder (e.g. a different dealer, or a different agent)
        // cannot remove it.
        public static bool CanRemove(UserRole viewerRole, int viewerUserId, UserRole addedByRole, int addedByUserId)
        {
            if (viewerUserId == addedByUserId)
            {
                return true;
            }

            return Rank(viewerRole) > Rank(addedByRole);
        }

        private static int Rank(UserRole role) => role switch
        {
            UserRole.Admin => 3,
            UserRole.Dealer => 2,
            UserRole.Agent => 1,
            _ => 0,
        };
    }
}
