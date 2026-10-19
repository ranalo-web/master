using Microsoft.Extensions.Logging.Abstractions;
using Ranalo.DataStore.DataModels;
using Ranalo.Models;
using Ranalo.PayTrigger.Models;
using Ranalo.Services.DeviceLock;
using Ranalo.SumsungKnox.Models;
using Ranalo.VeriTechClient;
using Ranalo.VeriTechClient.Models;

namespace Ranolo.Web.Tests
{
    // Stock phones: locked by IMEI with no order, sold later (2026-10-19).
    public class StockPhoneRulesTests
    {
        private static readonly DateTime Now = new(2026, 10, 19, 9, 0, 0, DateTimeKind.Utc);

        [Test]
        public void ParseImeis_OnePerLineOrCommas_DuplicatesDropped()
        {
            var (imeis, errors) = StockPhoneRules.ParseImeis("350522366526910\r\n356938035643809, 350522366526910\n");
            Assert.Multiple(() =>
            {
                Assert.That(errors, Is.Empty);
                Assert.That(imeis, Is.EqualTo(new[] { "350522366526910", "356938035643809" }));
            });
        }

        [Test]
        public void ParseImeis_AnyBadImei_NothingSent()
        {
            var (imeis, errors) = StockPhoneRules.ParseImeis("350522366526910\n350522366526911");
            Assert.Multiple(() =>
            {
                Assert.That(imeis, Is.Empty, "a typo shouldn't send the rest on their own");
                Assert.That(errors.Single(), Does.Contain("350522366526911"));
            });
        }

        [Test]
        public void ParseImeis_EmptyOrTooMany_Refused()
        {
            Assert.That(StockPhoneRules.ParseImeis(" ").Errors.Single(), Does.Contain("at least one"));
            var many = string.Join("\n", Enumerable.Range(0, StockPhoneRules.MaxPerBatch + 1).Select(i => $"35052236652{i:0000}"));
            Assert.That(StockPhoneRules.ParseImeis(many).Errors, Has.Some.Contains("at most"));
        }

        [TestCase(null, null, true)]
        [TestCase("Nokia", null, true)]          // Nuovo phones are enrolled in Nuovo
        [TestCase("Samsung", null, false)]
        [TestCase("TECNO", "TECNO", false)]
        [TestCase("itel", "Samsung", true)]      // the TAC says otherwise
        public void CheckBrand(string? brand, string? tacBrand, bool refused)
        {
            Assert.That(StockPhoneRules.CheckBrand(DeviceLockRules.NormaliseBrand(brand) ?? brand, tacBrand) != null, Is.EqualTo(refused));
        }

        [Test]
        public void CheckFree_AlreadyStockOrEnrolledOrLive_Refused()
        {
            Assert.Multiple(() =>
            {
                Assert.That(StockPhoneRules.CheckFree(new Enrolment { IMEI = "x", Status = EnrolmentStatus.Stock }, null), Does.Contain("already in stock"));
                Assert.That(StockPhoneRules.CheckFree(new Enrolment { IMEI = "x", AccountId = 12345678, OrderId = 9 }, null), Does.Contain("account 12345678"));
                Assert.That(StockPhoneRules.CheckFree(null, 7353632), Does.Contain("7353632"));
                Assert.That(StockPhoneRules.CheckFree(null, null), Is.Null);
            });
        }

        private static long Ms(DateTime t) => new DateTimeOffset(t).ToUnixTimeMilliseconds();

        [Test]
        public void KnoxCheck_NotInKnoxOrActivating_NotLocked()
        {
            Assert.Multiple(() =>
            {
                Assert.That(StockPhoneRules.KnoxCheck(null, Now).Locked, Is.False);
                Assert.That(StockPhoneRules.KnoxCheck(new DeviceDto { Status = "Activating" }, Now).Text, Does.Contain("switch the phone on"));
            });
        }

        [Test]
        public void KnoxCheck_LockedOrLockApplied_Locked()
        {
            Assert.Multiple(() =>
            {
                Assert.That(StockPhoneRules.KnoxCheck(new DeviceDto { Status = "Locked", LastSeen = Ms(Now.AddMinutes(-5)) }, Now).Locked, Is.True);
                Assert.That(StockPhoneRules.KnoxCheck(new DeviceDto { Status = "Enrolled", RelockTimestamp = Ms(Now.AddHours(-1)), LatestRelockApplied = true }, Now).Locked, Is.True);
            });
        }

        [Test]
        public void KnoxCheck_LockNotReachedOrInFuture_NotLocked()
        {
            var pending = StockPhoneRules.KnoxCheck(new DeviceDto { Status = "Enrolled", RelockTimestamp = Ms(Now.AddHours(-1)), LatestRelockApplied = false }, Now);
            var future = StockPhoneRules.KnoxCheck(new DeviceDto { Status = "Enrolled", RelockTimestamp = Ms(Now.AddDays(1)) }, Now);
            Assert.Multiple(() =>
            {
                Assert.That(pending.Locked, Is.False);
                Assert.That(pending.Text, Does.Contain("hasn't reached the phone"));
                Assert.That(future.Locked, Is.False);
                Assert.That(future.Text, Does.Contain("NOT locked"));
            });
        }

        private static FindLockStateResponse State(int lockState, int mobileStatus) =>
            new() { Code = 200, Message = "Success", Data = new FindLockStateData { LockState = lockState, MobileStatus = mobileStatus } };

        [Test]
        public void TranssionCheck()
        {
            Assert.Multiple(() =>
            {
                Assert.That(StockPhoneRules.TranssionCheck(new FindLockStateResponse { Code = 50071, Message = "The device-lock not exist" }).Locked, Is.False);
                Assert.That(StockPhoneRules.TranssionCheck(State(2000, 1000)).Text, Does.Contain("switch the phone on"));
                Assert.That(StockPhoneRules.TranssionCheck(State(3000, 1000)).Locked, Is.True);
                Assert.That(StockPhoneRules.TranssionCheck(State(3000, 2000)).Text, Does.Contain("NOT locked"));
            });
        }

        [Test]
        public void UnsoldStock_CantBeApproved()
        {
            var decision = DeviceLockRules.CanApproveEnrolment(UserRole.Admin, 1, new Enrolment { IMEI = "x", Status = EnrolmentStatus.Stock });
            Assert.That(decision.Allowed, Is.False);
            Assert.That(decision.Reason, Does.Contain("stock phone"));
        }

        [Test]
        public void SaleBrand_OnlyADifferentBrandOnTheOrderStopsIt()
        {
            var stock = new Enrolment { IMEI = "356938035643809", DeviceBrand = "TECNO" };
            Assert.Multiple(() =>
            {
                Assert.That(StockPhoneRules.CheckSaleBrand(stock, "TECNO"), Is.Null);
                Assert.That(StockPhoneRules.CheckSaleBrand(stock, null), Is.Null, "the admin already confirmed the brand");
                Assert.That(StockPhoneRules.CheckSaleBrand(stock, "Samsung"), Does.Contain("Samsung"));
            });
        }

        private static Enrolment Sale() => new()
        {
            IMEI = "356938035643809", AccountId = 36637211, OrderId = 19832, FirstName = "Jane", LastName = "W", DealerId = 4,
            EnrolledByUserId = 9, EnrolledByRole = (int)UserRole.Dealer, ProductName = "TECNO Spark 20", DealerRef = "D4",
            RequiredDeposit = 3000, DepositPaid = 2500, DepositShort = true, UpdatedBy = "Dealer",
        };

        [Test]
        public void AttachSale_TranssionSwitchedOn_WaitsForTheCall()
        {
            var stock = new Enrolment { IMEI = "356938035643809", DeviceBrand = "TECNO", Status = EnrolmentStatus.Stock, StockLockedAt = Now, ActivatedAt = Now };
            StockPhoneRules.AttachSale(stock, Sale(), Now);
            Assert.Multiple(() =>
            {
                Assert.That(stock.Status, Is.EqualTo(EnrolmentStatus.Enrolled));
                Assert.That(stock.AccountId, Is.EqualTo(36637211));
                Assert.That(stock.OrderId, Is.EqualTo(19832));
                Assert.That(stock.DealerId, Is.EqualTo(4));
                Assert.That(stock.EnrolledByUserId, Is.EqualTo(9));
                Assert.That(stock.DepositShort, Is.True);
                Assert.That(stock.DeviceBrand, Is.EqualTo("TECNO"));
                Assert.That(StockPhoneRules.FromStock(stock), Is.True);
            });
        }

        [Test]
        public void AttachSale_Knox_WaitsForApproval()
        {
            var stock = new Enrolment { IMEI = "350522366526910", DeviceBrand = "Samsung", Status = EnrolmentStatus.Stock, StockLockedAt = Now };
            StockPhoneRules.AttachSale(stock, Sale(), Now);
            Assert.That(stock.Status, Is.EqualTo(EnrolmentStatus.Pending));
        }
    }

    public class StockPhoneServiceTests
    {
        private FakeEnrolmentRepository _enrolments = null!;
        private FakeDevices _devices = null!;
        private FakePayTrigger _payTrigger = null!;
        private FakeKnox _knox = null!;
        private FakeDeviceLockRepository _lockRepo = null!;
        private FakeTime _time = null!;
        private StockPhoneService _service = null!;
        private static readonly User Admin = new() { UserId = 1, RoleId = UserRole.Admin, Name = "Admin" };

        [SetUp]
        public void SetUp()
        {
            _enrolments = new FakeEnrolmentRepository();
            _devices = new FakeDevices();
            _payTrigger = new FakePayTrigger();
            _knox = new FakeKnox();
            _lockRepo = new FakeDeviceLockRepository();
            _time = new FakeTime();
            var (veritech, _) = InterfaceFake<IVeritechApiClient>.Create(new()
            {
                ["UploadDevicesAsync"] = _ => Task.FromResult(new CreateDeviceResponse
                {
                    Data = new UploadDeviceResultModel { Transaction_Id = "vkdp9", Status = "Progress" },
                }),
            });
            _service = new StockPhoneService(_enrolments, _devices, _lockRepo, veritech, _knox, _payTrigger, _time,
                NullLogger<StockPhoneService>.Instance);
        }

        [Test]
        public async Task LockTranssion_SendsTheImeiOnly_LockedOnActivation_NoAccount()
        {
            var (messages, errors) = await _service.LockAsync("356938035643809", "tecno", 4, Admin);

            var call = _payTrigger.PreEnrolCalls.Single();
            var e = _enrolments.Items.Single();
            Assert.Multiple(() =>
            {
                Assert.That(errors, Is.Empty);
                Assert.That(messages.Single(), Does.Contain("Transsion"));
                Assert.That(call.PreLock, Is.True);
                Assert.That(call.Items.Single().Imei, Is.EqualTo("356938035643809"));
                Assert.That(call.Items.Single().OrderNum, Is.Null, "no order yet");
                Assert.That(e.Status, Is.EqualTo(EnrolmentStatus.Stock));
                Assert.That(e.AccountId, Is.EqualTo(0));
                Assert.That(e.DeviceBrand, Is.EqualTo("TECNO"));
                Assert.That(e.DealerId, Is.EqualTo(4));
                Assert.That(e.StockLockedByUserId, Is.EqualTo(1));
                Assert.That(_devices.Saved, Is.Empty, "no device row until it's sold: the lock jobs mustn't see it");
            });
        }

        [Test]
        public async Task LockTranssion_Refused_NothingSaved()
        {
            _payTrigger.PreEnrolResponse = new PreEnrollImeiResponse { Code = 40001, Message = "IMEI not found" };

            var (_, errors) = await _service.LockAsync("356938035643809", "TECNO", 0, Admin);

            Assert.That(errors.Single(), Does.Contain("IMEI not found"));
            Assert.That(_enrolments.Items, Is.Empty);
        }

        [Test]
        public async Task Lock_NotAdmin_Refused()
        {
            var (_, errors) = await _service.LockAsync("356938035643809", "TECNO", 0, new User { UserId = 2, RoleId = UserRole.Approver });
            Assert.That(errors.Single(), Does.Contain("Only an admin"));
            Assert.That(_payTrigger.PreEnrolCalls, Is.Empty);
        }

        [Test]
        public async Task Lock_PhoneStillOnAnAccount_Refused()
        {
            _devices.ById[7353632] = new Ranalo.Woocommece.Api.Models.Device { Id = 7353632, ImeiNo = "356938035643809", Status = "enrolled" };

            var (_, errors) = await _service.LockAsync("356938035643809", "TECNO", 0, Admin);

            Assert.That(errors.Single(), Does.Contain("7353632"));
            Assert.That(_payTrigger.PreEnrolCalls, Is.Empty);
        }

        [Test]
        public async Task LockKnox_UploadsToVeritech_ThenApproveAndLock()
        {
            await _service.LockAsync("350522366526910", "Samsung", 0, Admin);
            var e = _enrolments.Items.Single();
            Assert.That(e.VeriTechTransId, Is.EqualTo("vkdp9"));
            Assert.That(_knox.ApproveCalls, Is.Empty, "approve & lock is a separate step, once it reaches Knox");

            var result = await _service.KnoxApproveAndLockAsync(e.Id, Admin);

            var lockCall = _knox.ActionCalls.Single();
            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.True);
                Assert.That(_knox.ApproveCalls.Single().ApproveId, Is.EqualTo("vkdp9"));
                Assert.That(lockCall.Actions.Select(a => a.Action), Is.EqualTo(new[] { "unLock", "lock" }));
                Assert.That(lockCall.Actions[1].Timestamp, Is.EqualTo(_time.Now.ToUnixTimeMilliseconds()), "locked now");
                Assert.That(e.StockKnoxApprovedAt, Is.Not.Null);
                Assert.That(e.Status, Is.EqualTo(EnrolmentStatus.Stock));
            });

            await _service.KnoxApproveAndLockAsync(e.Id, Admin);
            Assert.That(_knox.ApproveCalls, Has.Count.EqualTo(1), "pressing again only resends the lock");
            Assert.That(_knox.ActionCalls, Has.Count.EqualTo(2));
        }

        [Test]
        public async Task KnoxApprove_NotInKnoxYet_TryAgainLater()
        {
            await _service.LockAsync("350522366526910", "Samsung", 0, Admin);
            var e = _enrolments.Items.Single();
            _knox.Status = System.Net.HttpStatusCode.NotFound;
            _knox.Body = "DEVICE_NOT_FOUND";

            var result = await _service.KnoxApproveAndLockAsync(e.Id, Admin);

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.False);
                Assert.That(result.Message, Does.Contain("try again"));
                Assert.That(e.StockKnoxApprovedAt, Is.Null);
                Assert.That(_knox.ActionCalls, Is.Empty);
            });
        }

        [Test]
        public async Task CheckLock_Transsion_LockedTicks_UnlockedTakesTheTickAway()
        {
            await _service.LockAsync("356938035643809", "TECNO", 0, Admin);
            var e = _enrolments.Items.Single();

            _payTrigger.FindResponse = new FindLockStateResponse { Code = 200, Message = "Success", Data = new FindLockStateData { LockState = 3000, MobileStatus = 1000 } };
            var locked = await _service.CheckLockAsync(e.Id, Admin);
            Assert.That(locked.Success, Is.True);
            Assert.That(e.StockVerifiedAt, Is.EqualTo(_time.Now.UtcDateTime));
            Assert.That(StockPhoneRules.NeedsVerifying(e), Is.False);

            _payTrigger.FindResponse = new FindLockStateResponse { Code = 200, Message = "Success", Data = new FindLockStateData { LockState = 3000, MobileStatus = 2000 } };
            var unlocked = await _service.CheckLockAsync(e.Id, Admin);
            Assert.That(unlocked.Success, Is.False);
            Assert.That(e.StockVerifiedAt, Is.Null);
            Assert.That(e.StockCheck, Does.Contain("NOT locked"));
        }

        [Test]
        public async Task CheckLock_Knox_ReadsKnox()
        {
            await _service.LockAsync("350522366526910", "Samsung", 0, Admin);
            var e = _enrolments.Items.Single();
            _knox.ListResponse = new ListDevicesResponse
            {
                DeviceList = new List<DeviceDto> { new() { Imei = "350522366526910", Status = "Locked" } },
            };

            var result = await _service.CheckLockAsync(e.Id, Admin);

            Assert.That(result.Success, Is.True);
            Assert.That(e.StockVerifiedAt, Is.Not.Null);
        }

        [Test]
        public async Task SoldKnoxStock_ApprovalUnlocksForADay()
        {
            await _service.LockAsync("350522366526910", "Samsung", 0, Admin);
            var e = _enrolments.Items.Single();
            await _service.KnoxApproveAndLockAsync(e.Id, Admin);
            StockPhoneRules.AttachSale(e, new Enrolment { IMEI = e.IMEI, AccountId = 36637211, OrderId = 19832, EnrolledByUserId = 9 }, _time.Now.UtcDateTime);

            var result = await _service.UnlockSoldKnoxAsync(e, new User { UserId = 3, RoleId = UserRole.Approver });

            var unlock = _knox.ActionCalls.Last();
            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.True);
                Assert.That(e.Status, Is.EqualTo(EnrolmentStatus.Approved));
                Assert.That(e.CallApprovedByUserId, Is.EqualTo(3));
                Assert.That(unlock.Actions[1].Timestamp, Is.EqualTo(_time.Now.AddDays(1).ToUnixTimeMilliseconds()));
            });
        }

        [Test]
        public async Task UnsoldKnoxStock_CantBeUnlocked()
        {
            await _service.LockAsync("350522366526910", "Samsung", 0, Admin);
            var e = _enrolments.Items.Single();

            var result = await _service.UnlockSoldKnoxAsync(e, Admin);

            Assert.That(result.Success, Is.False);
            Assert.That(_knox.ActionCalls, Is.Empty);
        }
    }

    public class StockPhoneTranssionTests
    {
        private FakeEnrolmentRepository _enrolments = null!;
        private FakeDevices _devices = null!;
        private FakePayTrigger _payTrigger = null!;
        private FakeTime _time = null!;
        private TranssionEnrolmentWorkflow _workflow = null!;

        [SetUp]
        public void SetUp()
        {
            _enrolments = new FakeEnrolmentRepository();
            _devices = new FakeDevices();
            _payTrigger = new FakePayTrigger();
            _time = new FakeTime();
            _workflow = new TranssionEnrolmentWorkflow(_enrolments, _devices, _payTrigger, new FakeDeviceLockRepository(), _time,
                NullLogger<TranssionEnrolmentWorkflow>.Instance);
        }

        private Enrolment Stock()
        {
            var e = new Enrolment
            {
                Id = Guid.NewGuid(), IMEI = "356938035643809", DeviceBrand = "TECNO", Status = EnrolmentStatus.Stock,
                StockLockedAt = _time.Now.UtcDateTime, PayTriggerResponse = "Stock: pre-enrolled, locks when switched on.",
            };
            _enrolments.Items.Add(e);
            return e;
        }

        [Test]
        public async Task SwitchedOnInStock_RecordedButStaysLocked()
        {
            var e = Stock();

            await _workflow.OnActivatedAsync(e.IMEI);

            Assert.Multiple(() =>
            {
                Assert.That(e.ActivatedAt, Is.Not.Null);
                Assert.That(e.Status, Is.EqualTo(EnrolmentStatus.Stock));
                Assert.That(_payTrigger.RepayCalls, Is.Empty, "never unlocked before it's sold");
            });
        }

        [Test]
        public async Task Sold_DeviceRowOnTheCustomersAccount_CallUnlocksWithoutOrderNumber()
        {
            var e = Stock();
            await _workflow.OnActivatedAsync(e.IMEI);
            StockPhoneRules.AttachSale(e, new Enrolment { IMEI = e.IMEI, AccountId = 36637211, OrderId = 19832, FirstName = "Jane", EnrolledByUserId = 9 }, _time.Now.UtcDateTime);

            var error = await _workflow.AttachStockSaleAsync(e, 4);
            Assert.That(error, Is.Null);
            Assert.That(_devices.ById[36637211].ImeiNo, Is.EqualTo(e.IMEI));
            Assert.That(_devices.ById[36637211].LockGroup, Is.EqualTo(3));
            Assert.That(e.Status, Is.EqualTo(EnrolmentStatus.Enrolled));

            var result = await _workflow.ApproveCallAsync(e.Id, new User { UserId = 3, RoleId = UserRole.Approver, Name = "Approver" });

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.True, result.Message);
                Assert.That(_payTrigger.RepayCalls.Single().Imei, Is.EqualTo(e.IMEI));
                Assert.That(_payTrigger.RepayCalls.Single().OrderNum, Is.Null, "Transsion has it under the IMEI only");
                Assert.That(e.UnlockedAt, Is.Not.Null);
            });
        }

        [Test]
        public async Task Sold_AccountAlreadyHasAPhone_NothingSaved()
        {
            var e = Stock();
            _devices.ById[36637211] = new Ranalo.Woocommece.Api.Models.Device { Id = 36637211, ImeiNo = "350522366526910", Status = "enrolled" };
            StockPhoneRules.AttachSale(e, new Enrolment { IMEI = e.IMEI, AccountId = 36637211, OrderId = 19832 }, _time.Now.UtcDateTime);
            var updates = _enrolments.UpdateCount;

            var error = await _workflow.AttachStockSaleAsync(e, 4);

            Assert.That(error, Does.Contain("already has device"));
            Assert.That(_enrolments.UpdateCount, Is.EqualTo(updates));
        }
    }
}
