using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using Ranalo.Models;

namespace Ranolo.Web.Tests
{
    public class PagingLinksTests
    {
        private static HttpRequest Request(string query)
        {
            var ctx = new DefaultHttpContext();
            ctx.Request.QueryString = new QueryString(query);
            return ctx.Request;
        }

        [Test]
        public void Keep_CarriesSearch_DropsPage()
        {
            Assert.That(PagingLinks.Keep(Request("?page=3&searchTerm=Renalo")), Is.EqualTo("&searchTerm=Renalo"));
        }

        [Test]
        public void Keep_DropsPageSize_WhenLinkSetsIt()
        {
            Assert.That(PagingLinks.Keep(Request("?pageSize=40&searchTerm=a%20b&dealer=5"), "pageSize"),
                Is.EqualTo("&searchTerm=a%20b&dealer=5"));
        }

        [Test]
        public void Keep_NoQuery_Empty()
        {
            Assert.That(PagingLinks.Keep(Request("")), Is.EqualTo(""));
            Assert.That(PagingLinks.Keep(Request("?searchTerm=")), Is.EqualTo(""));
        }
    }
}
