using System.Globalization;
using Ranalo.Models;

namespace Ranalo.Services
{
    // Statement Uploads report (2026-10-19): which periods of the M-Pesa
    // statement have been imported to catch missed payments, and the gaps.
    // A statement covers the days from its first payment to its last; days
    // are whole calendar days, so back-to-back statements join up.
    public static class StatementUploadRules
    {
        // M-Pesa's Completion Time, as PaymentsService reads it.
        private static readonly string[] Formats =
        {
            "dd-MM-yyyy HH:mm:ss", "d-M-yyyy HH:mm:ss", "dd/MM/yyyy HH:mm:ss",
            "yyyy-MM-dd HH:mm:ss", "M/d/yyyy h:mm:ss tt", "M/d/yyyy hh:mm:ss tt",
        };

        public static DateTime? ParseTime(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            var value = raw.Trim().Replace(' ', ' ').Replace("–", "-").Replace("—", "-");
            return DateTime.TryParseExact(value, Formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var t) ? t : null;
        }

        public static (DateTime? From, DateTime? To) Period(IEnumerable<PaymentTransaction> payments)
        {
            var times = payments.Select(p => ParseTime(p.CompletionTime)).Where(t => t.HasValue).Select(t => t!.Value).ToList();
            return times.Any() ? (times.Min(), times.Max()) : (null, null);
        }

        // Successful uploads' periods joined into covered stretches of days.
        public static List<DateSpan> Covered(IEnumerable<StatementUpload> uploads)
        {
            var spans = uploads
                .Where(u => u.Error == null && u.PeriodFrom.HasValue && u.PeriodTo.HasValue)
                .Select(u => new DateSpan(u.PeriodFrom!.Value.Date, u.PeriodTo!.Value.Date))
                .OrderBy(s => s.From)
                .ToList();

            var merged = new List<DateSpan>();
            foreach (var s in spans)
            {
                var last = merged.LastOrDefault();
                if (last != null && s.From <= last.To.AddDays(1))
                {
                    merged[^1] = last with { To = s.To > last.To ? s.To : last.To };
                }
                else
                {
                    merged.Add(s);
                }
            }
            return merged;
        }

        // Days between covered stretches, newest first.
        public static List<DateSpan> Gaps(List<DateSpan> covered) =>
            covered.Zip(covered.Skip(1), (a, b) => new DateSpan(a.To.AddDays(1), b.From.AddDays(-1)))
                .OrderByDescending(g => g.From)
                .ToList();

        // Months from the first covered day to today, oldest first.
        public static List<CoverageMonth> Months(List<DateSpan> covered, DateTime today)
        {
            if (covered.Count == 0) return new();

            var months = new List<CoverageMonth>();
            for (var m = new DateTime(covered[0].From.Year, covered[0].From.Month, 1); m <= today; m = m.AddMonths(1))
            {
                var end = m.AddMonths(1).AddDays(-1);
                if (end > today.Date) end = today.Date;
                var days = covered.Sum(s =>
                {
                    var from = s.From > m ? s.From : m;
                    var to = s.To < end ? s.To : end;
                    return to >= from ? (to - from).Days + 1 : 0;
                });
                months.Add(new CoverageMonth(m, days, (end - m).Days + 1));
            }
            return months;
        }

        public static StatementUploadsViewModel Build(List<StatementUpload> uploads, List<EarlierImport> earlier, DateTime today)
        {
            var covered = Covered(uploads);
            var coveredTo = covered.LastOrDefault()?.To;
            return new StatementUploadsViewModel
            {
                Uploads = uploads.OrderByDescending(u => u.UploadedAtUtc).ToList(),
                Covered = covered.OrderByDescending(c => c.From).ToList(),
                Gaps = Gaps(covered),
                Months = Months(covered, today),
                CoveredTo = coveredTo,
                DaysSinceCovered = coveredTo.HasValue ? Math.Max(0, (today.Date - coveredTo.Value).Days) : 0,
                MissedFound = uploads.Where(u => u.Error == null).Sum(u => u.NewPayments),
                Earlier = earlier,
            };
        }
    }
}
