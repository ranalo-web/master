using Microsoft.Extensions.Logging.Abstractions;
using Ranalo.DataStore.DataModels;
using Ranalo.Models;
using Ranalo.PayTrigger.Models;
using Ranalo.ScheduledServices;
using Ranalo.Services;
using Ranalo.Services.DeviceLock;
using Device = Ranalo.Woocommece.Api.Models.Device;
using MobileStatusReport = Ranalo.Models.MobileStatusReport;
using System.Net;

namespace Ranolo.Web.Tests
{
    public class TranssionEnrolmentWorkflowTests
    {
        private FakeEnrolmentRepository _enrolments = null!;
        private FakeDevices _devices = null!;
        private FakePayTrigger _payTrigger = null!;
        private FakeDeviceLockRepository _lockRepo = null!;
        private FakeTime _time = null!;
        private TranssionEnrolmentWorkflow _workflow = null!;

        private static readonly User Approver = new() { UserId = 20, RoleId = UserRole.Approver, Name = "Approver" };
        private static readonly User Admin = new() { UserId = 1, RoleId = UserRole.Admin, Name = "Admin" };
        private static readonly User Dealer = new() { UserId = 10, RoleId = UserRole.Dealer, Name = "Dealer" };

        [SetUp]
        public void SetUp()
        {
            _enrolments = new FakeEnrolmentRepository();
            _devices = new FakeDevices();
            _payTrigger = new FakePayTrigger();
            _lockRepo = new FakeDeviceLockRepository();
            _time = new FakeTime();
            _workflow = new TranssionEnrolmentWorkflow(_enrolments, _devices, _payTrigger, _lockRepo, _time,
                NullLogger<TranssionEnrolmentWorkflow>.Instance);
        }

        private Enrolment NewEnrolment(int enrolledBy = 10, UserRole role = UserRole.Dealer, bool depositShort = false)
        {
            var e = new Enrolment
            {
                Id = Guid.NewGuid(),
                AccountId = 30111222,
                OrderId = 5001,
                FirstName = "Jane",
                IMEI = "356938035643809",
                DeviceBrand = "TECNO",
                ProductName = "Tecno Spark 20",
                Status = EnrolmentStatus.New,
                EnrolledByUserId = enrolledBy,
                EnrolledByRole = (int)role,
                DepositShort = depositShort
            };
            _enrolments.Items.Add(e);
            return e;
        }

        private async Task<Enrolment> PreEnrolledAsync(int enrolledBy = 10, UserRole role = UserRole.Dealer, bool depositShort = false)
        {
            var e = NewEnrolment(enrolledBy, role, depositShort);
            await _workflow.PreEnrolAsync(e, deviceGroupId: 1234);
            return e;
        }

        // ---- Pre-enrol ----

        [Test]
        public async Task PreEnrol_LocksOnActivation_AndWaitsForCustomer()
        {
            var e = await PreEnrolledAsync();

            Assert.Multiple(() =>
            {
                Assert.That(_payTrigger.PreEnrolCalls, Has.Count.EqualTo(1));
                Assert.That(_payTrigger.PreEnrolCalls[0].PreLock, Is.True, "must lock as soon as it's switched on");
                Assert.That(_payTrigger.PreEnrolCalls[0].Items.Single().Imei, Is.EqualTo(e.IMEI));
                Assert.That(_payTrigger.PreEnrolCalls[0].Items.Single().OrderNum, Is.EqualTo("5001"));
                Assert.That(e.Status, Is.EqualTo(EnrolmentStatus.PendingActivation));
                Assert.That(_payTrigger.RepayCalls, Is.Empty, "nothing unlocks at upload");
            });
        }

        [Test]
        public async Task PreEnrol_CreatesDeviceRowTheLockJobsIgnore()
        {
            var e = await PreEnrolledAsync();
            var device = _devices.ById[e.AccountId];

            Assert.Multiple(() =>
            {
                Assert.That(device.Status, Is.EqualTo("pending_activation"), "lock jobs only touch 'enrolled'");
                Assert.That(device.Locked, Is.True);
                Assert.That(device.IsActivated, Is.False);
                Assert.That(device.LockGroup, Is.EqualTo(3));
                Assert.That(device.ImeiNo, Is.EqualTo(e.IMEI));
                Assert.That(device.DeviceGroupId, Is.EqualTo(1234));
                Assert.That(device.Make, Is.EqualTo("TECNO"));
            });
        }

        [Test]
        public async Task PreEnrol_TranssionRefusesImei_Error()
        {
            _payTrigger.PreEnrolResponse = new PreEnrollImeiResponse
            {
                Code = 200, Message = "Success",
                Data = new List<PreEnrollImeiResultItem> { new() { Imei = "356938035643809", Message = "IMEI exists", ErrCode = 123 } }
            };

            var e = await PreEnrolledAsync();

            Assert.That(e.Status, Is.EqualTo(EnrolmentStatus.Error));
            Assert.That(e.PayTriggerResponse, Does.Contain("IMEI exists"));
            Assert.That(_devices.ById, Is.Empty, "no device row for a refused IMEI");
        }

        [Test]
        public async Task PreEnrol_ApiError_Error()
        {
            _payTrigger.PreEnrolResponse = new PreEnrollImeiResponse { Code = 40000, Message = "Sign error" };
            var e = await PreEnrolledAsync();
            Assert.That(e.Status, Is.EqualTo(EnrolmentStatus.Error));
            Assert.That(e.PayTriggerResponse, Does.Contain("Sign error"));
        }

        [Test]
        public async Task PreEnrol_TranssionUnreachable_Error()
        {
            _payTrigger.Throw = new HttpRequestException("timeout");
            var e = await PreEnrolledAsync();
            Assert.That(e.Status, Is.EqualTo(EnrolmentStatus.Error));
            Assert.That(e.PayTriggerResponse, Does.Contain("timeout"));
        }

        [Test]
        public async Task PreEnrol_AccountAlreadyHasAnotherLiveDevice_ErrorWithoutCallingTranssion()
        {
            _devices.ById[30111222] = new Device { Id = 30111222, ImeiNo = "111111111111111", Status = "enrolled" };
            var e = await PreEnrolledAsync();

            Assert.That(e.Status, Is.EqualTo(EnrolmentStatus.Error));
            Assert.That(_payTrigger.PreEnrolCalls, Is.Empty);
            Assert.That(_devices.ById[30111222].ImeiNo, Is.EqualTo("111111111111111"), "old device untouched");
        }

        [Test]
        public async Task PreEnrol_AccountsPreviousDeviceRemoved_ReusesRow()
        {
            _devices.ById[30111222] = new Device { Id = 30111222, ImeiNo = "111111111111111", Status = "enrolled", EnrollmentStatus = "Removed" };
            var e = await PreEnrolledAsync();

            Assert.That(e.Status, Is.EqualTo(EnrolmentStatus.PendingActivation));
            Assert.That(_devices.ById[30111222].ImeiNo, Is.EqualTo(e.IMEI));
            Assert.That(_devices.ById[30111222].Status, Is.EqualTo("pending_activation"));
        }

        // ---- Activation then approval ----

        [Test]
        public async Task Activation_BeforeApproval_EnrolledButStillLocked()
        {
            var e = await PreEnrolledAsync();

            await _workflow.OnActivatedAsync(e.IMEI);

            Assert.Multiple(() =>
            {
                Assert.That(e.Status, Is.EqualTo(EnrolmentStatus.Enrolled));
                Assert.That(e.ActivatedAt, Is.EqualTo(_time.Now.UtcDateTime));
                Assert.That(e.UnlockedAt, Is.Null);
                Assert.That(_payTrigger.RepayCalls, Is.Empty, "no unlock before the approval call");
                Assert.That(_devices.ById[e.AccountId].Status, Is.EqualTo("pending_activation"));
            });
        }

        [Test]
        public async Task Activation_ThenApproval_Unlocks()
        {
            var e = await PreEnrolledAsync();
            await _workflow.OnActivatedAsync(e.IMEI);

            var result = await _workflow.ApproveCallAsync(e.Id, Approver);

            AssertUnlocked(e, result);
            Assert.That(e.CallApprovedByUserId, Is.EqualTo(Approver.UserId));
        }

        [Test]
        public async Task Approval_ThenActivation_UnlocksOnWebhook()
        {
            var e = await PreEnrolledAsync();

            var result = await _workflow.ApproveCallAsync(e.Id, Approver);
            Assert.That(result.Success, Is.True);
            Assert.That(result.Message, Does.Contain("switches it on"));
            Assert.That(e.Status, Is.EqualTo(EnrolmentStatus.PendingActivation));
            Assert.That(_payTrigger.RepayCalls, Is.Empty, "not switched on yet");

            await _workflow.OnActivatedAsync(e.IMEI);

            AssertUnlocked(e, null);
        }

        private void AssertUnlocked(Enrolment e, WorkflowResult? result)
        {
            var device = _devices.ById[e.AccountId];
            var expectedLock = _time.Now.Add(TranssionEnrolmentWorkflow.FirstUnlockPeriod).ToUnixTimeSeconds();

            Assert.Multiple(() =>
            {
                if (result != null)
                {
                    Assert.That(result.Success, Is.True, result.Message);
                }
                Assert.That(_payTrigger.RepayCalls, Has.Count.EqualTo(1));
                Assert.That(_payTrigger.RepayCalls[0].Imei, Is.EqualTo(e.IMEI));
                Assert.That(_payTrigger.RepayCalls[0].NextRepayTime, Is.EqualTo(expectedLock));
                Assert.That(e.Status, Is.EqualTo(EnrolmentStatus.Approved));
                Assert.That(e.UnlockedAt, Is.Not.Null);
                Assert.That(device.Status, Is.EqualTo("enrolled"), "now picked up by the paying-lock job");
                Assert.That(device.Locked, Is.False);
                Assert.That(device.IsActivated, Is.True);
                Assert.That(device.NextLockDate, Is.EqualTo("07/10/2026"));
            });
        }

        [Test]
        public async Task Activation_RepeatedWebhook_UnlocksOnlyOnce()
        {
            var e = await PreEnrolledAsync();
            await _workflow.ApproveCallAsync(e.Id, Approver);

            await _workflow.OnActivatedAsync(e.IMEI);
            await _workflow.OnActivatedAsync(e.IMEI);

            Assert.That(_payTrigger.RepayCalls, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task Activation_LearnsTacForTheBrand()
        {
            var e = await PreEnrolledAsync();
            await _workflow.OnActivatedAsync(e.IMEI);
            Assert.That(_lockRepo.Tacs["35693803"], Is.EqualTo("TECNO"));
        }

        [Test]
        public async Task Activation_UnknownImei_Ignored()
        {
            await _workflow.OnActivatedAsync("999999999999999");
            Assert.That(_enrolments.UpdateCount, Is.Zero);
        }

        [Test]
        public async Task Activation_SamsungEnrolment_Ignored()
        {
            var e = NewEnrolment();
            e.DeviceBrand = "Samsung";
            await _workflow.OnActivatedAsync(e.IMEI);
            Assert.That(e.ActivatedAt, Is.Null);
        }

        // ---- Approval permissions ----

        [Test]
        public async Task Approval_ApproverOwnEnrolment_RefusedAndNothingChanges()
        {
            var e = await PreEnrolledAsync(enrolledBy: Approver.UserId, role: UserRole.Approver);
            await _workflow.OnActivatedAsync(e.IMEI);

            var result = await _workflow.ApproveCallAsync(e.Id, Approver);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Contain("admin"));
            Assert.That(e.CallApprovedAt, Is.Null);
            Assert.That(_payTrigger.RepayCalls, Is.Empty);
        }

        [Test]
        public async Task Approval_AdminOnApproverEnrolment_Unlocks()
        {
            var e = await PreEnrolledAsync(enrolledBy: Approver.UserId, role: UserRole.Approver);
            await _workflow.OnActivatedAsync(e.IMEI);

            var result = await _workflow.ApproveCallAsync(e.Id, Admin);

            AssertUnlocked(e, result);
        }

        [Test]
        public async Task Approval_DepositShort_ApproverRefused_AdminAllowed()
        {
            var e = await PreEnrolledAsync(depositShort: true);
            await _workflow.OnActivatedAsync(e.IMEI);

            var byApprover = await _workflow.ApproveCallAsync(e.Id, Approver);
            Assert.That(byApprover.Success, Is.False);
            Assert.That(_payTrigger.RepayCalls, Is.Empty);

            var byAdmin = await _workflow.ApproveCallAsync(e.Id, Admin);
            AssertUnlocked(e, byAdmin);
        }

        [Test]
        public async Task Approval_Dealer_Refused()
        {
            var e = await PreEnrolledAsync(enrolledBy: 99);
            var result = await _workflow.ApproveCallAsync(e.Id, Dealer);
            Assert.That(result.Success, Is.False);
            Assert.That(e.CallApprovedAt, Is.Null);
        }

        [Test]
        public async Task Approval_Twice_SecondRefused()
        {
            var e = await PreEnrolledAsync();
            await _workflow.ApproveCallAsync(e.Id, Approver);
            var again = await _workflow.ApproveCallAsync(e.Id, Admin);
            Assert.That(again.Success, Is.False);
            Assert.That(e.CallApprovedByUserId, Is.EqualTo(Approver.UserId));
        }

        [Test]
        public async Task Approval_PreEnrolFailed_Refused()
        {
            _payTrigger.PreEnrolResponse = new PreEnrollImeiResponse { Code = 40000, Message = "Sign error" };
            var e = await PreEnrolledAsync();
            var result = await _workflow.ApproveCallAsync(e.Id, Admin);
            Assert.That(result.Success, Is.False);
        }

        [Test]
        public async Task Approval_SamsungEnrolment_Refused()
        {
            var e = NewEnrolment();
            e.DeviceBrand = "Samsung";
            e.Status = EnrolmentStatus.Pending;
            var result = await _workflow.ApproveCallAsync(e.Id, Admin);
            Assert.That(result.Success, Is.False);
        }

        // ---- Unlock failures ----

        [Test]
        public async Task Unlock_TranssionRefuses_StaysLockedAndCanRetry()
        {
            var e = await PreEnrolledAsync();
            await _workflow.OnActivatedAsync(e.IMEI);
            _payTrigger.RepayResponse = new PayTriggerApiResponse { Code = 50071, Message = "The device-lock not exist" };

            var result = await _workflow.ApproveCallAsync(e.Id, Approver);

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.False);
                Assert.That(result.Message, Does.Contain("Retry unlock"));
                Assert.That(e.UnlockedAt, Is.Null);
                Assert.That(e.Status, Is.EqualTo(EnrolmentStatus.Enrolled));
                Assert.That(_devices.ById[e.AccountId].Status, Is.EqualTo("pending_activation"), "lock jobs still ignore it");
                Assert.That(DeviceLockRules.ReadyToUnlock(e), Is.True);
            });

            _payTrigger.RepayResponse = new PayTriggerApiResponse { Code = 200, Message = "Success" };
            var retry = await _workflow.RetryUnlockAsync(e.Id, Approver);

            Assert.That(retry.Success, Is.True, retry.Message);
            Assert.That(e.Status, Is.EqualTo(EnrolmentStatus.Approved));
        }

        [Test]
        public async Task Unlock_TranssionUnreachableOnWebhook_DoesNotThrow()
        {
            var e = await PreEnrolledAsync();
            await _workflow.ApproveCallAsync(e.Id, Approver);
            _payTrigger.Throw = new HttpRequestException("down");

            Assert.DoesNotThrowAsync(() => _workflow.OnActivatedAsync(e.IMEI));
            Assert.That(e.ActivatedAt, Is.Not.Null);
            Assert.That(e.UnlockedAt, Is.Null);
        }

        [Test]
        public async Task RetryUnlock_NotReady_Refused()
        {
            var e = await PreEnrolledAsync();
            var result = await _workflow.RetryUnlockAsync(e.Id, Admin);
            Assert.That(result.Success, Is.False);
            Assert.That(_payTrigger.RepayCalls, Is.Empty);
        }

        [Test]
        public async Task RetryUnlock_Dealer_Refused()
        {
            var e = await PreEnrolledAsync();
            e.ActivatedAt = DateTime.UtcNow;
            e.CallApprovedAt = DateTime.UtcNow;
            var result = await _workflow.RetryUnlockAsync(e.Id, Dealer);
            Assert.That(result.Success, Is.False);
            Assert.That(_payTrigger.RepayCalls, Is.Empty);
        }
    }

    public class EnrolmentCheckServiceTests
    {
        private FakeDeviceLockRepository _repo = null!;
        private EnrolmentCheckService _service = null!;

        [SetUp]
        public void SetUp()
        {
            _repo = new FakeDeviceLockRepository();
            _repo.Products[5001] = new List<string> { "Tecno Spark 20" };
            _repo.Payments.Add(new DepositPayment("QWE123", "30111222", 5000m));
            _repo.Dealers["D042"] = 42;
            _service = new EnrolmentCheckService(_repo);
        }

        private static Enrolment Enrolment() => new() { AccountId = 30111222, OrderId = 5001, IMEI = "356938035643809" };

        // (10000 + 5000) * 0.235 = 3525 required
        private static CustomerDetails Order() => new() { OrderID = 5001, TotalAmount = 10000m, MpesaDepositRef = "QWE123", DealerRef = "D042" };

        [Test]
        public async Task AllGood_TecnoOrder_PassesWithBrandAndDealer()
        {
            var r = await _service.EvaluateAsync(Enrolment(), Order(), UserRole.Approver, null);

            Assert.Multiple(() =>
            {
                Assert.That(r.Errors, Is.Empty);
                Assert.That(r.Brand, Is.EqualTo("TECNO"));
                Assert.That(r.ProductName, Is.EqualTo("Tecno Spark 20"));
                Assert.That(r.BrandBlocked, Is.False);
                Assert.That(r.Deposit.Result, Is.EqualTo(DepositCheck.Ok));
                Assert.That(r.Deposit.Required, Is.EqualTo(3525m));
                Assert.That(r.OrderDealerId, Is.EqualTo(42));
            });
        }

        [Test]
        public async Task EmptyKeywordTable_UsesDefaults()
        {
            Assert.That(_repo.Keywords, Is.Empty);
            var r = await _service.EvaluateAsync(Enrolment(), Order(), UserRole.Dealer, null);
            Assert.That(r.Brand, Is.EqualTo("TECNO"));
        }

        [Test]
        public async Task KeywordTable_OverridesDefaults()
        {
            _repo.Keywords.Add(new BrandKeyword("Spark", "Infinix"));
            var r = await _service.EvaluateAsync(Enrolment(), Order(), UserRole.Dealer, null);
            Assert.That(r.Brand, Is.EqualTo("Infinix"));
        }

        [Test]
        public async Task AccessoryOnOrder_IgnoredWhenItNamesNoBrand()
        {
            _repo.Products[5001] = new List<string> { "Screen protector", "Tecno Spark 20" };
            var r = await _service.EvaluateAsync(Enrolment(), Order(), UserRole.Dealer, null);
            Assert.That(r.Brand, Is.EqualTo("TECNO"));
            Assert.That(r.ProductName, Is.EqualTo("Tecno Spark 20"));
        }

        [Test]
        public async Task TwoBrandsOnOrder_Blocked()
        {
            _repo.Products[5001] = new List<string> { "Samsung Galaxy A05", "Tecno Spark 20" };
            var r = await _service.EvaluateAsync(Enrolment(), Order(), UserRole.Dealer, null);
            Assert.That(r.BrandBlocked, Is.True);
            Assert.That(r.BrandError, Does.Contain("more than one brand"));
        }

        [Test]
        public async Task UnrecognisedProduct_Blocked()
        {
            _repo.Products[5001] = new List<string> { "Redmi A5" };
            var r = await _service.EvaluateAsync(Enrolment(), Order(), UserRole.Dealer, null);
            Assert.That(r.Brand, Is.Null);
            Assert.That(r.BrandBlocked, Is.True);
        }

        [Test]
        public async Task KnownTacForAnotherBrand_Blocked()
        {
            _repo.Tacs["35693803"] = "Samsung";
            var r = await _service.EvaluateAsync(Enrolment(), Order(), UserRole.Approver, null);
            Assert.That(r.BrandBlocked, Is.True);
            Assert.That(r.TacBrand, Is.EqualTo("Samsung"));
        }

        [Test]
        public async Task AdminOverride_UnblocksAndRecordsOverride()
        {
            _repo.Tacs["35693803"] = "Samsung";
            var r = await _service.EvaluateAsync(Enrolment(), Order(), UserRole.Admin, "infinix");

            Assert.That(r.Errors, Is.Empty);
            Assert.That(r.Brand, Is.EqualTo("Infinix"));
            Assert.That(r.BrandOverridden, Is.True);
            Assert.That(r.BrandBlocked, Is.False);
        }

        [TestCase(UserRole.Approver)]
        [TestCase(UserRole.Dealer)]
        public async Task NonAdminOverride_Refused(UserRole role)
        {
            var r = await _service.EvaluateAsync(Enrolment(), Order(), role, "Samsung");
            Assert.That(r.Errors, Has.Some.Contains("Only an admin"));
            Assert.That(r.BrandOverridden, Is.False);
        }

        [Test]
        public async Task AdminOverride_UnknownBrand_Refused()
        {
            var r = await _service.EvaluateAsync(Enrolment(), Order(), UserRole.Admin, "Nokia");
            Assert.That(r.Errors, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task DepositShort_FlaggedNotBlocked()
        {
            _repo.Payments.Clear();
            _repo.Payments.Add(new DepositPayment("QWE123", "30111222", 1000m));

            var r = await _service.EvaluateAsync(Enrolment(), Order(), UserRole.Dealer, null);

            Assert.That(r.Errors, Is.Empty);
            Assert.That(r.Deposit.IsShort, Is.True);
            Assert.That(r.Deposit.Paid, Is.EqualTo(1000m));
        }

        [Test]
        public async Task DepositUsesSalesPriceWhenOrderHasOne()
        {
            _repo.DailySalePrices[5001] = 20m; // 10000 - 20*365 = 2700 required
            _repo.Payments.Clear();
            _repo.Payments.Add(new DepositPayment("QWE123", "30111222", 2700m));

            var r = await _service.EvaluateAsync(Enrolment(), Order(), UserRole.Dealer, null);

            Assert.That(r.Deposit.Required, Is.EqualTo(2700m));
            Assert.That(r.Deposit.Result, Is.EqualTo(DepositCheck.Ok));
        }

        [Test]
        public async Task DepositNotReceived_Blocks()
        {
            _repo.Payments.Clear();
            var r = await _service.EvaluateAsync(Enrolment(), Order(), UserRole.Dealer, null);
            Assert.That(r.Errors, Has.Some.Contains("hasn't been received"));
        }

        [Test]
        public async Task NoDepositRefOnOrder_Blocks()
        {
            var order = Order();
            order.MpesaDepositRef = null!;
            var r = await _service.EvaluateAsync(Enrolment(), order, UserRole.Dealer, null);
            Assert.That(r.Errors, Has.Some.Contains("no M-Pesa deposit reference"));
        }

        [Test]
        public async Task AccountAlreadyHasAnotherPhone_BlocksAndSuggestsSuffix()
        {
            _repo.LiveDevices[30111222] = "352222222222222";
            var r = await _service.EvaluateAsync(Enrolment(), Order(), UserRole.Dealer, null);
            Assert.That(r.Errors, Has.Some.Contains("301112222"));
        }

        [Test]
        public async Task AccountHasThisSamePhone_NotBlocked()
        {
            _repo.LiveDevices[30111222] = "356938035643809";
            var r = await _service.EvaluateAsync(Enrolment(), Order(), UserRole.Dealer, null);
            Assert.That(r.Errors, Is.Empty);
        }

        [Test]
        public async Task SecondPhoneAccount_WithSuffix_Passes()
        {
            _repo.LiveDevices[30111222] = "352222222222222";
            _repo.Payments.Add(new DepositPayment("QWE999", "301112222", 5000m));
            var e = Enrolment();
            e.AccountId = 301112222;
            var order = Order();
            order.MpesaDepositRef = "QWE999";

            var r = await _service.EvaluateAsync(e, order, UserRole.Dealer, null);

            Assert.That(r.Errors, Is.Empty);
            Assert.That(r.Deposit.Result, Is.EqualTo(DepositCheck.Ok), "deposit must be paid to the suffixed account");
        }

        [Test]
        public async Task SecondPhone_DepositPaidToFirstAccount_Blocks()
        {
            var e = Enrolment();
            e.AccountId = 301112222;
            var r = await _service.EvaluateAsync(e, Order(), UserRole.Dealer, null);
            Assert.That(r.Errors, Has.Some.Contains("different account"));
        }

        // Nuovo phones pay to Nuovo's device number, not the national ID.
        [Test]
        public async Task OnNuovo_DepositOnNuovoAccount_Passes()
        {
            _repo.Payments.Clear();
            _repo.Payments.Add(new DepositPayment("QWE123", "7123456", 5000m));

            var r = await _service.EvaluateAsync(Enrolment(), Order(), UserRole.Dealer, null, onNuovo: true);

            Assert.That(r.Errors, Is.Empty);
            Assert.That(r.Deposit.Result, Is.EqualTo(DepositCheck.Ok));
            Assert.That(r.OrderDealerId, Is.EqualTo(42));
        }

        [Test]
        public async Task OnNuovo_AnyBrand_NotBlocked()
        {
            _repo.Products[5001] = new List<string> { "Redmi A5" };
            var r = await _service.EvaluateAsync(Enrolment(), Order(), UserRole.Dealer, null, onNuovo: true);
            Assert.That(r.BrandBlocked, Is.False);
            Assert.That(r.Errors, Is.Empty);
        }

        [Test]
        public async Task OnNuovo_DepositShort_StillFlagged()
        {
            _repo.Payments.Clear();
            _repo.Payments.Add(new DepositPayment("QWE123", "7123456", 100m));
            var r = await _service.EvaluateAsync(Enrolment(), Order(), UserRole.Dealer, null, onNuovo: true);
            Assert.That(r.Deposit.IsShort, Is.True);
        }

        [Test]
        public async Task OnNuovo_DepositNotReceived_Blocks()
        {
            _repo.Payments.Clear();
            var r = await _service.EvaluateAsync(Enrolment(), Order(), UserRole.Dealer, null, onNuovo: true);
            Assert.That(r.Errors, Has.Some.Contains("hasn't been received"));
        }

        [Test]
        public async Task KnoxOrTranssion_DepositOnNuovoStyleAccount_Blocks()
        {
            _repo.Payments.Clear();
            _repo.Payments.Add(new DepositPayment("QWE123", "7123456", 5000m));
            var r = await _service.EvaluateAsync(Enrolment(), Order(), UserRole.Dealer, null);
            Assert.That(r.Errors, Has.Some.Contains("different account"));
        }

        [Test]
        public async Task DealerRefNotFound_NoDealer()
        {
            var order = Order();
            order.DealerRef = "UNKNOWN";
            var r = await _service.EvaluateAsync(Enrolment(), order, UserRole.Approver, null);
            Assert.That(r.OrderDealerId, Is.Null);
        }
    }

    public class DeviceRemovalServiceTests
    {
        private FakeRemovalTasks _tasks = null!;
        private FakeEnrolmentRepository _enrolments = null!;
        private FakeDevices _devices = null!;
        private FakePayTrigger _payTrigger = null!;
        private FakeKnox _knox = null!;
        private List<long> _nuovoUnregistered = null!;
        private (bool, string) _nuovoResult;
        private DeviceRemovalService _service = null!;

        private static readonly User Admin = new() { UserId = 1, RoleId = UserRole.Admin };
        private static readonly User Approver = new() { UserId = 20, RoleId = UserRole.Approver };

        [SetUp]
        public void SetUp()
        {
            _tasks = new FakeRemovalTasks();
            _enrolments = new FakeEnrolmentRepository();
            _devices = new FakeDevices();
            _payTrigger = new FakePayTrigger();
            _knox = new FakeKnox();
            _nuovoUnregistered = new List<long>();
            _nuovoResult = (true, "200 {\"success\":true}");
            var (nuovo, _) = InterfaceFake<IDeviceProcessor>.Create(new()
            {
                ["UnregisterAsync"] = args => { _nuovoUnregistered.Add((long)args![0]!); return Task.FromResult(_nuovoResult); }
            });
            _service = new DeviceRemovalService(_tasks, _enrolments, _devices, _payTrigger, _knox, nuovo, new FakeTime(),
                NullLogger<DeviceRemovalService>.Instance);
        }

        private async Task<DeviceRemovalTask> QueuedAsync(int lockGroup, long account = 30111222, string? imei = "356938035643809")
        {
            _devices.ById[account] = new Device { Id = (int)account, ImeiNo = imei!, Status = "enrolled", Locked = false };
            await _service.QueueAsync(new[] { new DeviceRemovalTask { AccountId = account, Imei = imei, LockGroup = lockGroup } });
            return _tasks.Items.Last();
        }

        [Test]
        public async Task Queue_SetsProviderFromLockGroup()
        {
            await QueuedAsync(1, 1);
            await QueuedAsync(2, 2);
            await QueuedAsync(3, 3);
            await QueuedAsync(0, 4);

            Assert.That(_tasks.Items.Select(t => t.Provider), Is.EqualTo(new[] { "Nuovo", "Knox", "Transsion", "Nuovo" }));
            Assert.That(_tasks.Items.Select(t => t.Reason).Distinct().Single(), Is.EqualTo("Fully paid"));
        }

        [Test]
        public async Task Queue_SameAccountTwice_QueuedOnce()
        {
            var task = new DeviceRemovalTask { AccountId = 7, Imei = "1", LockGroup = 3 };
            var first = await _service.QueueAsync(new[] { task });
            var second = await _service.QueueAsync(new[] { new DeviceRemovalTask { AccountId = 7, Imei = "1", LockGroup = 3 } });

            Assert.That(first, Is.EqualTo(1));
            Assert.That(second, Is.EqualTo(0));
            Assert.That(_tasks.Items, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task Approve_Transsion_RemovesAndCompletes()
        {
            var task = await QueuedAsync(3);

            var result = await _service.ApproveAsync(task.Id, Admin);

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.True, result.Message);
                Assert.That(_payTrigger.RemoveCalls.Single().Imei, Is.EqualTo("356938035643809"));
                Assert.That(task.Status, Is.EqualTo("Completed"));
                Assert.That(task.DecidedByUserId, Is.EqualTo(Admin.UserId));
                Assert.That(task.CompletedAtUtc, Is.Not.Null);
                var device = _devices.ById[30111222];
                Assert.That(device.EnrollmentStatus, Is.EqualTo("Removed"));
                Assert.That(device.Status, Is.EqualTo("enrolled"), "stays in reports");
            });
        }

        [Test]
        public async Task Approve_Knox_CompletesDeviceWithApproveId()
        {
            _enrolments.Items.Add(new Enrolment { IMEI = "356938035643809", AccountId = 30111222, VeriTechTransId = "vk123" });
            var task = await QueuedAsync(2);

            var result = await _service.ApproveAsync(task.Id, Admin);

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(_knox.CompleteCalls.Single().DeviceUid, Is.EqualTo("356938035643809"));
            Assert.That(_knox.CompleteCalls.Single().ApproveId, Is.EqualTo("vk123"));
            Assert.That(task.Status, Is.EqualTo("Completed"));
        }

        [Test]
        public async Task Approve_KnoxHttpError_Failed()
        {
            _knox.Status = HttpStatusCode.BadRequest;
            _knox.Body = "{\"code\":\"4001\"}";
            var task = await QueuedAsync(2);

            var result = await _service.ApproveAsync(task.Id, Admin);

            Assert.That(result.Success, Is.False);
            Assert.That(task.Status, Is.EqualTo("Failed"));
            Assert.That(task.LastResponse, Does.Contain("400"));
            Assert.That(_devices.Updated, Is.Empty);
        }

        [Test]
        public async Task Approve_Nuovo_UnregistersByDeviceId()
        {
            var task = await QueuedAsync(1);

            var result = await _service.ApproveAsync(task.Id, Admin);

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(_nuovoUnregistered, Is.EqualTo(new[] { 30111222L }));
            Assert.That(task.Status, Is.EqualTo("Completed"));
            Assert.That(_devices.ById[30111222].EnrollmentStatus, Is.EqualTo("Removed"));
        }

        [Test]
        public async Task Approve_Nuovo_NoImeiNeeded()
        {
            var task = await QueuedAsync(1, imei: null);
            var result = await _service.ApproveAsync(task.Id, Admin);
            Assert.That(result.Success, Is.True, result.Message);
        }

        [Test]
        public async Task Approve_NuovoRefuses_Failed()
        {
            _nuovoResult = (false, "422 {\"success\":false}");
            var task = await QueuedAsync(1);

            var result = await _service.ApproveAsync(task.Id, Admin);

            Assert.That(result.Success, Is.False);
            Assert.That(task.Status, Is.EqualTo("Failed"));
            Assert.That(task.LastResponse, Does.Contain("422"));
            Assert.That(_devices.Updated, Is.Empty);
        }

        [Test]
        public async Task Approve_TranssionRefuses_FailedThenRetrySucceeds()
        {
            _payTrigger.RemoveResponse = new PayTriggerApiResponse { Code = 50071, Message = "The device-lock not exist" };
            var task = await QueuedAsync(3);

            var first = await _service.ApproveAsync(task.Id, Admin);
            Assert.That(first.Success, Is.False);
            Assert.That(task.Status, Is.EqualTo("Failed"));
            Assert.That(task.LastResponse, Does.Contain("50071"));

            _payTrigger.RemoveResponse = new PayTriggerApiResponse { Code = 200, Message = "Success" };
            var retry = await _service.ApproveAsync(task.Id, Admin);

            Assert.That(retry.Success, Is.True);
            Assert.That(task.Status, Is.EqualTo("Completed"));
            Assert.That(task.AttemptCount, Is.EqualTo(2));
        }

        [Test]
        public async Task Approve_ProviderThrows_FailedNotStuck()
        {
            _payTrigger.Throw = new HttpRequestException("down");
            var task = await QueuedAsync(3);

            var result = await _service.ApproveAsync(task.Id, Admin);

            Assert.That(result.Success, Is.False);
            Assert.That(task.Status, Is.EqualTo("Failed"), "not left in Processing");
        }

        [Test]
        public async Task Approve_Twice_ProviderCalledOnce()
        {
            var task = await QueuedAsync(3);

            await _service.ApproveAsync(task.Id, Admin);
            var second = await _service.ApproveAsync(task.Id, Admin);

            Assert.That(second.Success, Is.False);
            Assert.That(second.Message, Does.Contain("completed"));
            Assert.That(_payTrigger.RemoveCalls, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task Approve_WhileAnotherAdminIsProcessing_Refused()
        {
            var task = await QueuedAsync(3);
            task.Status = "Processing";
            task.LastAttemptAtUtc = new FakeTime().Now.UtcDateTime.AddMinutes(-1);

            var result = await _service.ApproveAsync(task.Id, Admin);

            Assert.That(result.Success, Is.False);
            Assert.That(_payTrigger.RemoveCalls, Is.Empty);
        }

        [Test]
        public async Task Approve_StaleProcessing_CanBeClaimedAgain()
        {
            var task = await QueuedAsync(3);
            task.Status = "Processing";
            task.LastAttemptAtUtc = new FakeTime().Now.UtcDateTime.AddMinutes(-60);

            var result = await _service.ApproveAsync(task.Id, Admin);

            Assert.That(result.Success, Is.True);
        }

        [TestCase(UserRole.Approver)]
        [TestCase(UserRole.Dealer)]
        [TestCase(UserRole.Agent)]
        public async Task Approve_NonAdmin_RefusedWithoutClaiming(UserRole role)
        {
            var task = await QueuedAsync(3);

            var result = await _service.ApproveAsync(task.Id, new User { UserId = 5, RoleId = role });

            Assert.That(result.Success, Is.False);
            Assert.That(task.Status, Is.EqualTo("Pending"));
            Assert.That(_payTrigger.RemoveCalls, Is.Empty);
        }

        [Test]
        public async Task Approve_NoImei_Failed()
        {
            var task = await QueuedAsync(3, imei: null);
            var result = await _service.ApproveAsync(task.Id, Admin);
            Assert.That(result.Success, Is.False);
            Assert.That(_payTrigger.RemoveCalls, Is.Empty);
        }

        [Test]
        public async Task Approve_Rejected_Refused()
        {
            var task = await QueuedAsync(3);
            await _service.RejectAsync(task.Id, Admin, "customer asked to keep it");

            var result = await _service.ApproveAsync(task.Id, Admin);

            Assert.That(result.Success, Is.False);
            Assert.That(_payTrigger.RemoveCalls, Is.Empty);
        }

        [Test]
        public async Task Reject_RecordsNote_AndAllowsRequeue()
        {
            var task = await QueuedAsync(3);

            var result = await _service.RejectAsync(task.Id, Admin, "  keep for now ");

            Assert.That(result.Success, Is.True);
            Assert.That(task.Status, Is.EqualTo("Rejected"));
            Assert.That(task.DecisionNote, Is.EqualTo("keep for now"));

            var requeued = await _service.QueueAsync(new[] { new DeviceRemovalTask { AccountId = task.AccountId, Imei = task.Imei, LockGroup = 3 } });
            Assert.That(requeued, Is.EqualTo(1));
        }

        [Test]
        public async Task Reject_NonAdmin_Refused()
        {
            var task = await QueuedAsync(3);
            var result = await _service.RejectAsync(task.Id, Approver, null);
            Assert.That(result.Success, Is.False);
            Assert.That(task.Status, Is.EqualTo("Pending"));
        }

        [Test]
        public async Task Reject_Completed_Refused()
        {
            var task = await QueuedAsync(3);
            await _service.ApproveAsync(task.Id, Admin);
            var result = await _service.RejectAsync(task.Id, Admin, null);
            Assert.That(result.Success, Is.False);
            Assert.That(task.Status, Is.EqualTo("Completed"));
        }

        [Test]
        public async Task QueueExistingFullyPaid_QueuesWhatTheScanFinds()
        {
            _tasks.FullyPaidWithoutTask.Add(new DeviceRemovalTask { AccountId = 1, Imei = "1", LockGroup = 2 });
            _tasks.FullyPaidWithoutTask.Add(new DeviceRemovalTask { AccountId = 2, Imei = "2", LockGroup = 3 });

            var queued = await _service.QueueExistingFullyPaidAsync();

            Assert.That(queued, Is.EqualTo(2));
            Assert.That(_tasks.Items.Select(t => t.Provider), Is.EqualTo(new[] { "Knox", "Transsion" }));
        }
    }

    public class FullyPaidJobTests
    {
        private static MobileStatusReport Account(int no, int? lockGroup, decimal arrears = 0, decimal balance = 0, string? nextLock = null) => new()
        {
            AccountNo = no,
            FirstName = $"C{no}",
            ImeiNo = $"35000000000000{no}",
            LockGroup = lockGroup,
            Arrears = arrears,
            LoanBalance = balance,
            NextLockDate = nextLock ?? $"15/10/{DateTime.UtcNow.Year}T10:00:00"
        };

        [Test]
        public void Planner_PicksFullyPaidThisYearOnly()
        {
            var year = DateTime.UtcNow.Year;
            var records = new[]
            {
                Account(1, 1),                                        // fully paid
                Account(2, 1, balance: 500),                          // still owes
                Account(3, 1, arrears: -10),                          // behind
                Account(4, 1, nextLock: "31/12/9999T23:59:59"),       // already handled
                Account(7, 3, nextLock: "31/12/2099T23:59:59"),       // already handled (Transsion fallback)
                Account(5, 1, nextLock: "garbage"),                   // unreadable: skipped, not a crash
                Account(6, 1, nextLock: null!),
            };
            records.Single(r => r.AccountNo == 6).NextLockDate = null;

            var picked = FullyPaidPlanner.Select(records, year);

            Assert.That(picked.Select(p => p.AccountNo), Is.EqualTo(new[] { 1 }));
        }

        [TestCase("15/10/2026T10:00:00")]
        [TestCase("15/10/2026 10:00:00")]
        [TestCase("2026-10-15 10:00:00")]
        [TestCase("15/10/2026")]
        public void Planner_ParsesKnownFormats(string value)
        {
            Assert.That(FullyPaidPlanner.ParseLockDate(value)?.Date, Is.EqualTo(new DateTime(2026, 10, 15)));
        }

        [Test]
        public async Task Process_SetsLockDatePerProvider_AndQueuesEveryAccount_RemovesNothing()
        {
            var records = new List<MobileStatusReport> { Account(1, 1), Account(2, 2), Account(3, 3), Account(4, null), Account(5, 1, balance: 100) };

            var (reports, _) = InterfaceFake<IApplicationReportService>.Create(new()
            {
                ["GetStatusReportByDealer"] = _ => Task.FromResult(new StatusReportViewModel { StatusReports = records })
            });

            List<LockTransaction>? nuovoBatch = null;
            var (processor, processorFake) = InterfaceFake<IDeviceProcessor>.Create(new()
            {
                ["ProcessBatchesAsync"] = args => { nuovoBatch = (List<LockTransaction>)args![0]!; return Task.FromResult(nuovoBatch); }
            });

            List<LockTransaction>? knox = null, transsion = null;
            var (enrolmentService, enrolmentFake) = InterfaceFake<IEnrolmentService>.Create(new()
            {
                ["SetFullyPaidKnox"] = args => { knox = (List<LockTransaction>)args![0]!; return Task.FromResult(knox); },
                ["SetFullyPaidPayTrigger"] = args => { transsion = (List<LockTransaction>)args![0]!; return Task.FromResult(transsion); }
            });

            var (payments, _) = InterfaceFake<Ranalo.DataStore.IPaymentsRepository>.Create(new());

            var tasks = new FakeRemovalTasks();
            var payTrigger = new FakePayTrigger();
            var knoxClient = new FakeKnox();
            var removal = new DeviceRemovalService(tasks, new FakeEnrolmentRepository(), new FakeDevices(), payTrigger, knoxClient,
                processor, new FakeTime(), NullLogger<DeviceRemovalService>.Instance);

            var job = new ScheduledLockFullyPaid(NullLogger<ScheduledLockFullyPaid>.Instance, null!);
            await job.Process(processor, reports, payments, enrolmentService, removal);

            Assert.Multiple(() =>
            {
                Assert.That(nuovoBatch!.Select(d => d.AccountId), Is.EqualTo(new long[] { 1, 4 }));
                Assert.That(knox!.Select(d => d.AccountId), Is.EqualTo(new long[] { 2 }), "Knox no longer sent to Nuovo");
                Assert.That(transsion!.Select(d => d.AccountId), Is.EqualTo(new long[] { 3 }));
                Assert.That(nuovoBatch!.Concat(knox!).Concat(transsion!).Select(d => d.AutoLockDate).Distinct().Single(),
                    Is.EqualTo(DeviceLockRules.FullyPaidLockDateUtc));

                Assert.That(tasks.Items.Select(t => (t.AccountId, t.Provider)), Is.EqualTo(new[]
                {
                    (1L, "Nuovo"), (2L, "Knox"), (3L, "Transsion"), (4L, "Nuovo")
                }));
                Assert.That(tasks.Items.All(t => t.Status == "Pending"), Is.True);

                Assert.That(payTrigger.RemoveCalls, Is.Empty, "the job never removes");
                Assert.That(knoxClient.CompleteCalls, Is.Empty, "the job never removes");
                Assert.That(processorFake.Calls.Select(c => c.Method), Does.Not.Contain("UnregisterAsync"), "the job never removes");
                Assert.That(enrolmentFake.Calls.Select(c => c.Method), Does.Not.Contain("RemoveDevicesPayTrigger"));
            });

            // Next run (e.g. the date update failed and the account shows up
            // again): no duplicate tasks.
            await job.Process(processor, reports, payments, enrolmentService, removal);
            Assert.That(tasks.Items, Has.Count.EqualTo(4));
        }

        [Test]
        public async Task Process_NothingFullyPaid_DoesNothing()
        {
            var (reports, _) = InterfaceFake<IApplicationReportService>.Create(new()
            {
                ["GetStatusReportByDealer"] = _ => Task.FromResult(new StatusReportViewModel { StatusReports = new List<MobileStatusReport>() })
            });
            var (processor, processorFake) = InterfaceFake<IDeviceProcessor>.Create(new());
            var (enrolmentService, enrolmentFake) = InterfaceFake<IEnrolmentService>.Create(new());
            var (payments, _) = InterfaceFake<Ranalo.DataStore.IPaymentsRepository>.Create(new());
            var tasks = new FakeRemovalTasks();
            var removal = new DeviceRemovalService(tasks, new FakeEnrolmentRepository(), new FakeDevices(), new FakePayTrigger(), new FakeKnox(),
                processor, new FakeTime(), NullLogger<DeviceRemovalService>.Instance);

            var job = new ScheduledLockFullyPaid(NullLogger<ScheduledLockFullyPaid>.Instance, null!);
            var result = await job.Process(processor, reports, payments, enrolmentService, removal);

            Assert.That(result, Is.Empty);
            Assert.That(processorFake.Calls, Is.Empty);
            Assert.That(enrolmentFake.Calls, Is.Empty);
            Assert.That(tasks.Items, Is.Empty);
        }
    }

    public class FullyPaidLockDateTests
    {
        private FakeEnrolmentRepository _enrolments = null!;
        private FakeDevices _devices = null!;
        private FakePayTrigger _payTrigger = null!;
        private FakeKnox _knox = null!;
        private EnrolmentService _service = null!;

        [SetUp]
        public void SetUp()
        {
            _enrolments = new FakeEnrolmentRepository();
            _devices = new FakeDevices();
            _payTrigger = new FakePayTrigger();
            _knox = new FakeKnox();
            var (veritech, _) = InterfaceFake<Ranalo.VeriTechClient.IVeritechApiClient>.Create(new());
            var (sync, _) = InterfaceFake<Ranalo.Woocommece.Api.Services.ISyncService>.Create(new());
            var (dealers, _) = InterfaceFake<Ranalo.DataStore.IRepository>.Create(new());
            var (workflow, _) = InterfaceFake<ITranssionEnrolmentWorkflow>.Create(new());
            _service = new EnrolmentService(_enrolments, veritech, _knox, _payTrigger, _devices, sync, dealers, workflow);

            _devices.ById[3] = new Device { Id = 3, ImeiNo = "356938035643809", Status = "enrolled", Locked = true, NextLockDate = "10/10/2026" };
            _devices.ById[2] = new Device { Id = 2, ImeiNo = "352222222222222", Status = "enrolled", Locked = true, NextLockDate = "10/10/2026" };
            _enrolments.Items.Add(new Enrolment { AccountId = 2, IMEI = "352222222222222", VeriTechTransId = "vk1" });
        }

        [Test]
        public async Task Transsion_Pushes9999_AndRecordsIt()
        {
            var result = await _service.SetFullyPaidPayTrigger(new List<LockTransaction> { new() { AccountId = 3 } });

            Assert.That(_payTrigger.RepayCalls.Single().Imei, Is.EqualTo("356938035643809"));
            Assert.That(_payTrigger.RepayCalls.Single().NextRepayTime,
                Is.EqualTo(new DateTimeOffset(DeviceLockRules.FullyPaidLockDateUtc).ToUnixTimeSeconds()));
            Assert.That(_devices.ById[3].NextLockDate, Is.EqualTo("31/12/9999"));
            Assert.That(_devices.ById[3].NextLockDateIsoFormat, Is.EqualTo("31/12/9999T23:59:59"));
            Assert.That(_devices.ById[3].Locked, Is.False);
            Assert.That(result.Single().Result, Is.EqualTo("9999: 200 Success"));
        }

        [Test]
        public async Task Transsion_Refuses9999_FallsBackTo2099()
        {
            _payTrigger.RepayResponses.Enqueue(new PayTriggerApiResponse { Code = 40002, Message = "nextRepayTime out of range" });

            var result = await _service.SetFullyPaidPayTrigger(new List<LockTransaction> { new() { AccountId = 3 } });

            Assert.Multiple(() =>
            {
                Assert.That(_payTrigger.RepayCalls.Select(c => DateTimeOffset.FromUnixTimeSeconds(c.NextRepayTime).Year),
                    Is.EqualTo(new[] { 9999, 2099 }));
                Assert.That(_devices.ById[3].NextLockDate, Is.EqualTo("31/12/2099"));
                Assert.That(_devices.ById[3].NextLockDateIsoFormat, Is.EqualTo("31/12/2099T23:59:59"));
                Assert.That(_devices.ById[3].Locked, Is.False);
                Assert.That(result.Single().AutoLockDate, Is.EqualTo(DeviceLockRules.FullyPaidFallbackLockDateUtc));
                Assert.That(result.Single().Result, Does.Contain("9999: 40002").And.Contain("2099: 200"));
            });
        }

        [Test]
        public async Task Transsion_Accepts9999_DoesNotTry2099()
        {
            await _service.SetFullyPaidPayTrigger(new List<LockTransaction> { new() { AccountId = 3 } });
            Assert.That(_payTrigger.RepayCalls, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task Transsion_RefusesBoth_LeavesOurRecordSoItRetries()
        {
            _payTrigger.RepayResponse = new PayTriggerApiResponse { Code = 40002, Message = "param error" };

            var result = await _service.SetFullyPaidPayTrigger(new List<LockTransaction> { new() { AccountId = 3 } });

            Assert.That(_payTrigger.RepayCalls, Has.Count.EqualTo(2));
            Assert.That(_devices.ById[3].NextLockDate, Is.EqualTo("10/10/2026"));
            Assert.That(_devices.Updated, Is.Empty);
            Assert.That(result.Single().Result, Does.Contain("param error"));
        }

        [Test]
        public async Task Transsion_OneFailureDoesNotStopTheRest()
        {
            _payTrigger.Throw = new HttpRequestException("down");
            var result = await _service.SetFullyPaidPayTrigger(new List<LockTransaction> { new() { AccountId = 3 }, new() { AccountId = 99 } });
            Assert.That(result, Has.Count.EqualTo(2));
            Assert.That(result[0].Result, Is.EqualTo("down"));
        }

        [Test]
        public async Task Knox_UnlocksWithoutRelock_AndRecords9999()
        {
            await _service.SetFullyPaidKnox(new List<LockTransaction> { new() { AccountId = 2 } });

            var call = _knox.ActionCalls.Single();
            Assert.That(call.DeviceUid, Is.EqualTo("352222222222222"));
            Assert.That(call.ApproveId, Is.EqualTo("vk1"));
            Assert.That(call.Actions.Select(a => a.Action), Is.EqualTo(new[] { "unLock" }), "no lock action is scheduled");
            Assert.That(_devices.ById[2].NextLockDate, Is.EqualTo("31/12/9999"));
            Assert.That(_devices.ById[2].Locked, Is.False);
        }

        [Test]
        public async Task Knox_Refused_LeavesOurRecord()
        {
            _knox.Status = HttpStatusCode.InternalServerError;
            await _service.SetFullyPaidKnox(new List<LockTransaction> { new() { AccountId = 2 } });
            Assert.That(_devices.ById[2].NextLockDate, Is.EqualTo("10/10/2026"));
        }
    }
}

namespace Ranolo.Web.Tests
{
    public class NuovoUnregisterResultTests
    {
        [TestCase(200, "{\"success\":true,\"message\":\"Devices unregistered\"}", true)]
        [TestCase(200, "{\"message\":\"ok\"}", true)]
        [TestCase(200, "{\"errors\":[]}", true)]
        [TestCase(200, "not json", true)]
        [TestCase(200, "{\"success\":false,\"message\":\"Device not found\"}", false)]
        [TestCase(200, "{\"errors\":[\"Device 123 not found\"]}", false)]
        [TestCase(200, "{\"errors\":{\"device_ids\":[\"invalid\"]}}", false)]
        [TestCase(401, "{\"error\":\"Unauthorized\"}", false)]
        [TestCase(500, "", false)]
        public void ReadUnregisterResult(int status, string body, bool expected)
        {
            var (success, response) = DeviceProcessor.ReadUnregisterResult(status, body);
            Assert.That(success, Is.EqualTo(expected));
            Assert.That(response, Does.StartWith(status.ToString()));
        }
    }
}
