namespace Ranalo.Services
{
    // "Hide suspended" on the commission pages (Pay Commissions, Agents
    // totals, Dealer commissions ready): dealers and agents whose commission
    // is suspended (over CommissionPayoutRules.SuspensionThresholdPct of their
    // accounts 7+ days past the lock date) are left out of the lists. The
    // choice is kept in a cookie, so it holds on every commission page until
    // it's switched off (Views/Shared/_HideSuspendedToggle.cshtml).
    public static class SuspendedFilter
    {
        public const string CookieName = "hide-suspended";

        public static bool Hide(HttpRequest request) => request.Cookies[CookieName] == "1";

        public static List<T> Apply<T>(IEnumerable<T> rows, Func<T, bool> isSuspended, bool hide) =>
            hide ? rows.Where(r => !isSuspended(r)).ToList() : rows.ToList();
    }
}
