using Microsoft.Extensions.Logging.Abstractions;
using Ranalo.DataStore;
using Ranalo.Models;
using Ranalo.Services;

namespace Ranolo.Web.Tests
{
    // Collectors' "spoke to customer" call notes and "can't reach" flags.
    public class CollectionsNoteRulesTests
    {
        [Test]
        public void CleanNoteTrimsAndSingleSpaces()
        {
            Assert.That(CollectionsRules.CleanNote("  phone   off\n for 2 weeks "), Is.EqualTo("phone off for 2 weeks"));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   \n ")]
        public void CleanNoteOfBlankIsNull(string? note)
        {
            Assert.That(CollectionsRules.CleanNote(note), Is.Null);
        }

        [Test]
        public void CleanNoteCutsAtTheColumnSize()
        {
            Assert.That(CollectionsRules.CleanNote(new string('x', 600))!.Length, Is.EqualTo(CollectionsRules.NoteMaxLength));
        }

        [Test]
        public void SameOpenNoteIsARepeat()
        {
            // The double click that put 18 flags on one account.
            Assert.That(CollectionsRules.IsRepeatFlag(new[] { "phone stolen" }, "Phone  stolen "), Is.True);
        }

        [Test]
        public void DifferentNoteIsNotARepeat()
        {
            Assert.That(CollectionsRules.IsRepeatFlag(new[] { "phone stolen" }, "moved to Mombasa"), Is.False);
            Assert.That(CollectionsRules.IsRepeatFlag(Array.Empty<string>(), "phone stolen"), Is.False);
        }

        [Test]
        public void PromiseDateTodayOrLaterOrNone()
        {
            var today = new DateTime(2026, 10, 12);
            Assert.That(CollectionsRules.IsValidPromiseDate(null, today), Is.True);
            Assert.That(CollectionsRules.IsValidPromiseDate(today, today), Is.True);
            Assert.That(CollectionsRules.IsValidPromiseDate(today.AddDays(7), today), Is.True);
            Assert.That(CollectionsRules.IsValidPromiseDate(today.AddDays(-1), today), Is.False);
        }
    }

    public class CollectionsServiceNotesTests
    {
        private const int CaseId = 374;
        private const int Collector = 32;

        private List<CollectionPeriodRow> _periods = null!;
        private List<CollectionFlagRow> _openFlags = null!;
        private Dictionary<int, CollectionCallNote> _calls = null!;
        private bool _callTableExists;
        private InterfaceFake<ICollectionsRepository> _repo = null!;
        private CollectionsService _service = null!;

        [SetUp]
        public void SetUp()
        {
            _periods = new List<CollectionPeriodRow>
            {
                new() { AssignmentId = 1, CaseId = CaseId, CollectorUserId = Collector, StartAt = new DateTime(2026, 10, 1) },
            };
            _openFlags = new List<CollectionFlagRow>();
            _calls = new Dictionary<int, CollectionCallNote>();
            _callTableExists = true;

            var (repo, fake) = InterfaceFake<ICollectionsRepository>.Create(new()
            {
                // Filtered by (collector, case) as the real query is.
                ["GetPeriodsAsync"] = a => Task.FromResult(_periods
                    .Where(p => a![0] == null || p.CollectorUserId == (int)a[0]!)
                    .Where(p => a![1] == null || p.CaseId == (int)a[1]!)
                    .ToList()),
                ["GetFlagsAsync"] = _ => Task.FromResult(_openFlags.ToList()),
                ["AddFlagAsync"] = _ => Task.CompletedTask,
                ["AddCallNoteAsync"] = _ => Task.FromResult(_callTableExists),
                ["GetLatestCallNotesAsync"] = _ => Task.FromResult(_calls),
                ["GetCasesAsync"] = _ => Task.FromResult(new List<CollectionCaseRow>
                {
                    new() { CaseId = CaseId, AccountNo = 9162191, ContractId = 1, Status = CollectionCaseStatus.Open },
                }),
                ["GetStandingsAsync"] = _ => Task.FromResult(new List<CollectionContractStanding>()),
            });
            _repo = fake;

            var (reports, _) = InterfaceFake<IDashboardReportRepository>.Create(new()
            {
                ["GetCollectionTermsAsync"] = _ => Task.FromResult(new Dictionary<long, CollectionTerms>()),
            });
            var (dealers, _) = InterfaceFake<IDealerAllocationRepository>.Create(new());

            _service = new CollectionsService(repo, reports, dealers, NullLogger<CollectionsService>.Instance);
        }

        private object?[]? Saved(string method) => _repo.Calls.SingleOrDefault(c => c.Method == method).Args;

        [Test]
        public async Task FlagIsSavedCleanedWithAConfirmation()
        {
            var (ok, message) = await _service.AddFlagAsync(CaseId, Collector, "  phone   stolen ");

            Assert.That(ok, Is.True);
            Assert.That(message, Does.Contain("Flagged for the admin team"));
            Assert.That(Saved("AddFlagAsync"), Is.EqualTo(new object[] { CaseId, Collector, "phone stolen" }));
        }

        [Test]
        public async Task RepeatFlagIsNotSavedAgain()
        {
            _openFlags.Add(new CollectionFlagRow { CaseId = CaseId, CollectorUserId = Collector, Note = "phone stolen" });

            var (ok, message) = await _service.AddFlagAsync(CaseId, Collector, "phone stolen");

            Assert.That(ok, Is.True);
            Assert.That(message, Does.Contain("Already flagged"));
            Assert.That(Saved("AddFlagAsync"), Is.Null);
        }

        [Test]
        public async Task SameNoteFromAnotherCollectorOrCaseIsStillSaved()
        {
            _openFlags.Add(new CollectionFlagRow { CaseId = CaseId, CollectorUserId = 99, Note = "phone stolen" });
            _openFlags.Add(new CollectionFlagRow { CaseId = 1, CollectorUserId = Collector, Note = "phone stolen" });

            await _service.AddFlagAsync(CaseId, Collector, "phone stolen");

            Assert.That(Saved("AddFlagAsync"), Is.Not.Null);
        }

        [Test]
        public async Task CantFlagAnAccountYouDontHold()
        {
            _periods[0].EndAt = new DateTime(2026, 10, 5);

            var (ok, message) = await _service.AddFlagAsync(CaseId, Collector, "phone stolen");

            Assert.That(ok, Is.False);
            Assert.That(message, Does.Contain("isn't assigned to you"));
            Assert.That(Saved("AddFlagAsync"), Is.Null);
        }

        [Test]
        public async Task BlankFlagIsRefused()
        {
            var (ok, _) = await _service.AddFlagAsync(CaseId, Collector, "   ");

            Assert.That(ok, Is.False);
            Assert.That(Saved("AddFlagAsync"), Is.Null);
        }

        [Test]
        public async Task CallNoteIsSavedWithThePromisedDate()
        {
            var promised = DateTime.Today.AddDays(5);

            var (ok, message) = await _service.AddCallNoteAsync(CaseId, Collector, " will pay  500 on Friday ", promised);

            Assert.That(ok, Is.True);
            Assert.That(message, Does.Contain("promised to pay by"));
            Assert.That(Saved("AddCallNoteAsync"), Is.EqualTo(new object?[] { CaseId, Collector, "will pay 500 on Friday", promised }));
        }

        [Test]
        public async Task CallNoteWithoutAPromiseIsSaved()
        {
            var (ok, message) = await _service.AddCallNoteAsync(CaseId, Collector, "discussed arrears", null);

            Assert.That(ok, Is.True);
            Assert.That(message, Is.EqualTo("Call saved."));
        }

        [Test]
        public async Task CallNoteWithAPastPromiseIsRefused()
        {
            var (ok, message) = await _service.AddCallNoteAsync(CaseId, Collector, "will pay", DateTime.Today.AddDays(-1));

            Assert.That(ok, Is.False);
            Assert.That(message, Does.Contain("can't be in the past"));
            Assert.That(Saved("AddCallNoteAsync"), Is.Null);
        }

        [Test]
        public async Task BlankCallNoteIsRefused()
        {
            var (ok, _) = await _service.AddCallNoteAsync(CaseId, Collector, "", null);

            Assert.That(ok, Is.False);
            Assert.That(Saved("AddCallNoteAsync"), Is.Null);
        }

        [Test]
        public async Task CantAddACallNoteToAnAccountYouDontHold()
        {
            var (ok, _) = await _service.AddCallNoteAsync(CaseId, collectorUserId: 99, "will pay", null);

            Assert.That(ok, Is.False);
            Assert.That(Saved("AddCallNoteAsync"), Is.Null);
        }

        [Test]
        public async Task CallNoteBeforeTheTableExistsSaysSo()
        {
            _callTableExists = false;

            var (ok, message) = await _service.AddCallNoteAsync(CaseId, Collector, "will pay", null);

            Assert.That(ok, Is.False);
            Assert.That(message, Does.Contain("aren't switched on yet"));
        }

        [Test]
        public async Task OpenCaseShowsTheLatestCall()
        {
            _calls[CaseId] = new CollectionCallNote { CaseId = CaseId, CollectorUserId = Collector, Note = "will pay Friday", CreatedAt = DateTime.Today };

            var open = await _service.GetOpenCaseAsync(9162191);

            Assert.That(open!.LastCall!.Note, Is.EqualTo("will pay Friday"));
        }

        [Test]
        public async Task OpenCaseWithNoCallsHasNoLastCall()
        {
            var open = await _service.GetOpenCaseAsync(9162191);

            Assert.That(open!.LastCall, Is.Null);
        }
    }
}

namespace Ranolo.Web.Tests
{
    // My Collections search box.
    public class CollectionsSearchTests
    {
        private static CollectionCaseRow Case() => new()
        {
            AccountNo = 7320500,
            CustomerName = "Austine Otieno",
            Contact = new AccountContact
            {
                AccountNo = 7320500,
                CustomerPhone = "0712345678",
                NextOfKinName = "Mary Achieng",
                NextOfKinPhone = "254722111222",
                NextOfKin2Name = "John Ouma",
                NextOfKin2Phone = "+254 733 999 888",
            },
        };

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void BlankSearchMatchesEverything(string? term)
        {
            Assert.That(CollectionsRules.MatchesSearch(Case(), term), Is.True);
        }

        [TestCase("7320500")]
        [TestCase("73205")]
        [TestCase("austine")]
        [TestCase("ACHIENG")]
        [TestCase("john ouma")]
        public void MatchesAccountAndNames(string term)
        {
            Assert.That(CollectionsRules.MatchesSearch(Case(), term), Is.True);
        }

        [TestCase("0712345678")]
        [TestCase("254712345678")]
        [TestCase("+254 712 345 678")]
        [TestCase("0722111222")]   // first next of kin, stored as 254...
        [TestCase("0733 999888")]  // second next of kin, stored as +254 ...
        public void MatchesPhonesWrittenAnyWay(string term)
        {
            Assert.That(CollectionsRules.MatchesSearch(Case(), term), Is.True);
        }

        [TestCase("Wanjiru")]
        [TestCase("0799000000")]
        [TestCase("12")]           // too short to search phones by
        public void NoMatch(string term)
        {
            Assert.That(CollectionsRules.MatchesSearch(Case(), term), Is.False);
        }

        [Test]
        public void CaseWithNoContactStillSearchesByNameAndAccount()
        {
            var c = new CollectionCaseRow { AccountNo = 9162191, CustomerName = "Peter" };
            Assert.That(CollectionsRules.MatchesSearch(c, "peter"), Is.True);
            Assert.That(CollectionsRules.MatchesSearch(c, "0712345678"), Is.False);
        }
    }
}

namespace Ranolo.Web.Tests
{
    public class NextOfKinListTests
    {
        private static AccountContact Contact(string? n1, string? p1, string? n2, string? p2) => new()
        {
            NextOfKinName = n1, NextOfKinPhone = p1, NextOfKinIdNumber = n1 == null ? null : "111",
            NextOfKin2Name = n2, NextOfKin2Phone = p2, NextOfKin2IdNumber = n2 == null ? null : "222",
        };

        [Test]
        public void BothShownFirstThenSecondWithTheirDetails()
        {
            var kin = CollectionsRules.NextOfKin(Contact("Mary", "0722111222", "John", "0733999888"));

            Assert.That(kin.Select(k => (k.Name, k.Phone, k.IdNumber)), Is.EqualTo(new[]
            {
                ("Mary", "0722111222", "111"), ("John", "0733999888", "222"),
            }));
        }

        [Test]
        public void SamePersonStoredTwiceShowsOnce()
        {
            // Seen on a real account: "Diana Rose 0740308949" as both.
            Assert.That(CollectionsRules.NextOfKin(Contact("Diana Rose", "0740308949", "Diana Rose", "0740308949")), Has.Count.EqualTo(1));
            Assert.That(CollectionsRules.NextOfKin(Contact("Diana", "0740308949", "D. Rose", "254740308949")), Has.Count.EqualTo(1), "same phone, written differently");
            Assert.That(CollectionsRules.NextOfKin(Contact("Diana", null, "diana ", null)), Has.Count.EqualTo(1), "same name, no phones");
        }

        [Test]
        public void SameNameDifferentPhonesAreTwoPeople()
        {
            Assert.That(CollectionsRules.NextOfKin(Contact("John", "0711000000", "John", "0722000000")), Has.Count.EqualTo(2));
        }

        [Test]
        public void OnlySecondOnFileStillShows()
        {
            var kin = CollectionsRules.NextOfKin(Contact(null, null, "John", "0733999888"));
            Assert.That(kin.Single().Name, Is.EqualTo("John"));
        }

        [Test]
        public void NothingOnFile()
        {
            Assert.That(CollectionsRules.NextOfKin(null), Is.Empty);
            Assert.That(CollectionsRules.NextOfKin(Contact(null, null, null, null)), Is.Empty);
        }
    }
}
