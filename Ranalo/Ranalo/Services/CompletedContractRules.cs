using Ranalo.Models;

namespace Ranalo.Services
{
    // Completed contracts: how a closed contract ended and how long it took.
    public static class CompletedContractRules
    {
        public const int PageSize = 10;

        // Contract value as the dashboards and Lock check work it out
        // (30-day months).
        public static decimal ContractValue(CompletedContractRow r) =>
            r.Deposit + r.Daily * 30 * r.TermInMonths + r.Weekly * (30m / 7m) * r.TermInMonths + r.Monthly * r.TermInMonths;

        public static int TermDays(CompletedContractRow r) => (int)Math.Round(r.TermInMonths * 30);

        public static void Evaluate(CompletedContractRow r)
        {
            r.ContractValue = ContractValue(r);
            r.Outcome = r.Paid >= r.ContractValue - LockCheckRules.PaidTolerance ? CompletedOutcome.PaidOff : CompletedOutcome.ClosedShort;
            r.DaysTaken = r.StartDate.HasValue ? Math.Max(0, (r.EndDate.Date - r.StartDate.Value.Date).Days) : null;
        }

        public static decimal Owed(CompletedContractRow r) => Math.Max(0, r.ContractValue - r.Paid);

        public static decimal Overpaid(CompletedContractRow r) => Math.Max(0, r.Paid - r.ContractValue);

        // "45 days early", "on time", "120 days late" against the term.
        public static string Timing(CompletedContractRow r)
        {
            if (r.DaysTaken == null) return "";
            var diff = r.DaysTaken.Value - TermDays(r);
            return diff == 0 ? "on time"
                : diff < 0 ? $"{-diff} day{(diff == -1 ? "" : "s")} early"
                : $"{diff} day{(diff == 1 ? "" : "s")} late";
        }

        public static bool IsLate(CompletedContractRow r) => r.DaysTaken > TermDays(r);

        public static string OutcomeLabel(CompletedOutcome o) => o switch
        {
            CompletedOutcome.PaidOff => "Paid off",
            _ => "Closed short",
        };

        // Whether the phone has been let go by its locker.
        public static string ReleaseLabel(CompletedContractRow r)
        {
            if (r.ReleaseStatus == "Completed") return "Released";
            if (r.ReleaseStatus is "Pending" or "Processing") return "Release pending";
            if (r.ReleaseStatus == "Failed") return "Release failed";
            if (r.ReleaseStatus == "Rejected") return "Release rejected";
            if ((r.LockGroup ?? 1) == 1 && string.Equals(r.NuovoStatus, "un_registered", StringComparison.OrdinalIgnoreCase)) return "Released from Nuovo";
            return "Not released";
        }

        public static bool Matches(CompletedContractRow r, string filter) => filter switch
        {
            CompletedFilter.PaidOff => r.Outcome == CompletedOutcome.PaidOff,
            CompletedFilter.ClosedShort => r.Outcome == CompletedOutcome.ClosedShort,
            _ => true,
        };

        // Account, contract, name, phone or IMEI.
        public static bool MatchesSearch(CompletedContractRow r, string? search)
        {
            if (string.IsNullOrWhiteSpace(search)) return true;
            var s = search.Trim();
            return r.AccountNo.ToString().Contains(s)
                || r.ContractId.ToString() == s
                || (r.CustomerName?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false)
                || (r.Imei?.Contains(s) ?? false)
                || ($"{r.Make} {r.Model}".Contains(s, StringComparison.OrdinalIgnoreCase))
                || (r.DealerName?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false);
        }

        public static CompletedContractsViewModel Build(List<CompletedContractRow> rows, string? filter, string? search, int page)
        {
            filter = filter is CompletedFilter.PaidOff or CompletedFilter.ClosedShort or CompletedFilter.All ? filter : CompletedFilter.PaidOff;
            foreach (var r in rows) Evaluate(r);

            var searched = rows.Where(r => MatchesSearch(r, search)).ToList();
            var shown = searched.Where(r => Matches(r, filter))
                .OrderByDescending(r => r.EndDate).ThenByDescending(r => r.ContractId).ToList();
            var pages = Math.Max(1, (int)Math.Ceiling(shown.Count / (double)PageSize));
            page = Math.Clamp(page, 1, pages);

            return new CompletedContractsViewModel
            {
                Filter = filter,
                Search = search?.Trim(),
                Counts = new Dictionary<string, int>
                {
                    [CompletedFilter.PaidOff] = searched.Count(r => r.Outcome == CompletedOutcome.PaidOff),
                    [CompletedFilter.ClosedShort] = searched.Count(r => r.Outcome == CompletedOutcome.ClosedShort),
                    [CompletedFilter.All] = searched.Count,
                },
                Rows = shown.Skip((page - 1) * PageSize).Take(PageSize).ToList(),
                Page = page,
                Pages = pages,
                Total = shown.Count,
            };
        }
    }
}
