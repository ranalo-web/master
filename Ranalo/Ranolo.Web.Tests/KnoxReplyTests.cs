using Ranalo.SumsungKnox;

namespace Ranolo.Web.Tests
{
    public class KnoxReplyTests
    {
        [Test]
        public void Success_is_accepted()
        {
            Assert.That(KnoxReply.Accepted("{\"result\":\"SUCCESS\",\"objectId\":\"69a19dec8d244d7eaa2afaba\",\"requestedId\":\"351065613492352\"}"), Is.True);
        }

        [TestCase("{\"result\":\"FAIL\",\"error\":{\"code\":4000000,\"message\":\"RESOURCE_INVALID_PARAM\"}}")]
        [TestCase("{\"error\":{\"code\":4000000},\"result\":\"FAIL\"}")]
        [TestCase("")]
        [TestCase("not json")]
        [TestCase("[]")]
        public void Anything_else_is_refused(string body)
        {
            Assert.That(KnoxReply.Accepted(body), Is.False);
        }
    }
}
