using Ranalo.Models;
using Ranalo.Services;

namespace Ranolo.Web.Tests
{
    // Admin > Announcements (2026-11-07).
    public class AnnouncementTests
    {
        [Test]
        public void Name_and_account_are_filled_in_per_customer()
        {
            var m = AnnouncementRules.For(" Hello ", "Hi {name}, account {account}.\r\nBye", "JANE", 1234567);

            Assert.That(m.Title, Is.EqualTo("Hello"));
            Assert.That(m.Content, Is.EqualTo("Hi Jane, account 1234567.\nBye"));
        }

        [Test]
        public void The_default_message_is_fine_to_send()
        {
            Assert.That(AnnouncementRules.Problem(AnnouncementRules.DefaultTitle, AnnouncementRules.DefaultBody), Is.Null);
        }

        [Test]
        public void The_default_message_survives_Knox()
        {
            var m = AnnouncementRules.For(AnnouncementRules.DefaultTitle, AnnouncementRules.DefaultBody, "Jane", 1234567);
            var knox = CustomerMessages.ForKnox(m);

            Assert.That(knox, Does.Contain("my.ranalocredit.com"));
            Assert.That(knox, Does.Contain("1234567"));
            Assert.That(knox.Any(c => CustomerMessages.KnoxNotAllowed.Contains(c)), Is.False);
        }

        [TestCase("", "text", "title")]
        [TestCase("Title", " ", "Write")]
        [TestCase("Title", "a <b> c", "Knox")]
        [TestCase("Title", "Pay = now", "Knox")]
        public void Problems_are_named(string title, string body, string expected)
        {
            Assert.That(AnnouncementRules.Problem(title, body), Does.Contain(expected));
        }

        [Test]
        public void Too_long_is_refused_allowing_for_long_names()
        {
            var body = "{name} " + new string('x', CustomerMessages.ContentLimit - 8);
            Assert.That(AnnouncementRules.Problem("Title", body), Does.Contain("too long"));
        }

        [Test]
        public void Each_send_has_its_own_type_and_shows_as_News_in_the_app()
        {
            var type = AnnouncementRules.TypeFor(new DateTime(2026, 11, 7, 21, 5, 0, DateTimeKind.Utc));
            Assert.That(type, Is.EqualTo("Announcement:20261107-2105"));
            Assert.That(type.Length, Is.LessThanOrEqualTo(100));

            var item = CustomerAppRules.InboxItem(new MessageLog
            {
                MessageType = type, MessageStatus = "sent", DateSent = DateTime.UtcNow,
                Message = "New: the Ranalo Credit app<br><br>Hi Jane",
            });
            Assert.That(item.Kind, Is.EqualTo("News"));
            Assert.That(item.Title, Is.EqualTo("New: the Ranalo Credit app"));
        }
    }
}
