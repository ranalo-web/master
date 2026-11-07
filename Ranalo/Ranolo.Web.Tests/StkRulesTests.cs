using System.Text;
using System.Text.Json;
using Ranalo.Models;
using Ranalo.Services;

namespace Ranolo.Web.Tests
{
    // M-Pesa Express (STK push) for the customer app (2026-11-06).
    public class StkRulesTests
    {
        [TestCase("0712345678", "254712345678")]
        [TestCase("712345678", "254712345678")]
        [TestCase("+254 712 345 678", "254712345678")]
        [TestCase("254712345678", "254712345678")]
        [TestCase("0112345678", "254112345678")]
        public void Phone_numbers_become_254_format(string raw, string expected)
        {
            Assert.That(StkRules.Phone(raw), Is.EqualTo(expected));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("0812345678")]
        [TestCase("07123")]
        [TestCase("2557123456789")]
        public void Anything_else_is_refused(string? raw)
        {
            Assert.That(StkRules.Phone(raw), Is.Null);
        }

        [Test]
        public void Amounts_are_whole_shillings_of_at_least_one()
        {
            Assert.That(StkRules.Amount(74.83m), Is.EqualTo(75));
            Assert.That(StkRules.Amount(100m), Is.EqualTo(100));
            Assert.That(StkRules.Amount(0m), Is.Null);
            Assert.That(StkRules.Amount(-5m), Is.Null);
        }

        [Test]
        public void Timestamp_and_password_use_Kenya_time()
        {
            var ts = StkRules.Timestamp(new DateTime(2026, 11, 6, 21, 30, 5, DateTimeKind.Utc));
            Assert.That(ts, Is.EqualTo("20261107003005"));

            var password = StkRules.Password("174379", "passkey", ts);
            Assert.That(Encoding.UTF8.GetString(Convert.FromBase64String(password)), Is.EqualTo("174379passkey20261107003005"));
        }

        [Test]
        public void A_paid_callback_is_read()
        {
            var result = Read(@"{""Body"":{""stkCallback"":{""MerchantRequestID"":""29115-34620561-1"",""CheckoutRequestID"":""ws_CO_191220191020363925"",
                ""ResultCode"":0,""ResultDesc"":""The service request is processed successfully."",
                ""CallbackMetadata"":{""Item"":[{""Name"":""Amount"",""Value"":75.00},{""Name"":""MpesaReceiptNumber"",""Value"":""nlj7rt61sv""},
                {""Name"":""TransactionDate"",""Value"":20261106143005},{""Name"":""PhoneNumber"",""Value"":254712345678}]}}}}");

            Assert.That(result!.Paid, Is.True);
            Assert.That(result.CheckoutRequestId, Is.EqualTo("ws_CO_191220191020363925"));
            Assert.That(result.Amount, Is.EqualTo(75m));
            Assert.That(result.Receipt, Is.EqualTo("NLJ7RT61SV"));
            Assert.That(result.PaidAtKenya, Is.EqualTo(new DateTime(2026, 11, 6, 14, 30, 5)));
            Assert.That(result.Phone, Is.EqualTo("254712345678"));
        }

        [Test]
        public void A_cancelled_prompt_is_not_paid()
        {
            var result = Read(@"{""Body"":{""stkCallback"":{""MerchantRequestID"":""1"",""CheckoutRequestID"":""ws_CO_1"",
                ""ResultCode"":1032,""ResultDesc"":""Request cancelled by user""}}}");

            Assert.That(result!.Paid, Is.False);
            Assert.That(result.ResultCode, Is.EqualTo(1032));
        }

        [TestCase("{}")]
        [TestCase("[]")]
        [TestCase(@"{""Body"":{""stkCallback"":{""ResultCode"":0}}}")]
        public void A_garbled_callback_is_not_read(string json)
        {
            Assert.That(Read(json), Is.Null);
        }

        [Test]
        public void Off_until_enabled_and_every_secret_is_set()
        {
            var s = new SafaricomStkSettings { ConsumerKey = "k", ConsumerSecret = "s", Passkey = "p", CallbackBaseUrl = "https://x", CallbackToken = new string('t', 32) };
            Assert.That(s.Ready, Is.False);
            s.Enabled = true;
            Assert.That(s.Ready, Is.True);
            s.Passkey = "";
            Assert.That(s.Ready, Is.False);
        }

        private static StkRules.Result? Read(string json)
        {
            using var doc = JsonDocument.Parse(json);
            return StkRules.ReadCallback(doc.RootElement);
        }
    }
}
