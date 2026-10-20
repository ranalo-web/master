namespace Ranalo.Services
{
    public static class ReportPaging
    {
        // Page size for the scheduled jobs, which need every account. They
        // used to take the first 1,000, so account 1,001 on would never be
        // locked, opened or messaged (2026-10-20).
        public const int All = int.MaxValue;
    }
}
