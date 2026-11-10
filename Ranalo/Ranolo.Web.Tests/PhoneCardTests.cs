using Ranalo.Models;

namespace Ranolo.Web.Tests
{
    // The phone card's dates (2026-11-10).
    public class PhoneCardTests
    {
        [TestCase("10-11-26 09:55:00 UTC", 2026, 11, 10, 9, 55)]
        [TestCase("11/11/2026T12:55:00", 2026, 11, 11, 12, 55)]
        [TestCase("11/11/2026 12:55:00", 2026, 11, 11, 12, 55)]
        [TestCase("2026-11-11T12:55:00", 2026, 11, 11, 12, 55)]
        public void Reads_our_date_shapes(string text, int y, int mo, int d, int h, int mi)
        {
            Assert.That(PhoneCardModel.Parse(text), Is.EqualTo(new DateTime(y, mo, d, h, mi, 0)));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("01-01-70 00:00:00 UTC")]
        [TestCase("not a date")]
        public void Never_or_unreadable_is_null(string? text)
        {
            Assert.That(PhoneCardModel.Parse(text), Is.Null);
        }

        [Test]
        public void Ago_in_plain_words()
        {
            var now = new DateTime(2026, 11, 10, 12, 0, 0);
            Assert.That(PhoneCardModel.Ago(now.AddMinutes(-5), now), Is.EqualTo("5 min ago"));
            Assert.That(PhoneCardModel.Ago(now.AddHours(-3), now), Is.EqualTo("3 h ago"));
            Assert.That(PhoneCardModel.Ago(now.AddDays(-1), now), Is.EqualTo("1 day ago"));
            Assert.That(PhoneCardModel.Ago(now.AddDays(-4), now), Is.EqualTo("4 days ago"));
        }

        [Test]
        public void Title_and_specs()
        {
            var c = new PhoneCardModel { Make = "samsung", Model = "SM-A175F", ProductName = "Galaxy A17", Storage = "128GB", Colour = " " };
            Assert.That(c.Title, Is.EqualTo("Samsung SM-A175F"));
            Assert.That(c.Specs, Is.EqualTo(new[] { "Galaxy A17", "128GB" }));
        }
    }
}
