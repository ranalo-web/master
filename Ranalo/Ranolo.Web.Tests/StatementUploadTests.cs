using Ranalo.Models;
using Ranalo.Services;

namespace Ranolo.Web.Tests
{
    // Statement Uploads report: which statement periods are in, and the gaps (2026-10-19).
    public class StatementUploadTests
    {
        private static StatementUpload Up(string from, string to, int added = 0, string? error = null) => new()
        {
            FileName = "statement.xlsx",
            UploadedAtUtc = DateTime.Parse(to).AddDays(1),
            PeriodFrom = DateTime.Parse(from).AddHours(8),
            PeriodTo = DateTime.Parse(to).AddHours(20),
            NewPayments = added,
            Error = error,
        };

        [Test]
        public void ParseTime_MpesaFormats()
        {
            Assert.That(StatementUploadRules.ParseTime("16-10-2026 23:24:05"), Is.EqualTo(new DateTime(2026, 10, 16, 23, 24, 5)));
            Assert.That(StatementUploadRules.ParseTime("16/10/2026 23:24:05"), Is.EqualTo(new DateTime(2026, 10, 16, 23, 24, 5)));
            Assert.That(StatementUploadRules.ParseTime("garbage"), Is.Null);
        }

        [Test]
        public void Period_FirstToLastPayment()
        {
            var rows = new[]
            {
                new PaymentTransaction { CompletionTime = "05-10-2026 10:00:00" },
                new PaymentTransaction { CompletionTime = "01-10-2026 09:00:00" },
                new PaymentTransaction { CompletionTime = "" },
            };

            var (from, to) = StatementUploadRules.Period(rows);

            Assert.That(from, Is.EqualTo(new DateTime(2026, 10, 1, 9, 0, 0)));
            Assert.That(to, Is.EqualTo(new DateTime(2026, 10, 5, 10, 0, 0)));
        }

        [Test]
        public void Covered_BackToBackAndOverlappingJoinUp()
        {
            var covered = StatementUploadRules.Covered(new[]
            {
                Up("2026-09-01", "2026-09-15"), Up("2026-09-16", "2026-09-30"), Up("2026-09-10", "2026-09-20"),
            });

            Assert.That(covered, Is.EqualTo(new[] { new DateSpan(new DateTime(2026, 9, 1), new DateTime(2026, 9, 30)) }));
        }

        [Test]
        public void Gaps_DaysBetweenStatements_NewestFirst()
        {
            var covered = StatementUploadRules.Covered(new[]
            {
                Up("2026-07-01", "2026-07-31"), Up("2026-08-10", "2026-08-31"), Up("2026-09-05", "2026-09-30"),
            });

            var gaps = StatementUploadRules.Gaps(covered);

            Assert.That(gaps, Is.EqualTo(new[]
            {
                new DateSpan(new DateTime(2026, 9, 1), new DateTime(2026, 9, 4)),
                new DateSpan(new DateTime(2026, 8, 1), new DateTime(2026, 8, 9)),
            }));
            Assert.That(gaps[1].Days, Is.EqualTo(9));
        }

        [Test]
        public void FailedUploads_DontCount()
        {
            var covered = StatementUploadRules.Covered(new[] { Up("2026-09-01", "2026-09-30", error: "Header not found") });

            Assert.That(covered, Is.Empty);
        }

        [Test]
        public void Months_CountCoveredDaysUpToToday()
        {
            var covered = StatementUploadRules.Covered(new[] { Up("2026-08-01", "2026-08-31"), Up("2026-09-16", "2026-09-30") });

            var months = StatementUploadRules.Months(covered, new DateTime(2026, 10, 19));

            Assert.That(months.Select(m => (m.Month.Month, m.DaysCovered, m.DaysInMonth)), Is.EqualTo(new[]
            {
                (8, 31, 31), (9, 15, 30), (10, 0, 19),
            }));
            Assert.That(months[0].Full, Is.True);
            Assert.That(months[2].None, Is.True);
        }

        [Test]
        public void Build_CheckedUpToAndDaysBehind()
        {
            var model = StatementUploadRules.Build(
                new List<StatementUpload> { Up("2026-09-01", "2026-09-30", added: 3), Up("2026-10-01", "2026-10-10", added: 2) },
                new List<EarlierImport>(), new DateTime(2026, 10, 19));

            Assert.That(model.CoveredTo, Is.EqualTo(new DateTime(2026, 10, 10)));
            Assert.That(model.DaysSinceCovered, Is.EqualTo(9));
            Assert.That(model.MissedFound, Is.EqualTo(5));
            Assert.That(model.Gaps, Is.Empty);
        }

        // In the system, not on statement (2026-11-04).
        [TestCase(" uk4s36ehj2 ", "UK4S36EHJ2")]
        [TestCase("UK4S36EHJ2", "UK4S36EHJ2")]
        [TestCase(null, "")]
        public void Receipts_are_kept_trimmed_and_upper_case(string? raw, string kept)
        {
            Assert.That(StatementUploadRules.CleanReceipt(raw), Is.EqualTo(kept));
        }

        [Test]
        public void Not_on_statement_needs_an_upload_that_kept_its_receipts()
        {
            var old = Up("2026-10-01", "2026-10-10");
            Assert.That(StatementUploadRules.Build(new() { old }, new(), new DateTime(2026, 11, 4)).CanCompare, Is.False);

            var kept = Up("2026-10-11", "2026-10-20");
            kept.ReceiptsKept = true;
            var missing = new List<NotOnStatementPayment>
            {
                new() { MpesaCode = "UK1", Amount = 150m },
                new() { MpesaCode = "UK2", Amount = 70.5m },
            };
            var model = StatementUploadRules.Build(new() { old, kept }, new(), new DateTime(2026, 11, 4), missing);

            Assert.That(model.CanCompare, Is.True);
            Assert.That(model.NotOnStatement, Has.Count.EqualTo(2));
            Assert.That(model.NotOnStatementTotal, Is.EqualTo(220.5m));
        }
    }
}
