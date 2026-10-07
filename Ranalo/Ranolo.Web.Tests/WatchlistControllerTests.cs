using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Ranalo.Controllers;
using Ranalo.DataStore.DataModels;
using Ranalo.Models;
using Ranalo.Services;

namespace Ranolo.Web.Tests
{
    public class WatchlistControllerTests
    {
        private static (WatchlistController Controller, List<string?> Searches) Create(UserRole role)
        {
            var searches = new List<string?>();
            var (reports, _) = InterfaceFake<IApplicationReportService>.Create(new()
            {
                ["GetAllAccountsAsync"] = args =>
                {
                    searches.Add((string?)args![1]);
                    return Task.FromResult(new AllAccountsViewModel
                    {
                        Accounts = new List<AllAccounts> { new() { AccountNo = 30111222, CustomerName = "Jane" } }
                    });
                }
            });
            var (watchlist, _) = InterfaceFake<IAccountWatchlistService>.Create(new()
            {
                ["GetWatchlistForViewerAsync"] = _ => Task.FromResult(new List<AccountWatchlistListItem>())
            });

            var controller = new WatchlistController(watchlist, reports);
            var context = new DefaultHttpContext();
            context.Items["UserSettings"] = new User { UserId = 1, RoleId = role, DealerId = 5 };
            controller.ControllerContext = new ControllerContext { HttpContext = context };
            controller.TempData = new TempDataDictionary(context, new NullTempDataProvider());
            return (controller, searches);
        }

        private sealed class NullTempDataProvider : ITempDataProvider
        {
            public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
            public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
        }

        [TestCase("  30111222 ", "30111222")]
        [TestCase("\t30111222\n", "30111222")]
        [TestCase(" Jane ", "Jane")]
        public async Task Search_TrimsWhatWasTyped(string typed, string searched)
        {
            var (controller, searches) = Create(UserRole.Admin);

            var result = await controller.Index(typed) as ViewResult;

            Assert.That(searches, Is.EqualTo(new[] { searched }));
            var model = (WatchlistViewModel)result!.Model!;
            Assert.That(model.SearchTerm, Is.EqualTo(searched));
            Assert.That(model.SearchResults, Has.Count.EqualTo(1));
        }

        [TestCase("")]
        [TestCase("   ")]
        public async Task BlankSearch_DoesNotSearch(string typed)
        {
            var (controller, searches) = Create(UserRole.Admin);
            await controller.Index(typed);
            Assert.That(searches, Is.Empty);
        }

        [TestCase(UserRole.Customer)]
        [TestCase(UserRole.Collector)]
        public async Task RolesWithoutWatchlist_AreSentAway(UserRole role)
        {
            var (controller, searches) = Create(role);
            var result = await controller.Index("30111222");
            Assert.That(result, Is.TypeOf<RedirectToActionResult>());
            Assert.That(searches, Is.Empty);
        }
    }
}
