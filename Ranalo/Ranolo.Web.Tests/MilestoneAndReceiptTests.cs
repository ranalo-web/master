using Ranalo.Models;
using Ranalo.PayTrigger;
using Ranalo.PayTrigger.Models;
using Ranalo.ScheduledServices;
using Ranalo.Services;
using static Ranalo.Services.CustomerMessages;

namespace Ranolo.Web.Tests
{
    public class PaymentReceiptTests
    {
        private static readonly DateTime Now = new(2026, 10, 27, 11, 30, 0, DateTimeKind.Utc);
        private static readonly DateTime Received = Now.AddMinutes(-5);

        [SetUp]
        public void Reset() => LockDateBook.Clear();

        [Test]
        public void Waits_until_the_lock_jobs_count_the_payment()
        {
            var before = new LockDateBook.Entry(Now.AddDays(1), Received.AddMinutes(-1));

            Assert.That(PaymentReceipt.Decide(Received, before, Now).Timing, Is.EqualTo(PaymentReceipt.Timing.Wait));
            Assert.That(PaymentReceipt.Decide(Received, null, Now).Timing, Is.EqualTo(PaymentReceipt.Timing.Wait));
        }

        [Test]
        public void Sends_with_the_date_once_counted()
        {
            var after = new LockDateBook.Entry(Now.AddDays(2), Received.AddMinutes(1));

            Assert.That(PaymentReceipt.Decide(Received, after, Now), Is.EqualTo((PaymentReceipt.Timing.Send, (DateTime?)Now.AddDays(2))));
        }

        [Test]
        public void Still_behind_sends_without_a_date()
        {
            var after = new LockDateBook.Entry(Now.AddHours(-3), Received.AddMinutes(1));

            Assert.That(PaymentReceipt.Decide(Received, after, Now), Is.EqualTo((PaymentReceipt.Timing.Send, (DateTime?)null)));
        }

        [Test]
        public void Gives_up_waiting_after_the_limit()
        {
            var old = Now - PaymentReceipt.MaxWait - TimeSpan.FromMinutes(1);

            Assert.That(PaymentReceipt.Decide(old, null, Now), Is.EqualTo((PaymentReceipt.Timing.Send, (DateTime?)null)));
            Assert.That(PaymentReceipt.Decide(null, null, Now).Timing, Is.EqualTo(PaymentReceipt.Timing.Send));
        }

        [Test]
        public void Book_skips_fully_paid_dates_and_converts_server_time_to_utc()
        {
            LockDateBook.Record(1, new DateTime(9999, 12, 31));
            Assert.That(LockDateBook.Get(1), Is.Null);

            var local = new DateTime(2026, 10, 28, 12, 0, 0, DateTimeKind.Local);
            LockDateBook.Record(2, local, Now);
            Assert.That(LockDateBook.Get(2)!.LockAtUtc, Is.EqualTo(local.ToUniversalTime()));
        }

        [Test]
        public void Job_holds_back_uncounted_payments_and_fills_in_the_date()
        {
            LockDateBook.Record(30702957, Now.AddDays(1), Received.AddMinutes(1));
            var payments = new List<PaymentMessage>
            {
                new() { AccountNo = "30702957", ReceivedAtUtc = Received },
                new() { AccountNo = "24941358", ReceivedAtUtc = Received },
            };

            var ready = ScheduledSendPaymentMessages.ReadyToSend(payments, Now);

            Assert.That(ready.Select(p => p.AccountNo), Is.EqualTo(new[] { "30702957" }));
            Assert.That(ready[0].UnlockedUntilUtc, Is.EqualTo(Now.AddDays(1)));
        }

        [Test]
        public void Receipt_says_unlocked_until_in_Kenya_time()
        {
            var message = PaymentReceived("Jane", 300m, "1", "X", Now, new DateTime(2026, 10, 29, 9, 30, 0));

            Assert.That(message.Content, Does.Contain("Your phone is unlocked until Thu 29 Oct, 12:30 PM."));
            Assert.That(PaymentReceived("Jane", 300m, "1", "X", Now).Content, Does.Contain("Keep paying daily"));
        }
    }

    public class MilestoneMessageTests
    {
        [Test]
        public void Welcome_and_fully_paid_fit_a_popup()
        {
            var messages = new[] { Welcome("Bartholomew-Christopher", 12345678, 127.35m), FullyPaid("Bartholomew-Christopher", 12345678) };

            Assert.That(messages.Select(m => m.Title.Length), Is.All.LessThanOrEqualTo(TitleLimit));
            Assert.That(messages.Select(m => m.Content.Length), Is.All.LessThanOrEqualTo(ContentLimit));
            Assert.That(messages[0].Content, Does.Contain("Ksh 127.35 a day"));
            Assert.That(messages[1].Content, Does.Contain("The phone is yours"));
        }

        [Test]
        public void Popup_has_no_M_Pesa_button_until_configured()
        {
            var message = new Message("T", "C");

            var off = PayTriggerPopup.For("1", message, new PayTriggerSettings());
            Assert.That(off.DeeplinkPkg, Is.Null);
            Assert.That(off.PushType, Is.EqualTo(1));

            var on = PayTriggerPopup.For("1", message, new PayTriggerSettings { MpesaAppPackage = "com.safaricom.mpesa.lifestyle" });
            Assert.That(on.DeeplinkPkg, Is.EqualTo("com.safaricom.mpesa.lifestyle"));
            Assert.That(on.Deeplink, Is.Null);
        }
    }
}
