using NUnit.Framework;
using Ranalo.Models;

namespace Ranolo.Web.Tests
{
    public class UsersPageTests
    {
        [Test]
        public void DealerName_StaffAndUnknown()
        {
            var vm = new UsersViewModel { DealerNames = new() { [11] = "Ranalo Credit", [14] = "Mali Mali" } };
            Assert.That(vm.DealerName(0), Is.EqualTo("Ranalo (staff)"));
            Assert.That(vm.DealerName(14), Is.EqualTo("Mali Mali"));
            Assert.That(vm.DealerName(99), Is.EqualTo("Dealer 99"));
        }
    }
}
