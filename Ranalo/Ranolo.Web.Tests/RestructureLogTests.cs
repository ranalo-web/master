using Ranalo.DataStore;

namespace Ranolo.Web.Tests
{
    // Restructure log (2026-11-10): who agreed a plan, shown on the account's history.
    public class RestructureLogTests
    {
        [TestCase("Agreed", "Mary", "Mary")]
        [TestCase("Self-service", null, "Customer (in app)")]
        [TestCase("Before log", null, "Not recorded")]
        [TestCase("Undone", null, "-")]
        public void Who_did_it(string kind, string? byName, string expected)
        {
            Assert.That(new RestructureLogEntry { Kind = kind, ByName = byName }.Who, Is.EqualTo(expected));
        }

        [Test]
        public void An_undo_is_not_counted_as_a_restructure()
        {
            Assert.That(RestructureLogKind.Counted, Does.Not.Contain(RestructureLogKind.Undone));
            Assert.That(RestructureLogKind.Counted, Does.Contain(RestructureLogKind.SelfService));
        }
    }
}
