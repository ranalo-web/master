using Ranalo.DataStore.DataModels;
using Ranalo.Services;

namespace Ranolo.Web.Tests
{
    public class AccountWatchlistServiceTests
    {
        [Test]
        public void CanRemove_SameAdder_CanAlwaysRemoveOwnEntry()
        {
            Assert.That(AccountWatchlistService.CanRemove(UserRole.Agent, viewerUserId: 5, UserRole.Agent, addedByUserId: 5), Is.True);
        }

        [Test]
        public void CanRemove_AdminAddedEntry_DealerCannotRemove()
        {
            Assert.That(AccountWatchlistService.CanRemove(UserRole.Dealer, viewerUserId: 2, UserRole.Admin, addedByUserId: 1), Is.False);
        }

        [Test]
        public void CanRemove_AdminAddedEntry_AgentCannotRemove()
        {
            Assert.That(AccountWatchlistService.CanRemove(UserRole.Agent, viewerUserId: 3, UserRole.Admin, addedByUserId: 1), Is.False);
        }

        [Test]
        public void CanRemove_DealerAddedEntry_AgentCannotRemove()
        {
            Assert.That(AccountWatchlistService.CanRemove(UserRole.Agent, viewerUserId: 3, UserRole.Dealer, addedByUserId: 2), Is.False);
        }

        [Test]
        public void CanRemove_DealerAddedEntry_AdminCanRemove()
        {
            Assert.That(AccountWatchlistService.CanRemove(UserRole.Admin, viewerUserId: 1, UserRole.Dealer, addedByUserId: 2), Is.True);
        }

        [Test]
        public void CanRemove_AgentAddedEntry_TheirDealerCanRemove()
        {
            Assert.That(AccountWatchlistService.CanRemove(UserRole.Dealer, viewerUserId: 2, UserRole.Agent, addedByUserId: 3), Is.True);
        }

        [Test]
        public void CanRemove_AgentAddedEntry_AdminCanRemove()
        {
            Assert.That(AccountWatchlistService.CanRemove(UserRole.Admin, viewerUserId: 1, UserRole.Agent, addedByUserId: 3), Is.True);
        }

        [Test]
        public void CanRemove_DifferentAgentSameRank_CannotRemoveEachOthersEntries()
        {
            Assert.That(AccountWatchlistService.CanRemove(UserRole.Agent, viewerUserId: 4, UserRole.Agent, addedByUserId: 3), Is.False);
        }

        [Test]
        public void CanRemove_DifferentDealerSameRank_CannotRemoveEachOthersEntries()
        {
            Assert.That(AccountWatchlistService.CanRemove(UserRole.Dealer, viewerUserId: 9, UserRole.Dealer, addedByUserId: 2), Is.False);
        }
    }
}
