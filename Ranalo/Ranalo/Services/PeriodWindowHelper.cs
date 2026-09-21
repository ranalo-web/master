namespace Ranalo.Services
{
    // Shared Week/Month/YTD/Last-12-Months period resolution -- same window
    // definitions used by PaymentsController and DevicesController's period
    // filters, and (independently re-derived, not yet pointed at this) the
    // Dealer Dashboard's own period toggle. "period" is one of
    // "week"/"month"/"ytd"/"year" (case-insensitive); any other value
    // (including "" or null) means "all time" -- both bounds null.
    public static class PeriodWindowHelper
    {
        public static (DateTime? FromDate, DateTime? ToDateExclusive) Resolve(string? period)
        {
            var today = DateTime.Now.Date;
            var tomorrow = today.AddDays(1);

            return period?.ToLowerInvariant() switch
            {
                "week" => (today.AddDays(-6), tomorrow),
                "month" => (new DateTime(today.Year, today.Month, 1), tomorrow),
                "ytd" => (new DateTime(today.Year, 1, 1), tomorrow),
                "year" => (tomorrow.AddYears(-1), tomorrow),
                _ => (null, null),
            };
        }
    }
}
