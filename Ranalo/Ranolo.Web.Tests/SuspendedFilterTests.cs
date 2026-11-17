using Microsoft.AspNetCore.Http;
using Ranalo.Services;

namespace Ranolo.Web.Tests
{
    // "Hide suspended" on the commission pages: a cookie, off unless set to 1.
    public class SuspendedFilterTests
    {
        private record Payee(string Name, bool Suspended);

        private static readonly Payee[] Payees = { new("Oty", false), new("Agnes", true), new("Jacksolo", false) };

        private static HttpRequest RequestWith(string? cookie)
        {
            var context = new DefaultHttpContext();
            if (cookie != null)
            {
                context.Request.Headers.Cookie = $"{SuspendedFilter.CookieName}={cookie}";
            }
            return context.Request;
        }

        [Test]
        public void Hide_LeavesSuspendedOut_InTheSameOrder()
        {
            var shown = SuspendedFilter.Apply(Payees, p => p.Suspended, hide: true);
            Assert.That(shown.Select(p => p.Name), Is.EqualTo(new[] { "Oty", "Jacksolo" }));
        }

        [Test]
        public void Show_KeepsEveryone()
        {
            Assert.That(SuspendedFilter.Apply(Payees, p => p.Suspended, hide: false), Has.Count.EqualTo(3));
        }

        [TestCase(null, false)]
        [TestCase("0", false)]
        [TestCase("1", true)]
        public void Cookie_TurnsItOn(string? cookie, bool expected)
        {
            Assert.That(SuspendedFilter.Hide(RequestWith(cookie)), Is.EqualTo(expected));
        }
    }
}
