using Ranalo.DataStore.DataModels;
using Ranalo.Models;
using Ranalo.Services.DeviceLock;

namespace Ranolo.Web.Tests
{
    public class DeviceLockRulesTests
    {
        private static readonly IReadOnlyList<BrandKeyword> Keywords = DeviceLockRules.DefaultBrandKeywords;

        private static Enrolment NewEnrolment(int? enrolledBy = 10, UserRole enrolledRole = UserRole.Dealer) => new()
        {
            IMEI = "356938035643809",
            EnrolledByUserId = enrolledBy,
            EnrolledByRole = (int)enrolledRole,
            Status = EnrolmentStatus.PendingActivation,
            DeviceBrand = DeviceLockRules.Tecno
        };

        // ---- Brand detection ----

        [TestCase("TECNO Spark 20 Pro 8/256", "TECNO")]
        [TestCase("tecno camon 30", "TECNO")]
        [TestCase("itel A70 64GB", "itel")]
        [TestCase("Infinix Hot 40i", "Infinix")]
        [TestCase("Samsung Galaxy A15", "Samsung")]
        [TestCase("Galaxy A05s", "Samsung")]
        // Real product names from Woo_OrderProduct.
        [TestCase("SAMSUNG, Galaxy A42, 5G, 128 GB", "Samsung")]
        [TestCase("Samsung Galaxy M35 5G - Dark Blue", "Samsung")]
        [TestCase("Tecno Camon 20 - Black", "TECNO")]
        [TestCase("Techno Spark 40 Pro - 256GB, Black", "TECNO")]
        [TestCase("Camon 19", "TECNO")]
        [TestCase("Tecno Camon 40 Pro - 256GB, Black", "TECNO")]
        [TestCase("Itel S23+ - Cyan", "itel")]
        [TestCase("Itel A04 - Black", "itel")]
        [TestCase("Infinix Hot 50 Pro+", "Infinix")]
        [TestCase("Infinix Note 40 - Black", "Infinix")]
        public void DetectBrand_FindsBrandInProductName(string productName, string expected)
        {
            Assert.That(DeviceLockRules.DetectBrand(productName, Keywords), Is.EqualTo(expected));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("Oppo A18")]
        [TestCase("Hisense 43\" TV")]
        [TestCase("Redmi A5")]
        [TestCase("TCL 32S5K QLED HDR TV")]
        [TestCase("iPhone 11 Pro")]
        [TestCase("OnePlus Nord N30 SE 5G - Cyan")]
        [TestCase("test")]
        public void DetectBrand_UnknownProduct_ReturnsNull(string? productName)
        {
            Assert.That(DeviceLockRules.DetectBrand(productName, Keywords), Is.Null);
        }

        [Test]
        public void DetectBrand_MatchesWholeWordsOnly()
        {
            // "itel" inside another word must not count as the itel brand.
            Assert.That(DeviceLockRules.DetectBrand("Satellite phone", Keywords), Is.Null);
        }

        [Test]
        public void DetectBrand_TwoBrandsInName_IsAmbiguous()
        {
            Assert.That(DeviceLockRules.DetectBrand("Samsung charger for TECNO", Keywords), Is.Null);
        }

        [Test]
        public void DetectBrand_TwoKeywordsSameBrand_IsNotAmbiguous()
        {
            Assert.That(DeviceLockRules.DetectBrand("Samsung Galaxy A25", Keywords), Is.EqualTo("Samsung"));
        }

        [Test]
        public void DetectBrand_CustomKeywords_NormaliseBrandCase()
        {
            var custom = new[] { new BrandKeyword("Spark", "tecno") };
            Assert.That(DeviceLockRules.DetectBrand("Spark 20", custom), Is.EqualTo("TECNO"));
        }

        [Test]
        public void ProviderForBrand_RoutesTranssionBrandsToTranssion()
        {
            Assert.Multiple(() =>
            {
                Assert.That(DeviceLockRules.ProviderForBrand("Samsung"), Is.EqualTo(LockProvider.Knox));
                Assert.That(DeviceLockRules.ProviderForBrand("itel"), Is.EqualTo(LockProvider.Transsion));
                Assert.That(DeviceLockRules.ProviderForBrand("TECNO"), Is.EqualTo(LockProvider.Transsion));
                Assert.That(DeviceLockRules.ProviderForBrand("Infinix"), Is.EqualTo(LockProvider.Transsion));
                Assert.That(DeviceLockRules.ProviderForBrand(null), Is.EqualTo(LockProvider.Nuovo));
            });
        }

        [Test]
        public void ProviderForLockGroup_MatchesDevicesTable()
        {
            Assert.Multiple(() =>
            {
                Assert.That(DeviceLockRules.ProviderForLockGroup(1), Is.EqualTo(LockProvider.Nuovo));
                Assert.That(DeviceLockRules.ProviderForLockGroup(null), Is.EqualTo(LockProvider.Nuovo));
                Assert.That(DeviceLockRules.ProviderForLockGroup(2), Is.EqualTo(LockProvider.Knox));
                Assert.That(DeviceLockRules.ProviderForLockGroup(3), Is.EqualTo(LockProvider.Transsion));
            });
        }

        // ---- TAC ----

        [Test]
        public void Tac_IsFirstEightDigits()
        {
            Assert.That(DeviceLockRules.Tac("356938035643809"), Is.EqualTo("35693803"));
            Assert.That(DeviceLockRules.Tac("1234"), Is.Null);
            Assert.That(DeviceLockRules.Tac(null), Is.Null);
        }

        [Test]
        public void CheckBrand_KnownTacMatchingOrder_Ok()
        {
            var r = DeviceLockRules.CheckBrand("TECNO", "TECNO");
            Assert.That(r.Result, Is.EqualTo(BrandCheck.Ok));
            Assert.That(r.Blocks, Is.False);
        }

        [Test]
        public void CheckBrand_UnknownTac_PassesButUnverified()
        {
            var r = DeviceLockRules.CheckBrand("TECNO", null);
            Assert.That(r.Result, Is.EqualTo(BrandCheck.TacUnverified));
            Assert.That(r.Blocks, Is.False);
        }

        [Test]
        public void CheckBrand_TacDisagreesWithOrder_Blocks()
        {
            var r = DeviceLockRules.CheckBrand("TECNO", "Samsung");
            Assert.That(r.Result, Is.EqualTo(BrandCheck.Mismatch));
            Assert.That(r.Blocks, Is.True);
            Assert.That(r.Error, Does.Contain("Samsung"));
        }

        [Test]
        public void CheckBrand_NoBrandOnOrder_Blocks()
        {
            var r = DeviceLockRules.CheckBrand(null, "TECNO");
            Assert.That(r.Result, Is.EqualTo(BrandCheck.UnknownBrand));
            Assert.That(r.Blocks, Is.True);
        }

        // ---- Deposit ----

        [Test]
        public void RequiredDeposit_MatchesContractFormula()
        {
            // (10000 + 5000) * 0.235
            Assert.That(DeviceLockRules.RequiredDeposit(10000m, null), Is.EqualTo(3525m));
            // sales price: total - daily * 365
            Assert.That(DeviceLockRules.RequiredDeposit(20000m, 40m), Is.EqualTo(5400m));
        }

        [Test]
        public void CheckDeposit_EnoughOnRightAccount_Ok()
        {
            var r = DeviceLockRules.CheckDeposit("QWE123", "12345678", 3000m,
                new[] { new DepositPayment("QWE123", "12345678", 3000m) });

            Assert.That(r.Result, Is.EqualTo(DepositCheck.Ok));
            Assert.That(r.Blocks, Is.False);
            Assert.That(r.IsShort, Is.False);
            Assert.That(r.Paid, Is.EqualTo(3000m));
        }

        [Test]
        public void CheckDeposit_MatchesRefIgnoringCaseAndSpaces()
        {
            var r = DeviceLockRules.CheckDeposit(" qwe123 ", "12345678", 3000m,
                new[] { new DepositPayment("QWE123", "12345678 ", 3500m) });

            Assert.That(r.Result, Is.EqualTo(DepositCheck.Ok));
        }

        [Test]
        public void CheckDeposit_LessThanRequired_IsShortNotBlocked()
        {
            var r = DeviceLockRules.CheckDeposit("QWE123", "12345678", 3000m,
                new[] { new DepositPayment("QWE123", "12345678", 2000m) });

            Assert.That(r.Result, Is.EqualTo(DepositCheck.Short));
            Assert.That(r.Blocks, Is.False);
            Assert.That(r.IsShort, Is.True);
        }

        [Test]
        public void CheckDeposit_NoRefOnOrder_Blocks()
        {
            var r = DeviceLockRules.CheckDeposit(null, "12345678", 3000m, Array.Empty<DepositPayment>());
            Assert.That(r.Result, Is.EqualTo(DepositCheck.MissingRef));
            Assert.That(r.Blocks, Is.True);
        }

        [Test]
        public void CheckDeposit_RefNotReceived_Blocks()
        {
            var r = DeviceLockRules.CheckDeposit("QWE123", "12345678", 3000m, Array.Empty<DepositPayment>());
            Assert.That(r.Result, Is.EqualTo(DepositCheck.NotFound));
            Assert.That(r.Blocks, Is.True);
        }

        [Test]
        public void CheckDeposit_PaidToAnotherAccount_Blocks()
        {
            var r = DeviceLockRules.CheckDeposit("QWE123", "12345678", 3000m,
                new[] { new DepositPayment("QWE123", "99999999", 5000m) });

            Assert.That(r.Result, Is.EqualTo(DepositCheck.WrongAccount));
            Assert.That(r.Blocks, Is.True);
        }

        // ---- Permissions ----

        [Test]
        public void CanEnrol_DealersApproversAdminsOnly()
        {
            Assert.Multiple(() =>
            {
                Assert.That(DeviceLockRules.CanEnrol(UserRole.Dealer), Is.True);
                Assert.That(DeviceLockRules.CanEnrol(UserRole.Approver), Is.True);
                Assert.That(DeviceLockRules.CanEnrol(UserRole.Admin), Is.True);
                Assert.That(DeviceLockRules.CanEnrol(UserRole.Agent), Is.False);
                Assert.That(DeviceLockRules.CanEnrol(UserRole.Collector), Is.False);
            });
        }

        [Test]
        public void CanApprove_ApproverOnDealerEnrolment_Allowed()
        {
            Assert.That(DeviceLockRules.CanApproveEnrolment(UserRole.Approver, 20, NewEnrolment()).Allowed, Is.True);
        }

        [Test]
        public void CanApprove_ApproverOwnEnrolment_Refused()
        {
            var d = DeviceLockRules.CanApproveEnrolment(UserRole.Approver, 20, NewEnrolment(enrolledBy: 20, UserRole.Approver));
            Assert.That(d.Allowed, Is.False);
            Assert.That(d.Reason, Does.Contain("admin"));
        }

        [Test]
        public void CanApprove_AdminOnApproverEnrolment_Allowed()
        {
            Assert.That(DeviceLockRules.CanApproveEnrolment(UserRole.Admin, 1, NewEnrolment(enrolledBy: 20, UserRole.Approver)).Allowed, Is.True);
        }

        [Test]
        public void CanApprove_DepositShort_AdminOnly()
        {
            var e = NewEnrolment();
            e.DepositShort = true;

            Assert.That(DeviceLockRules.CanApproveEnrolment(UserRole.Approver, 20, e).Allowed, Is.False);
            Assert.That(DeviceLockRules.CanApproveEnrolment(UserRole.Admin, 1, e).Allowed, Is.True);
        }

        [TestCase(UserRole.Dealer)]
        [TestCase(UserRole.Agent)]
        [TestCase(UserRole.Collector)]
        [TestCase(UserRole.Guest)]
        public void CanApprove_OtherRoles_Refused(UserRole role)
        {
            Assert.That(DeviceLockRules.CanApproveEnrolment(role, 99, NewEnrolment()).Allowed, Is.False);
        }

        [Test]
        public void CanApprove_AlreadyApprovedOrErrored_Refused()
        {
            var approved = NewEnrolment();
            approved.CallApprovedAt = DateTime.UtcNow;
            var errored = NewEnrolment();
            errored.Status = EnrolmentStatus.Error;

            Assert.That(DeviceLockRules.CanApproveEnrolment(UserRole.Admin, 1, approved).Allowed, Is.False);
            Assert.That(DeviceLockRules.CanApproveEnrolment(UserRole.Admin, 1, errored).Allowed, Is.False);
        }

        [Test]
        public void CanDecideRemovals_AdminOnly()
        {
            Assert.That(DeviceLockRules.CanDecideRemovals(UserRole.Admin), Is.True);
            Assert.That(DeviceLockRules.CanDecideRemovals(UserRole.Approver), Is.False);
            Assert.That(DeviceLockRules.CanDecideRemovals(UserRole.Dealer), Is.False);
        }

        [Test]
        public void CanOverrideBrand_AdminOnly()
        {
            Assert.That(DeviceLockRules.CanOverrideBrand(UserRole.Admin), Is.True);
            Assert.That(DeviceLockRules.CanOverrideBrand(UserRole.Approver), Is.False);
        }

        // ---- State ----

        [Test]
        public void ReadyToUnlock_NeedsActivationAndApproval_InEitherOrder()
        {
            var e = NewEnrolment();
            Assert.That(DeviceLockRules.ReadyToUnlock(e), Is.False);

            e.CallApprovedAt = DateTime.UtcNow;
            Assert.That(DeviceLockRules.ReadyToUnlock(e), Is.False, "approved but not switched on");

            e.CallApprovedAt = null;
            e.ActivatedAt = DateTime.UtcNow;
            Assert.That(DeviceLockRules.ReadyToUnlock(e), Is.False, "switched on but not approved");

            e.CallApprovedAt = DateTime.UtcNow;
            Assert.That(DeviceLockRules.ReadyToUnlock(e), Is.True);

            e.UnlockedAt = DateTime.UtcNow;
            Assert.That(DeviceLockRules.ReadyToUnlock(e), Is.False, "already unlocked");
        }

        [Test]
        public void WaitingStatus_FollowsActivation()
        {
            var e = NewEnrolment();
            Assert.That(DeviceLockRules.WaitingStatus(e), Is.EqualTo(EnrolmentStatus.PendingActivation));
            e.ActivatedAt = DateTime.UtcNow;
            Assert.That(DeviceLockRules.WaitingStatus(e), Is.EqualTo(EnrolmentStatus.Enrolled));
        }

        [Test]
        public void FullyPaidLockDate_Is31Dec9999()
        {
            Assert.That(DeviceLockRules.FullyPaidLockDateUtc.ToString("dd/MM/yyyy"), Is.EqualTo("31/12/9999"));
        }
    }
}
