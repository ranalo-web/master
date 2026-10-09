using Ranalo.Models;
using Ranalo.Services;

namespace Ranolo.Web.Tests
{
    // Fraud early-warning checks, built from the Newway Electrical case
    // (2026-10-09).
    public class FraudRulesTests
    {
        private static readonly DateTime Today = new(2026, 10, 9);

        private static FraudContractRow Contract(long account, int daysOld = 30, decimal deposit = 3_000m, decimal paid = 3_000m,
            bool hasDevice = true, int? dealerId = 1, string dealer = "Dealer A") => new()
        {
            AccountNo = account,
            ContractId = (int)(account % 100_000),
            CustomerName = "Customer " + account,
            DealerId = dealerId,
            DealerName = dealer,
            StartDate = Today.AddDays(-daysOld),
            Deposit = deposit,
            ContractValue = 20_000m,
            TotalPaid = paid,
            HasDevice = hasDevice,
            Locked = true,
        };

        private static FraudOrderRow Order(long id, string nationalId, string? phone = null, string? imei = null,
            string? mpesa = null, long? account = null) => new()
        {
            OrderId = id,
            Status = "approval-waiting",
            DateCreated = Today.AddDays(-2),
            CustomerName = "Order " + id,
            NationalId = nationalId,
            Phone = phone,
            Imei = imei,
            MpesaDepositRef = mpesa,
            AccountNo = account,
        };

        private static List<FraudFlag> Run(IEnumerable<FraudContractRow>? contracts = null, IEnumerable<FraudOrderRow>? orders = null,
            IEnumerable<FraudNextOfKinRow>? nok = null, IEnumerable<FraudDeviceRow>? devices = null) =>
            FraudRules.Evaluate(contracts ?? Array.Empty<FraudContractRow>(), orders ?? Array.Empty<FraudOrderRow>(),
                nok ?? Array.Empty<FraudNextOfKinRow>(), devices ?? Array.Empty<FraudDeviceRow>(), Today);

        [Test]
        public void PhonesAreComparedByTheirNineDigits()
        {
            Assert.That(FraudRules.NormalizePhone("0722 638 538"), Is.EqualTo("722638538"));
            Assert.That(FraudRules.NormalizePhone("+254722638538"), Is.EqualTo("722638538"));
            Assert.That(FraudRules.NormalizePhone("0110123456"), Is.EqualTo("110123456"));
            Assert.That(FraudRules.NormalizePhone("087887"), Is.Null);
        }

        [Test]
        public void JunkIdsImeisAndDepositCodesAreIgnored()
        {
            Assert.That(FraudRules.NormalizeNationalId("22hh..**88"), Is.Null);
            Assert.That(FraudRules.NormalizeNationalId(" 0040143065 "), Is.EqualTo("40143065"));
            Assert.That(FraudRules.NormalizeImei("35825340118053"), Is.Null);
            Assert.That(FraudRules.NormalizeImei("358253401180537"), Is.EqualTo("358253401180537"));
            Assert.That(FraudRules.NormalizeMpesaRef("MPESA123"), Is.Null);
            Assert.That(FraudRules.NormalizeMpesaRef("KKKKKKKKKK"), Is.Null);
            Assert.That(FraudRules.NormalizeMpesaRef("seo286bs6i"), Is.EqualTo("SEO286BS6I"));
        }

        [Test]
        public void DepositOnlyNeedsFourteenDaysAndNothingBeyondTheDeposit()
        {
            var flags = Run(new[]
            {
                Contract(1, daysOld: 14, paid: 3_100m),   // within 5% of the deposit
                Contract(2, daysOld: 13, paid: 3_000m),   // too new
                Contract(3, daysOld: 60, paid: 4_000m),   // has paid instalments
            });

            Assert.That(flags.Where(f => f.CheckCode == FraudCheckCodes.DepositOnly).Select(f => f.AccountNo), Is.EqualTo(new long?[] { 1 }));
            Assert.That(flags.Single().AmountAtRisk, Is.EqualTo(16_900m));
            Assert.That(flags.Single().FlaggedOn, Is.EqualTo(Today));
        }

        [Test]
        public void ContractWithoutDeviceIsFlagged()
        {
            var flags = Run(new[] { Contract(691, paid: 5_000m, hasDevice: false) });
            Assert.That(flags.Select(f => f.CheckCode), Is.EqualTo(new[] { FraudCheckCodes.NoDevice }));
        }

        [Test]
        public void LockOverdueOnlyWhenUnlockedAndBehind()
        {
            var overdue = Contract(10, paid: 8_000m);
            overdue.Locked = false; overdue.NextLockDate = "28/05/2026"; overdue.Shortfall = 4_000m;
            var paidUp = Contract(11, paid: 8_000m);
            paidUp.Locked = false; paidUp.NextLockDate = "28/05/2026"; paidUp.Shortfall = 0m;
            var locked = Contract(12, paid: 8_000m);
            locked.Locked = true; locked.NextLockDate = "28/05/2026"; locked.Shortfall = 4_000m;

            var flags = Run(new[] { overdue, paidUp, locked });
            Assert.That(flags.Select(f => f.AccountNo), Is.EqualTo(new long?[] { 10 }));
            Assert.That(flags.Single().CheckCode, Is.EqualTo(FraudCheckCodes.LockOverdue));
        }

        [Test]
        public void ImeiOnAnotherCustomersAccountIsFlagged()
        {
            // Harrison's phone (8680519) was put on orders for two other people.
            var devices = new[] { new FraudDeviceRow { AccountNo = 8680519, Imei = "355132188643698", CustomerName = "Customer" } };
            var flags = Run(orders: new[]
            {
                Order(18804, "50522869", imei: "355132188643698"),
                Order(18805, "29399689", imei: "355132188643698"),
            }, devices: devices);

            var onOther = flags.Where(f => f.CheckCode == FraudCheckCodes.ImeiOnOtherAccount).ToList();
            Assert.That(onOther.Select(f => f.SubjectKey), Is.EquivalentTo(new[] { "order:18804", "order:18805" }));
            Assert.That(flags.Count(f => f.CheckCode == FraudCheckCodes.ImeiReused), Is.EqualTo(1));
        }

        [Test]
        public void ImeiOnTheCustomersOwnAccountIsNotFlagged()
        {
            var devices = new[]
            {
                // Knox: the account is the ID number, or the ID plus a digit.
                new FraudDeviceRow { AccountNo = 818985102, Imei = "111111111111111" },
                new FraudDeviceRow { AccountNo = 408985102, Imei = "222222222222222" },
                // Nuovo: device number, but the contract's order is this customer's.
                new FraudDeviceRow { AccountNo = 7260267, Imei = "333333333333333", ContractOrderNationalId = "41321974" },
                // The order's own contract.
                new FraudDeviceRow { AccountNo = 7334838, Imei = "444444444444444" },
            };
            var flags = Run(orders: new[]
            {
                Order(1, "818985102", imei: "111111111111111"),
                Order(2, "40898510", imei: "222222222222222"),
                Order(3, "41321974", imei: "333333333333333"),
                Order(4, "40556101", imei: "444444444444444", account: 7334838),
            }, devices: devices);

            Assert.That(flags, Is.Empty);
        }

        [Test]
        public void PhoneOrDepositCodeOnDifferentIdsIsFlagged()
        {
            var flags = Run(orders: new[]
            {
                Order(1, "11111111", phone: "0796733873", mpesa: "SEO286BS6I"),
                Order(2, "22222222", phone: "+254 796 733 873", mpesa: "seo286bs6i"),
                Order(3, "33333333", phone: "0700000001"),
                // Same person twice is fine.
                Order(4, "44444444", phone: "0700000002"),
                Order(5, "044444444", phone: "0700000002"),
            });

            Assert.That(flags.Select(f => (f.CheckCode, f.SubjectKey)), Is.EquivalentTo(new[]
            {
                (FraudCheckCodes.PhoneMultiId, "796733873"),
                (FraudCheckCodes.DepositRefReused, "SEO286BS6I"),
            }));
            Assert.That(flags.First().OrderIds, Is.EqualTo(new long[] { 1, 2 }));
        }

        [Test]
        public void NextOfKinSharedByThreeCustomersIsFlagged()
        {
            var orders = new[] { Order(1, "11111111"), Order(2, "22222222"), Order(3, "33333333"), Order(4, "44444444") };
            var nok = new[]
            {
                new FraudNextOfKinRow { OrderId = 1, Phone = "0711111111", IdNumber = "99999999" },
                new FraudNextOfKinRow { OrderId = 2, Phone = "0711111111", IdNumber = "99999999" },
                new FraudNextOfKinRow { OrderId = 3, Phone = "0711111111" },
                new FraudNextOfKinRow { OrderId = 4, Phone = "0722222222" },
            };

            var flags = Run(orders: orders, nok: nok);
            Assert.That(flags.Select(f => f.SubjectKey), Is.EqualTo(new[] { "phone:711111111" }));
        }

        [Test]
        public void DealerWithTwiceTheDepositOnlyRateIsHighRisk()
        {
            var contracts = new List<FraudContractRow>();
            long account = 1;
            // Newway-like dealer: 6 of 20 deposit only (30%).
            for (var i = 0; i < 20; i++) contracts.Add(Contract(account++, paid: i < 6 ? 3_000m : 9_000m, dealerId: 13, dealer: "Newway"));
            // Everyone else: 2 of 60 (3%).
            for (var i = 0; i < 60; i++) contracts.Add(Contract(account++, paid: i < 2 ? 3_000m : 9_000m, dealerId: 4, dealer: "Lucky"));

            var flags = Run(contracts);
            var risks = FraudRules.DealerRisks(contracts, flags, Today);

            Assert.That(FraudRules.DepositOnlyRate(contracts, Today), Is.EqualTo(0.1m));
            Assert.That(risks[0].DealerName, Is.EqualTo("Newway"));
            Assert.That(risks[0].Level, Is.EqualTo(FraudRiskLevel.High));
            Assert.That(risks[0].AmountAtRisk, Is.EqualTo(6 * 17_000m));
            Assert.That(risks.Single(d => d.DealerId == 4).Level, Is.EqualTo(FraudRiskLevel.Normal));
        }

        [Test]
        public void ClearedFlagsDontCountAndConfirmedFraudMakesTheDealerHighRisk()
        {
            var contracts = new[] { Contract(1, hasDevice: false, paid: 9_000m), Contract(2, hasDevice: false, paid: 9_000m) };
            var flags = Run(contracts);
            flags[0].Review = new FraudReview { Status = FraudReviewStatus.Cleared };

            var dealer = FraudRules.DealerRisks(contracts, flags, Today).Single();
            Assert.That(dealer.OtherFlags, Is.EqualTo(1));
            Assert.That(dealer.Level, Is.EqualTo(FraudRiskLevel.Normal));

            flags[1].Review = new FraudReview { Status = FraudReviewStatus.Confirmed };
            Assert.That(FraudRules.DealerRisks(contracts, flags, Today).Single().Level, Is.EqualTo(FraudRiskLevel.High));

            var summary = FraudRules.Summaries(flags, Today).Single(s => s.Check.Code == FraudCheckCodes.NoDevice);
            Assert.That(summary.Open, Is.EqualTo(1));
            Assert.That(summary.Confirmed, Is.EqualTo(1));
        }
            [Test]
        public void DealersAndAgentsSeeANeutralStatusAndNeverLockOverdue()
        {
            var depositOnly = Contract(1, paid: 3_000m, dealerId: 13);
            depositOnly.AgentUserId = 42;
            var overdue = Contract(2, paid: 9_000m, dealerId: 13);
            overdue.Locked = false; overdue.NextLockDate = "28/05/2026"; overdue.Shortfall = 1_000m;
            var orders = new[]
            {
                Order(18804, "50522869", imei: "357095838643699"),
                Order(18805, "29399689", imei: "357095838643699", account: 7000001),
            };
            orders[1].DealerId = 14;

            var flags = Run(new[] { depositOnly, overdue }, orders);
            Assert.That(flags.Any(f => f.CheckCode == FraudCheckCodes.LockOverdue), Is.True);

            var items = FraudRules.VerificationItems(flags, new[] { depositOnly, overdue }, orders);
            Assert.That(items.Select(i => (i.AccountNo, i.OrderId)), Is.EquivalentTo(new (long?, long?)[]
            {
                (1, null), (null, 18804), (7000001, 18805),
            }));
            Assert.That(items.All(i => i.Status == VerificationStatus.UnderVerification), Is.True);
            Assert.That(items.Single(i => i.AccountNo == 1).AgentUserId, Is.EqualTo(42));
            Assert.That(items.Single(i => i.OrderId == 18805).DealerId, Is.EqualTo(14));
        }

        [Test]
        public void ConfirmedShowsAsFailedAndClearedDisappears()
        {
            var a = Contract(1, paid: 3_000m);
            var b = Contract(2, paid: 3_000m);
            var flags = Run(new[] { a, b });
            flags.Single(f => f.AccountNo == 1).Review = new FraudReview { Status = FraudReviewStatus.Confirmed };
            flags.Single(f => f.AccountNo == 2).Review = new FraudReview { Status = FraudReviewStatus.Cleared };

            var items = FraudRules.VerificationItems(flags, new[] { a, b }, Array.Empty<FraudOrderRow>());
            Assert.That(items.Single().AccountNo, Is.EqualTo(1));
            Assert.That(items.Single().Status, Is.EqualTo(VerificationStatus.Failed));
        }
    }
}
