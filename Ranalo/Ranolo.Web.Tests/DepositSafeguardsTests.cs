using Ranalo.Services;
using Ranalo.Woocommece.Api.Services;

namespace Ranolo.Web.Tests
{
    public class DepositCodeGuardTests
    {
        [Test]
        public void Received_code_is_kept_over_one_nobody_paid()
        {
            // Order 19928: enrolled with UJPEOBI5XU found, later held UJPEOB15XU.
            Assert.That(DepositCodeGuard.CodeToSave("UJPEOBI5XU", "UJPEOB15XU", existingReceived: true, incomingReceived: false),
                Is.EqualTo("UJPEOBI5XU"));
        }

        [Test]
        public void Received_code_is_kept_when_WooCommerce_sends_none()
        {
            Assert.That(DepositCodeGuard.CodeToSave("UJPEOBI5XU", null, true, false), Is.EqualTo("UJPEOBI5XU"));
            Assert.That(DepositCodeGuard.CodeToSave("UJPEOBI5XU", "", true, false), Is.EqualTo("UJPEOBI5XU"));
        }

        [Test]
        public void A_correction_in_WooCommerce_to_a_received_code_is_taken()
        {
            Assert.That(DepositCodeGuard.CodeToSave("UJPEOB15XU", "UJPEOBI5XU", false, true), Is.EqualTo("UJPEOBI5XU"));
        }

        [Test]
        public void Without_a_received_code_WooCommerce_wins_as_before()
        {
            Assert.That(DepositCodeGuard.CodeToSave(null, "UJPEOB15XU", false, false), Is.EqualTo("UJPEOB15XU"));
            Assert.That(DepositCodeGuard.CodeToSave("AAAAAAAAAA", "BBBBBBBBBB", false, false), Is.EqualTo("BBBBBBBBBB"));
        }

        [Test]
        public void Same_code_in_a_different_case_or_spacing_is_the_same()
        {
            Assert.That(DepositCodeGuard.Same(" ujpeobi5xu", "UJPEOBI5XU "), Is.True);
            Assert.That(DepositCodeGuard.Same(null, ""), Is.True);
        }
    }

    public class DepositSafeguardsTests
    {
        private static ApprovedWithoutContract Row() => new()
        {
            AccountId = 30702957,
            OrderId = 19928,
            DepositCode = "UJPEOBI5XU",
            DepositReceived = true,
            DepositPaidToAccount = "30702957",
            DeviceStatus = "enrolled"
        };

        [Test]
        public void Choices_leave_out_another_orders_deposit_and_are_newest_first()
        {
            var payments = new[]
            {
                new AccountPayment { MpesaCode = "OLDDEPOSIT", PaidAt = new DateTime(2026, 9, 1), DepositOnOrderId = 18000 },
                new AccountPayment { MpesaCode = "UJPEOBI5XU", PaidAt = new DateTime(2026, 10, 25) },
                new AccountPayment { MpesaCode = "UJQEOBKEKM", PaidAt = new DateTime(2026, 10, 26) },
            };

            var choices = DepositSafeguards.DepositChoices(payments, orderId: 19928);

            Assert.That(choices.Select(c => c.MpesaCode), Is.EqualTo(new[] { "UJQEOBKEKM", "UJPEOBI5XU" }));
        }

        [Test]
        public void This_orders_own_deposit_stays_a_choice()
        {
            var payments = new[] { new AccountPayment { MpesaCode = "UJPEOBI5XU", DepositOnOrderId = 19928 } };

            Assert.That(DepositSafeguards.DepositChoices(payments, 19928), Has.Count.EqualTo(1));
        }

        [Test]
        public void No_code_on_the_order()
        {
            var row = Row();
            row.DepositCode = null;
            Assert.That(DepositSafeguards.WhyNoContract(row), Does.Contain("no deposit M-Pesa code"));
        }

        [Test]
        public void Code_nobody_paid_points_at_the_payments()
        {
            var row = Row();
            row.DepositCode = "UJPEOB15XU";
            row.DepositReceived = false;
            Assert.That(DepositSafeguards.WhyNoContract(row), Does.Contain("UJPEOB15XU isn't in our payments"));
        }

        [Test]
        public void Deposit_on_another_account_points_at_Assign_Payments()
        {
            var row = Row();
            row.DepositPaidToAccount = "254712345678";
            Assert.That(DepositSafeguards.WhyNoContract(row), Does.Contain("Assign Payments"));
        }

        [Test]
        public void No_device_on_the_account()
        {
            var row = Row();
            row.DeviceStatus = null;
            Assert.That(DepositSafeguards.WhyNoContract(row), Does.Contain("no device"));
        }

        [Test]
        public void Everything_in_place_waits_for_the_contract_job()
        {
            Assert.That(DepositSafeguards.WhyNoContract(Row()), Does.Contain("next run"));
        }
    }
}
