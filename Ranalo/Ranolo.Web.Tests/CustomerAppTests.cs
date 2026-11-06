using Microsoft.AspNetCore.Http;
using Ranalo;
using Ranalo.Models;
using Ranalo.Services;

namespace Ranolo.Web.Tests
{
    // Customer app (my.ranalocredit.com, 2026-11-06).
    public class CustomerAppTests
    {
        private static readonly DateTime Now = new(2026, 11, 6, 9, 0, 0, DateTimeKind.Utc);

        // ---------- Login ----------

        [Test]
        public void Codes_are_six_digits()
        {
            for (var i = 0; i < 50; i++)
            {
                Assert.That(CustomerLoginRules.NewCode(), Does.Match("^[0-9]{6}$"));
            }
        }

        [TestCase("123 456", "123456")]
        [TestCase("12345", null)]
        [TestCase("1234567", null)]
        [TestCase(null, null)]
        public void Typed_codes_are_cleaned(string? typed, string? expected)
        {
            Assert.That(CustomerLoginRules.CleanCode(typed), Is.EqualTo(expected));
        }

        [TestCase("1234567", 1234567L)]
        [TestCase(" 123 4567 ", 1234567L)]
        [TestCase("12", null)]
        [TestCase("abc", null)]
        public void Account_numbers_are_cleaned(string typed, long? expected)
        {
            Assert.That(CustomerLoginRules.AccountNo(typed), Is.EqualTo(expected));
        }

        [Test]
        public void Wrong_codes_slow_down_then_lock()
        {
            Assert.That(CustomerLoginRules.WaitAfter(2), Is.EqualTo(TimeSpan.Zero));
            Assert.That(CustomerLoginRules.WaitAfter(3), Is.EqualTo(TimeSpan.FromMinutes(1)));
            Assert.That(CustomerLoginRules.WaitAfter(5), Is.EqualTo(TimeSpan.FromMinutes(4)));
            Assert.That(CustomerLoginRules.WaitAfter(9), Is.EqualTo(TimeSpan.FromMinutes(30)));
            Assert.That(CustomerLoginRules.Locks(9), Is.False);
            Assert.That(CustomerLoginRules.Locks(10), Is.True);
        }

        [Test]
        public void A_new_code_waits_a_minute_and_five_an_hour_at_most()
        {
            Assert.That(CustomerLoginRules.SendWait(new List<DateTime>(), Now), Is.EqualTo(TimeSpan.Zero));
            Assert.That(CustomerLoginRules.SendWait(new[] { Now.AddSeconds(-20) }, Now), Is.EqualTo(TimeSpan.FromSeconds(40)));
            Assert.That(CustomerLoginRules.SendWait(new[] { Now.AddMinutes(-2) }, Now), Is.EqualTo(TimeSpan.Zero));

            var five = Enumerable.Range(1, 5).Select(i => Now.AddMinutes(-10 * i)).ToList();
            Assert.That(CustomerLoginRules.SendWait(five, Now), Is.EqualTo(TimeSpan.FromMinutes(10)));
        }

        [Test]
        public void The_code_is_never_what_gets_logged()
        {
            var message = CustomerLoginRules.CodeMessage("482913");
            Assert.That(message, Does.Contain("482913"));
            Assert.That(CustomerLoginRules.CodeLogText, Does.Not.Contain("482913"));
            Assert.That(CustomerMessages.ForKnox(message).IndexOfAny(CustomerMessages.KnoxNotAllowed), Is.EqualTo(-1));
        }

        // ---------- What the app shows ----------

        private static MobileStatusReport Report() => new()
        {
            AccountNo = 1234567, FirstName = "JANE", Make = "samsung", Model = "SM-A055F",
            Deposit = 3368m, TotalLoan = 26000m, TotalPaid = 14684m, LoanBalance = 14684m,
            Arrears = -150m, Daily = 68.16m, TermInMonths = 12,
            FirstPaymentDate = new DateTime(2025, 5, 6), LastPaymentDate = new DateTime(2026, 11, 6, 7, 12, 1), LastPaidAmt = 68m,
        };

        [Test]
        public void Figures_match_the_weekly_summary()
        {
            var r = Report();
            var view = CustomerAppRules.Account(r, Now.AddHours(10), null, Now);
            var summary = CustomerMessages.Summary(r, Now.AddHours(10), view.ContractEnds!.Value).Content;

            Assert.That(summary, Does.Contain($"Paid so far: {view.PercentPaid}%"));
            Assert.That(summary, Does.Contain($"Balance: Ksh {CustomerMessages.Money(view.Balance)}"));
            Assert.That(summary, Does.Contain($"Behind by: Ksh {CustomerMessages.Money(view.Behind)}"));
            Assert.That(view.ContractEnds, Is.EqualTo(new DateTime(2025, 5, 6).AddDays(360)));
            Assert.That(view.LocksAtKenya, Is.EqualTo(Now.AddHours(13)));
            Assert.That(view.FirstName, Is.EqualTo("Jane"));
            Assert.That(view.DeviceName, Is.EqualTo("Samsung SM-A055F"));
        }

        [Test]
        public void A_restructured_account_stands_on_its_plan_not_the_old_contract()
        {
            // Far behind on the original contract, a little ahead on the agreed plan.
            var r = Report();
            r.Arrears = -7261.60m;
            var plan = new RestructuredRecord { AccountNo = 1234567, ArrearsR = 41.99m, AmountRes = 68.16m, DateAgreed = new DateTime(2026, 10, 19) };

            var view = CustomerAppRules.Account(r, Now.AddHours(10), null, Now, plan);

            Assert.That(view.Behind, Is.EqualTo(0m));
            Assert.That(view.Ahead, Is.EqualTo(41.99m));
            Assert.That(view.Daily, Is.EqualTo(68.16m));
            Assert.That(view.RestructuredOn, Is.EqualTo(new DateTime(2026, 10, 19)));
            Assert.That(view.Balance, Is.EqualTo(r.LoanBalance));
        }

        [Test]
        public void Monthly_payers_have_a_loan_total_and_a_true_percentage()
        {
            // A monthly payer: Ksh 2,000 deposit + 12 x 1,500 a month; paid 8,220,
            // report balance 11,780. TotalLoan used to be Daily x 30 x term = 0,
            // so it showed 100% paid (411% in the weekly summary).
            var loan = ContractMath.TotalLoan(daily: 0m, weekly: 0m, monthly: 1500m, termInMonths: 12);
            Assert.That(loan, Is.EqualTo(18000m));

            var r = Report();
            r.Deposit = 2000m; r.Daily = 0m; r.Monthly = 1500m; r.TotalLoan = loan; r.TotalPaid = 8220m; r.LoanBalance = 11780m;
            var view = CustomerAppRules.Account(r, null, null, Now);

            Assert.That(view.Owed, Is.EqualTo(20000m));
            Assert.That(view.Owed - view.TotalPaid, Is.EqualTo(r.LoanBalance));
            Assert.That(view.PercentPaid, Is.EqualTo(41));
            Assert.That(CustomerMessages.Summary(r, null, Now).Content, Does.Contain("Paid so far: 41%"));

            Assert.That(ContractMath.TotalLoan(68.16m, 0m, 0m, 12), Is.EqualTo(68.16m * 360));
            Assert.That(ContractMath.TotalLoan(0m, 700m, 0m, 12), Is.EqualTo(700m * 30m / 7m * 12));
        }

        [Test]
        public void A_restructured_account_shows_both_its_plan_and_its_original_contract()
        {
            var r = Report();
            r.Arrears = -7261.60m; r.TotalDue = 27905.60m; r.TotalPaid = 20644m;
            var plan = new RestructuredRecord { ArrearsR = 92m, AmountRes = 68.16m, TotalDueR = 1226.88m, TotalPaidR = 1318.88m, DateAgreed = new DateTime(2026, 10, 19) };

            var view = CustomerAppRules.Account(r, Now.AddHours(10), null, Now, plan);

            Assert.That((view.PlanDue, view.PlanPaid, view.Ahead, view.Behind), Is.EqualTo((1226.88m, 1318.88m, 92m, 0m)));
            Assert.That((view.ContractDue, view.ContractBehind, view.ContractDaily), Is.EqualTo((27905.60m, 7261.60m, 68.16m)));
        }

        [TestCase(150.40, 0, 151)]
        [TestCase(150.40, 100, 51)]
        [TestCase(150.40, 200, 0)]
        public void Unlock_amount_is_the_auto_restructure_daily_less_paid_today(decimal newDaily, decimal paid24, decimal expected)
        {
            Assert.That(CustomerAppRules.UnlockAmount(newDaily, paid24), Is.EqualTo(expected));
        }

        [Test]
        public void On_a_plan_the_plans_own_rule_opens_the_phone()
        {
            // Ahead on the plan: nothing to pay, it opens when it connects.
            Assert.That(CustomerAppRules.PlanUnlockAmount(new RestructuredRecord { ArrearsR = 35m, AmountRes = 70m }), Is.EqualTo(0m));

            // Behind, past the contract end (no auto restructure): catch up plus one plan day.
            Assert.That(CustomerAppRules.PlanUnlockAmount(new RestructuredRecord { ArrearsR = -148.5m, AmountRes = 80m, AutoNewDaily = null }), Is.EqualTo(229m));

            // Behind with an auto restructure too: whichever is less.
            Assert.That(CustomerAppRules.PlanUnlockAmount(new RestructuredRecord { ArrearsR = -1000m, AmountRes = 100m, AutoNewDaily = 250m, PaidLast24Hours = 50m }), Is.EqualTo(200m));
            Assert.That(CustomerAppRules.PlanUnlockAmount(new RestructuredRecord { ArrearsR = -20m, AmountRes = 100m, AutoNewDaily = 250m }), Is.EqualTo(120m));
        }

        [Test]
        public void No_unlock_amount_without_an_auto_restructure()
        {
            // Not in arrears, or past the contract end (NewDaily 0 / null).
            Assert.That(CustomerAppRules.UnlockAmount(null, 0m), Is.Null);
            Assert.That(CustomerAppRules.UnlockAmount(0m, 0m), Is.Null);
        }

        [Test]
        public void A_locked_phone_shows_what_opens_it_for_24_hours()
        {
            var r = Report();
            r.Locked = true; r.NewDaily = 180.25m; r.TotalLast24 = 50m;
            var view = CustomerAppRules.Account(r, Now.AddHours(-2), null, Now);
            Assert.That(view.Locked, Is.True);
            Assert.That(view.UnlockFor24h, Is.EqualTo(131m));

            // Restructured: the plan's auto restructure figures count.
            var plan = new RestructuredRecord { ArrearsR = -300m, AmountRes = 70m, AutoNewDaily = 95.5m, PaidLast24Hours = 0m };
            Assert.That(CustomerAppRules.Account(r, Now.AddHours(-2), null, Now, plan).UnlockFor24h, Is.EqualTo(96m));

            // A lock date that has passed counts as locked too.
            r.Locked = false;
            Assert.That(CustomerAppRules.Account(r, Now.AddMinutes(-1), null, Now).Locked, Is.True);
            Assert.That(CustomerAppRules.Account(r, Now.AddHours(5), null, Now).Locked, Is.False);
        }

        [Test]
        public void Fully_paid_shows_no_lock_date()
        {
            var r = Report();
            r.TotalPaid = 29400m; r.LoanBalance = -32m; r.Arrears = 74m;
            var view = CustomerAppRules.Account(r, new DateTime(9999, 12, 31, 23, 59, 59, DateTimeKind.Utc), null, Now);

            Assert.That(view.FullyPaid, Is.True);
            Assert.That(view.LocksAtKenya, Is.Null);
            Assert.That(view.Balance, Is.EqualTo(0m));
            Assert.That(view.PercentPaid, Is.EqualTo(100));
        }

        [Test]
        public void The_product_name_and_first_photo_come_from_the_order()
        {
            var device = new MyDevice
            {
                ProductName = "Samsung Galaxy A05",
                Images = "https://ranalocredit.com/a.png, https://ranalocredit.com/b.png",
            };
            var view = CustomerAppRules.Account(Report(), null, device, Now);
            Assert.That(view.DeviceName, Is.EqualTo("Samsung Galaxy A05"));
            Assert.That(view.PhotoUrl, Is.EqualTo("https://ranalocredit.com/a.png"));
            Assert.That(CustomerAppRules.FirstPhoto("http://insecure.png"), Is.Null);
        }

        [Test]
        public void Statement_balance_after_the_last_payment_is_the_report_balance()
        {
            var payments = new List<MyPayment>
            {
                new() { MpesaCode = "B", Amount = 100m, PaidAt = new DateTime(2026, 11, 2) },
                new() { MpesaCode = "A", Amount = 3368m, PaidAt = new DateTime(2026, 11, 1) },
                new() { MpesaCode = "C", Amount = 68m, PaidAt = new DateTime(2026, 11, 3) },
            };
            var lines = CustomerAppRules.Statement(payments, 10000m);

            Assert.That(lines.Select(l => l.MpesaCode), Is.EqualTo(new[] { "C", "B", "A" }));
            Assert.That(lines[0].PaidSoFar, Is.EqualTo(3536m));
            Assert.That(lines[0].BalanceAfter, Is.EqualTo(10000m - 3536m));
            Assert.That(lines[2].BalanceAfter, Is.EqualTo(10000m - 3368m));
        }

        [Test]
        public void Email_phone_and_ID_are_required()
        {
            var access = new CustomerAccess { Email = "a@b.co", Phone = "254712345678", NationalId = "12345678" };
            Assert.That(CustomerAppRules.Missing(access), Is.Empty);

            access.Email = "not-an-email"; access.Phone = null; access.NationalId = "12";
            Assert.That(CustomerAppRules.Missing(access), Is.EqualTo(new[] { "email", "M-Pesa number", "national ID" }));
        }

        // ---------- Inbox ----------

        [Test]
        public void Inbox_reads_Nuovo_Knox_and_Transsion_messages_alike()
        {
            var message = CustomerMessages.PaymentReceived("Jane", 300m, "12345678", "TJK12AB34C", new DateTime(2026, 10, 27, 14, 10, 0));
            var sentAt = new DateTime(2026, 11, 6, 9, 0, 0);

            foreach (var stored in new[] { CustomerMessages.ForNuovoKnox(message), CustomerMessages.ForKnox(message), message.Full })
            {
                var item = CustomerAppRules.InboxItem(new MessageLog { Message = stored, MessageType = "Payment", MessageStatus = "sent", DateSent = sentAt });

                Assert.That(item.Title, Is.EqualTo("Payment received: Ksh 300"), stored);
                Assert.That(item.Lines, Has.Some.Contains("TJK12AB34C"), stored);
                Assert.That(item.Kind, Is.EqualTo("Receipt"));
                Assert.That(item.SentAtKenya, Is.EqualTo(sentAt.AddHours(3)));
                Assert.That(item.Delivered, Is.True);
            }
        }

        // ---------- Staff page and preview ----------

        [TestCase(Ranalo.DataStore.DataModels.UserRole.Admin, true)]
        [TestCase(Ranalo.DataStore.DataModels.UserRole.Approver, true)]
        [TestCase(Ranalo.DataStore.DataModels.UserRole.Dealer, false)]
        [TestCase(Ranalo.DataStore.DataModels.UserRole.Agent, false)]
        [TestCase(Ranalo.DataStore.DataModels.UserRole.Collector, false)]
        public void Only_admin_and_approver_manage_the_app(Ranalo.DataStore.DataModels.UserRole role, bool allowed)
        {
            Assert.That(Ranalo.Controllers.CustomerAppAccessController.CanManage(role), Is.EqualTo(allowed));
        }

        [Test]
        public void A_preview_works_only_for_the_staff_member_who_started_it()
        {
            var session = new CustomerSession(new Microsoft.AspNetCore.DataProtection.EphemeralDataProtectionProvider());
            var response = new DefaultHttpContext().Response;
            session.StartPreview(response, 1234567, staffUserId: 12);

            var cookie = response.Headers.SetCookie.ToString();
            var value = cookie[(cookie.IndexOf('=') + 1)..cookie.IndexOf(';')];
            var request = new DefaultHttpContext().Request;
            request.Headers.Cookie = $"{CustomerSession.PreviewCookieName}={value}";

            Assert.That(session.ReadPreview(request, 12), Is.EqualTo(1234567));
            Assert.That(session.ReadPreview(request, 13), Is.Null);

            request.Headers.Cookie = $"{CustomerSession.PreviewCookieName}={value}x";
            Assert.That(session.ReadPreview(request, 12), Is.Null);

            // A customer's own cookie can't be used as a preview, nor the other way round.
            Assert.That(session.Read(request), Is.Null);
        }

        // ---------- my.ranalocredit.com ----------

        [TestCase("my.ranalocredit.com", true)]
        [TestCase("ops.ranalocredit.com", false)]
        [TestCase("localhost", false)]
        public void Only_the_my_host_is_the_customer_app(string host, bool customer)
        {
            Assert.That(CustomerHostGate.IsCustomerHost(new HostString(host)), Is.EqualTo(customer));
        }

        [TestCase("/my", true)]
        [TestCase("/my/statement", true)]
        [TestCase("/api/kose/stk/x/callback", true)]
        [TestCase("/css/site.css", true)]
        [TestCase("/pending-tasks", false)]
        [TestCase("/account-details/1234567", false)]
        [TestCase("/login", false)]
        [TestCase("/allpayments", false)]
        public void Staff_pages_are_closed_on_the_my_host(string path, bool allowed)
        {
            Assert.That(CustomerHostGate.PathAllowed(new PathString(path)), Is.EqualTo(allowed));
        }
    }
}
