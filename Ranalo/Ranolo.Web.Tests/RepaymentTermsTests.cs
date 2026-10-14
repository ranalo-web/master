using Newtonsoft.Json.Linq;
using Ranalo.Calculator.Logic.Contract;
using Ranalo.Woocommece.Api.Services;

namespace Ranolo.Web.Tests
{
    // Contract term from WooCommerce's Repayment Period (agreed 2026-10-14).
    public class RepaymentTermsTests
    {
        // A line item shaped like a live order (19832, Samsung Galaxy A17),
        // with the Repayment Period the website now adds.
        private static JObject Order(string? period, string color = "Black", decimal daily = 124.88425m, decimal deposit = 5287.5m, decimal lineTotal = 50870.25125m)
        {
            var meta = new JArray();
            if (color != null)
                meta.Add(new JObject { ["key"] = "pa_color", ["value"] = color.ToLowerInvariant(), ["display_key"] = "Color", ["display_value"] = color });
            if (period != null)
                meta.Add(new JObject { ["key"] = "pa_repayment-period", ["value"] = period.ToLowerInvariant().Replace(' ', '-'), ["display_key"] = "Repayment Period", ["display_value"] = period });
            meta.Add(new JObject { ["key"] = "_rc_deposit", ["value"] = deposit.ToString(System.Globalization.CultureInfo.InvariantCulture), ["display_key"] = "_rc_deposit", ["display_value"] = deposit.ToString(System.Globalization.CultureInfo.InvariantCulture) });
            meta.Add(new JObject { ["key"] = "_rc_daily", ["value"] = daily.ToString(System.Globalization.CultureInfo.InvariantCulture), ["display_key"] = "_rc_daily", ["display_value"] = daily.ToString(System.Globalization.CultureInfo.InvariantCulture) });
            meta.Add(new JObject { ["key"] = "_rc_line_total", ["value"] = lineTotal.ToString(System.Globalization.CultureInfo.InvariantCulture), ["display_key"] = "_rc_line_total", ["display_value"] = lineTotal.ToString(System.Globalization.CultureInfo.InvariantCulture) });

            return new JObject
            {
                ["line_items"] = new JArray
                {
                    new JObject { ["name"] = "Samsung Galaxy A17", ["product_id"] = 19280, ["quantity"] = 1, ["sku"] = "", ["meta_data"] = meta }
                }
            };
        }

        [TestCase("14 Months", 14)]
        [TestCase("12 Months", 12)]
        [TestCase("14-months", 14)]
        [TestCase("14", 14)]
        [TestCase("", null)]
        [TestCase("Monthly", null)]
        [TestCase("0 Months", null)]
        public void ParsesTheRepaymentPeriod(string value, int? expected)
        {
            Assert.That(RepaymentTerms.ParseTerm(value), Is.EqualTo((decimal?)expected));
        }

        [Test]
        public void FourteenMonthPhoneTakesTheWebsitesTermDailyAndDeposit()
        {
            var product = SyncService.MapOrderProducts(Order("14 Months", daily: 100m, deposit: 2500m, lineTotal: 44500m)).Single();

            Assert.That(product.TermInMonths, Is.EqualTo(14m));
            Assert.That(product.DailySalePrice, Is.EqualTo(100m));
            Assert.That(product.DepositAmount, Is.EqualTo(2500m));
            Assert.That(product.DeviceAmount, Is.EqualTo(44500m));
        }

        [Test]
        public void TermIsReadFromTheSlugWhenThereIsNoDisplayValue()
        {
            var order = Order(null);
            ((JArray)order["line_items"]![0]!["meta_data"]!).Add(new JObject { ["key"] = "pa_repayment-period", ["value"] = "14-months" });

            Assert.That(SyncService.MapOrderProducts(order).Single().TermInMonths, Is.EqualTo(14m));
        }

        [Test]
        public void PhoneWithoutARepaymentPeriodTakesTheWebsitesFigures()
        {
            // WooCommerce's figures always come first: order 19832 is a
            // 12-month A17 at 124.88425 a day and 5,287.50 deposit.
            var product = SyncService.MapOrderProducts(Order(null)).Single();

            Assert.That(product.TermInMonths, Is.Null);
            Assert.That(RepaymentTerms.TermOrDefault(product.TermInMonths), Is.EqualTo(12m));
            Assert.That(product.DailySalePrice, Is.EqualTo(124.88425m));
            Assert.That(product.DepositAmount, Is.EqualTo(5287.5m));
            Assert.That(product.DeviceAmount, Is.EqualTo(50870.25125m));
        }

        [Test]
        public void ItemWithoutColourStillTakesTheWebsitesDailyRate()
        {
            var product = SyncService.MapOrderProducts(Order(null, color: null!, daily: 150m, deposit: 6000m, lineTotal: 60750m)).Single();

            Assert.That(product.DailySalePrice, Is.EqualTo(150m));
            Assert.That(product.TermInMonths, Is.Null);
        }

        [Test]
        public void DepositIsTheWebsitesWhenQuoted()
        {
            Assert.That(RepaymentTerms.SalesDeposit(44500m, 100m, 2500m), Is.EqualTo(2500m));
        }

        [Test]
        public void DepositFallsBackToTwelveMonthsWorkedBack()
        {
            // Older orders with no _rc_deposit: total - daily x 365, as before.
            Assert.That(RepaymentTerms.SalesDeposit(60750m, 150m, null), Is.EqualTo(6000m));
        }

        [Test]
        public void TwelveMonthWebsiteDepositMatchesTheOldWorkingBack()
        {
            // Live order 19832: the website's deposit is what we used to work out.
            Assert.That(RepaymentTerms.SalesDeposit(50870.25125m, 124.88425m, 5287.5m),
                Is.EqualTo(Math.Round(RepaymentTerms.SalesDeposit(50870.25125m, 124.88425m, null), 2)));
        }

        [Test]
        public void FourteenMonthContractRunsFourHundredAndTwentyDays()
        {
            var calc = new ContractCalculatorService();

            Assert.That(calc.CalculateTotalLoan(100m, 14m), Is.EqualTo(42000m));
            Assert.That(calc.CalculateTotalCost(100m, 2500m, 14m), Is.EqualTo(44500m));
            Assert.That(calc.CalculateNoDaysLeft(new DateTime(2026, 10, 14), 14), Is.EqualTo(420));
        }

        [TestCase(null, 12)]
        [TestCase(0, 12)]
        [TestCase(14, 14)]
        public void TermDefaultsToTwelve(int? term, int expected)
        {
            Assert.That(RepaymentTerms.TermOrDefault(term), Is.EqualTo((decimal)expected));
        }
    }
}
