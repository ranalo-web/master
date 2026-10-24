using Ranalo.Services;

namespace Ranolo.Web.Tests
{
    // Financials monthly chart follows the date range (2026-10-24).
    public class FinancialsTrendMonthsTests
    {
        private static readonly DateTime Oct2026 = new(2026, 10, 1);

        [Test]
        public void AllTime_Last12Months()
        {
            Assert.That(DashboardReportService.FinancialsTrendMonths(null, Oct2026), Is.EqualTo(12));
        }

        [Test]
        public void WeekOrMonth_Last12Months()
        {
            Assert.That(DashboardReportService.FinancialsTrendMonths(new DateTime(2026, 10, 18), Oct2026), Is.EqualTo(12));
        }

        [Test]
        public void Range_ItsOwnMonths()
        {
            Assert.That(DashboardReportService.FinancialsTrendMonths(new DateTime(2026, 1, 1), Oct2026), Is.EqualTo(10));
            Assert.That(DashboardReportService.FinancialsTrendMonths(new DateTime(2025, 1, 1), new DateTime(2025, 12, 1)), Is.EqualTo(12));
            Assert.That(DashboardReportService.FinancialsTrendMonths(new DateTime(2026, 9, 15), Oct2026), Is.EqualTo(2));
        }

        [Test]
        public void LongRange_Last24Months()
        {
            Assert.That(DashboardReportService.FinancialsTrendMonths(new DateTime(2020, 1, 1), Oct2026), Is.EqualTo(24));
        }
    }
}
