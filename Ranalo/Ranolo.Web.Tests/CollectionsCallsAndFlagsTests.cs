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
