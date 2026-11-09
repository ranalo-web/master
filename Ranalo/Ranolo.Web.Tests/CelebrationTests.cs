using Ranalo.Services;

namespace Ranolo.Web.Tests
{
    // Holiday, birthday and halfway greetings (2026-11-09).
    public class CelebrationTests
    {
        [TestCase(2026, 4, 5)]
        [TestCase(2027, 3, 28)]
        [TestCase(2028, 4, 16)]
        [TestCase(2030, 4, 21)]
        public void Easter_sunday(int year, int month, int day)
        {
            Assert.That(CelebrationRules.EasterSunday(year), Is.EqualTo(new DateTime(year, month, day)));
        }

        [Test]
        public void Every_year_has_the_fixed_holidays_and_easter()
        {
            var keys = CelebrationRules.HolidaysIn(2027).Select(h => h.Key).ToList();
            Assert.That(keys, Is.EquivalentTo(new[]
                { "NewYear", "Easter", "LabourDay", "Madaraka", "Mazingira", "Mashujaa", "Jamhuri", "Christmas" }));
        }

        [Test]
        public void Eid_comes_from_settings_for_its_year_only()
        {
            var eid = new[] { new DateTime(2027, 3, 10), new DateTime(2028, 2, 27) };
            Assert.That(CelebrationRules.HolidayOn(new DateTime(2027, 3, 10), eid)?.Key, Is.EqualTo("EidAlFitr"));
            Assert.That(CelebrationRules.HolidaysIn(2027, eid).Count(h => h.Key == "EidAlFitr"), Is.EqualTo(1));
            Assert.That(CelebrationRules.HolidayOn(new DateTime(2027, 3, 10)), Is.Null);
        }

        [Test]
        public void Holiday_type_is_per_year()
        {
            var christmas = CelebrationRules.HolidayOn(new DateTime(2026, 12, 25))!;
            Assert.That(CelebrationRules.HolidayType(christmas), Is.EqualTo("Holiday:Christmas-2026"));
        }

        [Test]
        public void Every_greeting_fits_the_limits_and_knox()
        {
            var eid = new[] { new DateTime(2027, 3, 10) };
            var messages = CelebrationRules.HolidaysIn(2027, eid).Select(h => CelebrationRules.For(h, "Bartholomew"))
                .Append(CelebrationRules.Birthday("Bartholomew"))
                .Append(CelebrationRules.Halfway("Bartholomew", 999_999_999_999));
            foreach (var m in messages)
            {
                Assert.That(m.Title.Length, Is.LessThanOrEqualTo(CustomerMessages.TitleLimit), m.Title);
                Assert.That(m.Content.Length, Is.LessThanOrEqualTo(CustomerMessages.ContentLimit), m.Title);
                Assert.That((m.Title + m.Content).Intersect(CustomerMessages.KnoxNotAllowed), Is.Empty, m.Title);
                Assert.That(m.Content, Does.Not.Contain("{name}"));
            }
        }

        [Test]
        public void Only_daytime_in_kenya()
        {
            Assert.That(CelebrationRules.SendingHours(new DateTime(2026, 12, 25, 8, 59, 0)), Is.False);
            Assert.That(CelebrationRules.SendingHours(new DateTime(2026, 12, 25, 9, 0, 0)), Is.True);
            Assert.That(CelebrationRules.SendingHours(new DateTime(2026, 12, 25, 18, 59, 0)), Is.True);
            Assert.That(CelebrationRules.SendingHours(new DateTime(2026, 12, 25, 19, 0, 0)), Is.False);
        }

        // ---------- Birthdays ----------

        private static readonly DateTime Today = new(2026, 11, 9);

        [TestCase("1990-07-03", 1990, 7, 3)]
        [TestCase("03/07/1990", 1990, 7, 3)]
        [TestCase("3/7/1990", 1990, 7, 3)]
        [TestCase("03-07-1990", 1990, 7, 3)]
        [TestCase("1990-07-03T00:00:00", 1990, 7, 3)]
        [TestCase("July 3, 1990", 1990, 7, 3)]
        public void Dob_formats_day_first(string raw, int y, int m, int d)
        {
            Assert.That(CelebrationRules.ParseDob(raw, Today), Is.EqualTo(new DateTime(y, m, d)));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("not a date")]
        [TestCase("31/02/1990")]
        [TestCase("01/01/2020")]  // a child: a typo
        [TestCase("01/01/1890")]
        public void Dob_that_cant_be_trusted_is_ignored(string? raw)
        {
            Assert.That(CelebrationRules.ParseDob(raw, Today), Is.Null);
        }

        [Test]
        public void Birthday_on_the_day_and_29_feb_on_28_feb()
        {
            Assert.That(CelebrationRules.IsBirthday(new DateTime(1990, 11, 9), Today), Is.True);
            Assert.That(CelebrationRules.IsBirthday(new DateTime(1990, 11, 10), Today), Is.False);
            Assert.That(CelebrationRules.IsBirthday(new DateTime(1992, 2, 29), new DateTime(2027, 2, 28)), Is.True);
            Assert.That(CelebrationRules.IsBirthday(new DateTime(1992, 2, 29), new DateTime(2028, 2, 28)), Is.False);
            Assert.That(CelebrationRules.IsBirthday(new DateTime(1992, 2, 29), new DateTime(2028, 2, 29)), Is.True);
        }

        // ---------- Halfway ----------

        [TestCase(4999, 10000, false)]
        [TestCase(5000, 10000, true)]
        [TestCase(5999, 10000, true)]
        [TestCase(6000, 10000, false)]
        [TestCase(100, 0, false)]
        public void Halfway_band(decimal paid, decimal owed, bool expected)
        {
            Assert.That(CelebrationRules.IsHalfway(paid, owed), Is.EqualTo(expected));
        }

        [Test]
        public void Inbox_labels()
        {
            Assert.That(CustomerAppRules.InboxItem(new Ranalo.Models.MessageLog { MessageType = "Holiday:Christmas-2026", Message = "x" }).Kind, Is.EqualTo("Greetings"));
            Assert.That(CustomerAppRules.InboxItem(new Ranalo.Models.MessageLog { MessageType = "Birthday:2026", Message = "x" }).Kind, Is.EqualTo("Birthday"));
            Assert.That(CustomerAppRules.InboxItem(new Ranalo.Models.MessageLog { MessageType = "Halfway", Message = "x" }).Kind, Is.EqualTo("Halfway"));
        }
    }
}
