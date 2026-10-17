using Ranalo.Models;
using Ranalo.Services;

namespace Ranolo.Web.Tests
{
    // Completed contracts: closed contracts and their details (2026-10-16).
    public class CompletedContractRulesTests
    {
        private static readonly DateTime Start = new(2025, 6, 9);

        // A 12-month contract at 100 a day with a 2,000 deposit: worth 38,000.
        private static CompletedContractRow Contract(decimal paid = 38000, int daysTaken = 360, int id = 1, long account = 7353632,
            string? name = "Musyoka", string? releaseStatus = null, int? lockGroup = 1, string? nuovoStatus = "enrolled") => new()
        {
            ContractId = id, AccountNo = account, CustomerName = name, Make = "samsung", Model = "SM-A055F", Imei = "352965712662418",
            DealerName = "Renalo", Deposit = 2000, Daily = 100, TermInMonths = 12, StartDate = Start, EndDate = Start.AddDays(daysTaken),
            Paid = paid, ReleaseStatus = releaseStatus, LockGroup = lockGroup, NuovoStatus = nuovoStatus,
        };

        private static CompletedContractRow Evaluated(CompletedContractRow r)
        {
            CompletedContractRules.Evaluate(r);
            return r;
        }

        [Test]
        public void PaidTheContractValue_IsPaidOff()
        {
            var r = Evaluated(Contract());
            Assert.Multiple(() =>
            {
                Assert.That(r.ContractValue, Is.EqualTo(38000m));
                Assert.That(r.Outcome, Is.EqualTo(CompletedOutcome.PaidOff));
                Assert.That(CompletedContractRules.Owed(r), Is.EqualTo(0m));
            });
        }

        [Test]
        public void AShillingShort_StillCountsAsPaidOff()
        {
            Assert.That(Evaluated(Contract(paid: 37999)).Outcome, Is.EqualTo(CompletedOutcome.PaidOff));
        }

        [Test]
        public void ClosedWithMoneyOwed_IsClosedShort()
        {
            var r = Evaluated(Contract(paid: 30000));
            Assert.Multiple(() =>
            {
                Assert.That(r.Outcome, Is.EqualTo(CompletedOutcome.ClosedShort));
                Assert.That(CompletedContractRules.Owed(r), Is.EqualTo(8000m));
            });
        }

        [Test]
        public void PaidMoreThanTheValue_ShowsTheOverpayment()
        {
            Assert.That(CompletedContractRules.Overpaid(Evaluated(Contract(paid: 38250))), Is.EqualTo(250m));
        }

        [TestCase(360, "on time", false)]
        [TestCase(315, "45 days early", false)]
        [TestCase(359, "1 day early", false)]
        [TestCase(480, "120 days late", true)]
        [TestCase(361, "1 day late", true)]
        public void Timing_IsAgainstTheTermInDays(int daysTaken, string expected, bool late)
        {
            var r = Evaluated(Contract(daysTaken: daysTaken));
            Assert.Multiple(() =>
            {
                Assert.That(r.DaysTaken, Is.EqualTo(daysTaken));
                Assert.That(CompletedContractRules.Timing(r), Is.EqualTo(expected));
                Assert.That(CompletedContractRules.IsLate(r), Is.EqualTo(late));
            });
        }

        [Test]
        public void NoStartDate_NoTiming()
        {
            var r = Contract();
            r.StartDate = null;
            Evaluated(r);
            Assert.Multiple(() =>
            {
                Assert.That(r.DaysTaken, Is.Null);
                Assert.That(CompletedContractRules.Timing(r), Is.EqualTo(""));
            });
        }

        [TestCase("Completed", 1, "enrolled", "Released")]
        [TestCase("Pending", 2, null, "Release pending")]
        [TestCase("Processing", 3, null, "Release pending")]
        [TestCase("Failed", 1, "enrolled", "Release failed")]
        [TestCase(null, 1, "un_registered", "Released from Nuovo")]
        [TestCase(null, 1, "enrolled", "Not released")]
        [TestCase(null, 2, "un_registered", "Not released")]   // Nuovo's status means nothing for a Knox phone
        public void ReleaseLabel(string? task, int lockGroup, string? nuovo, string expected)
        {
            Assert.That(CompletedContractRules.ReleaseLabel(Contract(releaseStatus: task, lockGroup: lockGroup, nuovoStatus: nuovo)), Is.EqualTo(expected));
        }

        [Test]
        public void Build_FiltersSearchesCountsAndPagesNewestFirst()
        {
            var rows = Enumerable.Range(1, 12).Select(i => Contract(id: i, account: 7000000 + i, daysTaken: 300 + i)).ToList();
            rows.Add(Contract(paid: 10000, id: 13, account: 8000013, name: "Short"));

            var first = CompletedContractRules.Build(rows, CompletedFilter.PaidOff, null, 1);
            var second = CompletedContractRules.Build(rows, CompletedFilter.PaidOff, null, 2);
            Assert.Multiple(() =>
            {
                Assert.That(first.Counts[CompletedFilter.PaidOff], Is.EqualTo(12));
                Assert.That(first.Counts[CompletedFilter.ClosedShort], Is.EqualTo(1));
                Assert.That(first.Counts[CompletedFilter.All], Is.EqualTo(13));
                Assert.That(first.Rows, Has.Count.EqualTo(CompletedContractRules.PageSize));
                Assert.That(first.Pages, Is.EqualTo(2));
                Assert.That(first.Rows[0].ContractId, Is.EqualTo(12));   // ended last
                Assert.That(second.Rows.Select(r => r.ContractId), Is.EqualTo(new[] { 2, 1 }));
            });

            var shortOnes = CompletedContractRules.Build(rows, CompletedFilter.ClosedShort, null, 1);
            Assert.That(shortOnes.Rows.Single().CustomerName, Is.EqualTo("Short"));

            var searched = CompletedContractRules.Build(rows, CompletedFilter.All, "8000013", 1);
            Assert.Multiple(() =>
            {
                Assert.That(searched.Rows.Single().ContractId, Is.EqualTo(13));
                Assert.That(searched.Counts[CompletedFilter.All], Is.EqualTo(1));
            });
        }

        [Test]
        public void Build_UnknownFilterAndPageOutOfRange_FallBack()
        {
            var vm = CompletedContractRules.Build(new List<CompletedContractRow> { Contract() }, "nonsense", null, 9);
            Assert.Multiple(() =>
            {
                Assert.That(vm.Filter, Is.EqualTo(CompletedFilter.PaidOff));
                Assert.That(vm.Page, Is.EqualTo(1));
                Assert.That(vm.Rows, Has.Count.EqualTo(1));
            });
        }

        [TestCase("musyoka")]
        [TestCase("7353632")]
        [TestCase("352965712662")]
        [TestCase("A055F")]
        [TestCase("renalo")]
        public void Search_MatchesAccountNamePhoneImeiOrDealer(string q)
        {
            Assert.That(CompletedContractRules.MatchesSearch(Contract(), q), Is.True);
        }
    }
}
