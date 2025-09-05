using Ranalo.Services;
using System.Globalization;

namespace Ranolo.Web.Tests
{
    public class Tests
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void Test1()
        {

            var service = new ContractCalculatorService();
            var utcDate = "2025-06-04 11:43:46";

            DateTime tryDate = DateTime.ParseExact(utcDate, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            DateTime firstPaymentDate = DateTime.ParseExact(
            utcDate.Replace(" UTC", ""),          // Remove UTC for parsing
            "yyyy-MM-dd HH:mm:ss",                // Expected format
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal
        );
            var result = service.CalculateNoDaysUnit(firstPaymentDate);

            Assert.Pass();
        }
    }
}
