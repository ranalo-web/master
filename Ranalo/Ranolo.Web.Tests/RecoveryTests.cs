using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Ranalo.DataStore;
using Ranalo.Models;
using Ranalo.PayTrigger.Models;
using Ranalo.Services;
using Ranalo.SumsungKnox;
using Ranalo.SumsungKnox.Models;
using Ranalo.VeriTechClient;
using Ranalo.VeriTechClient.Models;

namespace Ranolo.Web.Tests
{
    // Recovery: getting a phone back under a lock (agreed 2026-10-09).
    public class RecoveryRulesTests
    {
        private static LockRecovery Phone(string status = RecoveryStatus.Investigating, string make = "samsung", string? trans = null) => new()
        {
            Id = 1, AccountNo = 8511324, Imei = "358253401180537", Make = make, Model = "SM-A065F",
            Status = status, VeritechTransId = trans, CurrentLockGroup = 1,
        };

        [Test]
        public void OtherBrandsAreTrackedOnly()
        {
            var actions = RecoveryRules.AllowedActions(Phone(make: "Xiaomi"));
            Assert.That(actions, Is.EquivalentTo(new[] { RecoveryAction.Note, RecoveryAction.NotCatchable, RecoveryAction.Close }));
        }

        [TestCase("TECNO")]
        [TestCase("itel")]
        [TestCase("Infinix")]
        [TestCase("INFINIX")]   // as Nuovo has them
        [TestCase("ITEL")]
        [TestCase(" Tecno ")]
        public void TranssionStepsOpenUpInOrder(string make)
        {
            var fresh = RecoveryRules.AllowedActions(Phone(make: make));
            Assert.That(fresh, Is.SupersetOf(new[] { RecoveryAction.TranssionSend, RecoveryAction.TranssionCheck }));
            Assert.That(fresh, Does.Not.Contain(RecoveryAction.Lock));
            Assert.That(fresh, Does.Not.Contain(RecoveryAction.Upload), "no Knox steps for Transsion phones");

            var sent = RecoveryRules.AllowedActions(Phone(RecoveryStatus.UploadSent, make));
            Assert.That(sent, Does.Not.Contain(RecoveryAction.Lock), "Transsion refuses a lock date before activation (code 20001)");
            Assert.That(sent, Does.Not.Contain(RecoveryAction.MoveToTranssion), "not until it's active");

            var active = RecoveryRules.AllowedActions(Phone(RecoveryStatus.Approved, make));
            Assert.That(active, Is.SupersetOf(new[] { RecoveryAction.Lock, RecoveryAction.Unlock, RecoveryAction.MoveToTranssion }));
            Assert.That(active, Does.Not.Contain(RecoveryAction.MoveToKnox));
        }

        [Test]
        public void SamsungWithNoMakeIsKnownByItsModel()
        {
            // 41109528: Knox device with no make, model SM-A165F/DS.
            Assert.That(RecoveryRules.IsSamsung(null, "SM-A165F/DS"), Is.True);
            Assert.That(RecoveryRules.IsSamsung("", " sm-a055f"), Is.True);
            Assert.That(RecoveryRules.IsSamsung("TECNO", "SM-A165F"), Is.False, "a make wins over the model");
            Assert.That(RecoveryRules.IsSamsung(null, "KM5"), Is.False);
        }

        [Test]
        public void PhoneAlreadyOnKnoxJustLocksAndUnlocks()
        {
            var r = Phone(trans: "vkdp465096utid");
            r.Make = null;
            r.Model = "SM-A165F/DS";
            r.CurrentLockGroup = 2;

            var actions = RecoveryRules.AllowedActions(r);
            Assert.That(actions, Is.SupersetOf(new[] { RecoveryAction.KnoxCheck, RecoveryAction.Lock, RecoveryAction.Unlock }));
            Assert.That(actions, Does.Contain(RecoveryAction.Upload), "Knox may have lost it (41109528): send it again");
            Assert.That(actions, Does.Not.Contain(RecoveryAction.MoveToKnox), "it's already the Knox phone");

            r.Status = RecoveryStatus.Approved;   // approved again after a re-upload
            Assert.That(RecoveryRules.AllowedActions(r), Does.Not.Contain(RecoveryAction.MoveToKnox));

            r.Status = RecoveryStatus.Investigating;
            r.VeritechTransId = null;
            Assert.That(RecoveryRules.AllowedActions(r), Does.Not.Contain(RecoveryAction.Lock), "no approve id to give Knox");
        }

        [Test]
        public void SamsungDoesntGetTranssionSteps()
        {
            var actions = RecoveryRules.AllowedActions(Phone(RecoveryStatus.Approved, trans: "tx1"));
            Assert.That(actions, Does.Not.Contain(RecoveryAction.TranssionSend));
            Assert.That(actions, Does.Not.Contain(RecoveryAction.MoveToTranssion));
        }

        [Test]
        public void MovedToTranssionOnlyAllowsChecksAndNotes()
        {
            var r = Phone(RecoveryStatus.MovedToTranssion, "TECNO");
            Assert.That(RecoveryRules.AllowedActions(r), Is.EquivalentTo(new[] { RecoveryAction.Note, RecoveryAction.TranssionCheck }));
            Assert.That(RecoveryRules.IsOpen(r.Status), Is.False);
        }

        [TestCase(RecoveryStatus.UploadSent, 3000, RecoveryStatus.Approved)]
        [TestCase(RecoveryStatus.Investigating, 3000, RecoveryStatus.Approved)]
        [TestCase(RecoveryStatus.UploadSent, 1000, RecoveryStatus.UploadSent)]
        [TestCase(RecoveryStatus.LockSent, 3000, RecoveryStatus.LockSent)]
        [TestCase(RecoveryStatus.Approved, 5000, RecoveryStatus.Approved)]
        public void TranssionCheckMakesASentPhoneActiveOnly(string current, int lockState, string expected)
        {
            Assert.That(RecoveryRules.StatusAfterTranssionCheck(current, lockState), Is.EqualTo(expected));
        }

        [Test]
        public void LastReplySummaryIsTheFirstLineShortened()
        {
            var r = new LockRecovery { LastReply = "HTTP 403 " + new string('x', 200) + "\nsecond line" };
            Assert.That(r.LastReplySummary, Does.StartWith("HTTP 403"));
            Assert.That(r.LastReplySummary!.Length, Is.EqualTo(91));
            Assert.That(r.LastReplySummary, Does.Not.Contain("second line"));
            Assert.That(new LockRecovery().LastReplySummary, Is.Null);
        }

        [Test]
        public void StatusLabelsFollowTheLocker()
        {
            Assert.That(RecoveryStatus.Label(RecoveryStatus.UploadSent, "TECNO"), Is.EqualTo("Sent to Transsion"));
            Assert.That(RecoveryStatus.Label(RecoveryStatus.UploadSent, "samsung"), Is.EqualTo("Sent to Knox"));
            Assert.That(RecoveryStatus.Label(RecoveryStatus.Approved, "itel"), Is.EqualTo("Active in Transsion"));
            Assert.That(RecoveryStatus.Label(RecoveryStatus.MovedToTranssion), Is.EqualTo("Moved to Transsion"));
        }

        [Test]
        public void KnoxStepsOpenUpInOrder()
        {
            var fresh = RecoveryRules.AllowedActions(Phone());
            Assert.That(fresh, Does.Contain(RecoveryAction.Upload));
            Assert.That(fresh, Does.Not.Contain(RecoveryAction.Approve));
            Assert.That(fresh, Does.Not.Contain(RecoveryAction.Lock));

            var uploaded = RecoveryRules.AllowedActions(Phone(RecoveryStatus.UploadSent, trans: "tx1"));
            Assert.That(uploaded, Does.Contain(RecoveryAction.Approve));
            Assert.That(uploaded, Does.Contain(RecoveryAction.UploadStatus));
            Assert.That(uploaded, Does.Not.Contain(RecoveryAction.MoveToKnox));

            var approved = RecoveryRules.AllowedActions(Phone(RecoveryStatus.Approved, trans: "tx1"));
            Assert.That(approved, Is.SupersetOf(new[] { RecoveryAction.Lock, RecoveryAction.Unlock, RecoveryAction.MoveToKnox }));
        }

        [Test]
        public void MovedOrEndedRecoveriesOnlyAllowChecksNotesAndReopening()
        {
            Assert.That(RecoveryRules.AllowedActions(Phone(RecoveryStatus.MovedToKnox, trans: "tx1")),
                Is.EquivalentTo(new[] { RecoveryAction.Note, RecoveryAction.KnoxCheck }));
            Assert.That(RecoveryRules.AllowedActions(Phone(RecoveryStatus.NotCatchable)),
                Is.EquivalentTo(new[] { RecoveryAction.Note, RecoveryAction.Reopen }));
        }

        [Test]
        public void FailedCallsNeverMoveTheStatus()
        {
            Assert.That(RecoveryRules.StatusAfter(RecoveryAction.Approve, false, RecoveryStatus.UploadSent), Is.EqualTo(RecoveryStatus.UploadSent));
            Assert.That(RecoveryRules.StatusAfter(RecoveryAction.Approve, true, RecoveryStatus.UploadSent), Is.EqualTo(RecoveryStatus.Approved));
            Assert.That(RecoveryRules.StatusAfter(RecoveryAction.Unlock, true, RecoveryStatus.LockSent), Is.EqualTo(RecoveryStatus.Approved));
            Assert.That(RecoveryRules.StatusAfter(RecoveryAction.KnoxCheck, true, RecoveryStatus.LockSent), Is.EqualTo(RecoveryStatus.LockSent));
        }

        [Test]
        public void CannotMoveToKnoxWhenTheAccountAlreadyHasAnEnrolment()
        {
            var approved = Phone(RecoveryStatus.Approved, trans: "tx1");
            Assert.That(RecoveryRules.CannotMoveToKnox(approved, false), Is.Null);
            Assert.That(RecoveryRules.CannotMoveToKnox(approved, true), Does.Contain("already has an enrolment"));
            Assert.That(RecoveryRules.CannotMoveToKnox(Phone(RecoveryStatus.UploadSent, trans: "tx1"), false), Is.Not.Null);
        }
    }

    public class RecoveryServiceTests
    {
        private FakeRecoveries _repo = null!;
        private FakeEnrolmentRepository _enrolments = null!;
        private InterfaceFake<IVeritechApiClient> _veritech = null!;
        private InterfaceFake<IKnoxGuardClient> _knox = null!;
        private HttpStatusCode _knoxStatus;
        private FakePayTrigger _payTrigger = null!;
        private HashSet<string> _knoxHas = null!;
        private string _knoxListResult = "SUCCESS";
        private FakeTime _time = null!;
        private RecoveryService _service = null!;

        [SetUp]
        public void SetUp()
        {
            _repo = new FakeRecoveries();
            _repo.Items[1] = new LockRecovery
            {
                Id = 1, AccountNo = 8680519, Imei = "355132188643698", Make = "samsung", Model = "SM-A065F",
                CustomerName = "Harrison", Status = RecoveryStatus.Investigating, CurrentLockGroup = 1,
            };
            _enrolments = new FakeEnrolmentRepository();
            _knoxStatus = HttpStatusCode.OK;

            IVeritechApiClient veritech;
            (veritech, _veritech) = InterfaceFake<IVeritechApiClient>.Create(new()
            {
                ["UploadDevicesAsync"] = _ => Task.FromResult(new CreateDeviceResponse
                {
                    Data = new UploadDeviceResultModel { Transaction_Id = "vkdp-123", Status = "success" },
                }),
                ["GetTransactionStatusAsync"] = _ => Task.FromResult(new SuccessTransactionStatusResponse
                {
                    Data = new TransactionStatusResultModel { Status = "Complete" },
                    Message = "OK",
                }),
            });

            _knoxHas = new HashSet<string>();
            _knoxListResult = "SUCCESS";
            Task<HttpResponseMessage> Reply(object?[]? _) =>
                Task.FromResult(new HttpResponseMessage(_knoxStatus) { Content = new StringContent("{\"result\":\"x\"}", Encoding.UTF8, "application/json") });
            IKnoxGuardClient knox;
            (knox, _knox) = InterfaceFake<IKnoxGuardClient>.Create(new()
            {
                ["ApproveDeviceAsync"] = Reply,
                ["ExecuteDeviceActionsAsync"] = Reply,
                ["ListDevicesAsync"] = args =>
                {
                    var search = ((ListDevicesRequest)args![0]!).Search;
                    return Task.FromResult(new ListDevicesResponse
                    {
                        Result = _knoxListResult,
                        DeviceList = _knoxHas.Contains(search!) ? new List<DeviceDto> { new() { Imei = search } } : null,
                    });
                },
            });

            _payTrigger = new FakePayTrigger();
            _time = new FakeTime();
            _service = new RecoveryService(_repo, _enrolments, veritech, knox, _payTrigger, _time, NullLogger<RecoveryService>.Instance);
        }

        [Test]
        public async Task UploadKeepsTheVeritechTransactionId()
        {
            var result = await _service.RunAsync(1, RecoveryAction.Upload, null, 7);

            Assert.That(result.Success, Is.True);
            Assert.That(_repo.Items[1].Status, Is.EqualTo(RecoveryStatus.UploadSent));
            Assert.That(_repo.Items[1].VeritechTransId, Is.EqualTo("vkdp-123"));
            Assert.That(_repo.Log.Single().Step, Is.EqualTo("Upload"));
        }

        [Test]
        public async Task UploadCompleteButNotInKnoxSaysAskVeritech()
        {
            // 5871723 (vkdp360691utid): Veritech "Complete", Knox never got it.
            _repo.Items[1].Status = RecoveryStatus.UploadSent;
            _repo.Items[1].VeritechTransId = "vkdp360691utid";

            var result = await _service.RunAsync(1, RecoveryAction.UploadStatus, null, 7);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Contain("Knox doesn't have the phone").And.Contain("vkdp360691utid"));
        }

        [Test]
        public async Task UploadCompleteAndInKnoxSaysApproveNext()
        {
            _repo.Items[1].Status = RecoveryStatus.UploadSent;
            _repo.Items[1].VeritechTransId = "vkdp-123";
            _knoxHas.Add("355132188643698");

            var result = await _service.RunAsync(1, RecoveryAction.UploadStatus, null, 7);

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(result.Message, Does.Contain("Knox has the phone"));
        }

        [Test]
        public async Task KnoxRefusingTheApprovalIsLoggedAndChangesNothing()
        {
            _repo.Items[1].Status = RecoveryStatus.UploadSent;
            _repo.Items[1].VeritechTransId = "vkdp-123";
            _knoxStatus = HttpStatusCode.BadRequest;

            var result = await _service.RunAsync(1, RecoveryAction.Approve, "first try", 7);

            Assert.That(result.Success, Is.False);
            Assert.That(_repo.Items[1].Status, Is.EqualTo(RecoveryStatus.UploadSent));
            Assert.That(_repo.Log.Single().Success, Is.False);
            Assert.That(_repo.Log.Single().Response, Does.StartWith("first try\nHTTP 400"));
        }

        [Test]
        public async Task StepsThatArentAvailableYetDontCallKnox()
        {
            var result = await _service.RunAsync(1, RecoveryAction.Lock, null, 7);

            Assert.That(result.Success, Is.False);
            Assert.That(_knox.Calls, Is.Empty);
            Assert.That(_repo.Log, Is.Empty);
        }

        [Test]
        public async Task ReopeningIsRefusedWhileAnotherRecoveryForTheAccountIsOpen()
        {
            // 8478363: recovery 5 closed, recovery 7 opened since.
            _repo.Items[1].Status = RecoveryStatus.Closed;
            _repo.Items[2] = new LockRecovery { Id = 2, AccountNo = 8680519, Imei = "355132188643698", Make = "samsung", Status = RecoveryStatus.Investigating };

            var result = await _service.RunAsync(1, RecoveryAction.Reopen, null, 7);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Contain("already has an open recovery (2)"));
            Assert.That(_repo.Items[1].Status, Is.EqualTo(RecoveryStatus.Closed));
            Assert.That(_repo.Log, Is.Empty);
        }

        [Test]
        public async Task KnoxLockSendsARelockTimestampOfNow()
        {
            // Knox's lock call needs LockDevice, which our account lacks (403).
            _repo.Items[1].Status = RecoveryStatus.Approved;
            _repo.Items[1].VeritechTransId = "vkdp-123";

            var result = await _service.RunAsync(1, RecoveryAction.Lock, null, 7);

            Assert.That(result.Success, Is.True, result.Message);
            var call = _knox.Calls.Single();
            Assert.That(call.Method, Is.EqualTo("ExecuteDeviceActionsAsync"));
            var request = (DeviceActionsRequest)call.Args![0]!;
            Assert.That(request.DeviceUid, Is.EqualTo("355132188643698"));
            Assert.That(request.ApproveId, Is.EqualTo("vkdp-123"));
            // Knox: "PAYG Actions must be two sets of arrays" -- unLock, then lock.
            Assert.That(request.Actions.Select(a => a.Action), Is.EqualTo(new[] { "unLock", "lock" }));
            Assert.That(request.Actions[1].Timestamp, Is.EqualTo(_time.Now.ToUnixTimeMilliseconds()));
            Assert.That(_repo.Items[1].Status, Is.EqualTo(RecoveryStatus.LockSent));
        }

        [Test]
        public async Task KnoxUnlockSendsAnUnlockAction()
        {
            _repo.Items[1].Status = RecoveryStatus.LockSent;
            _repo.Items[1].VeritechTransId = "vkdp-123";

            var result = await _service.RunAsync(1, RecoveryAction.Unlock, null, 7);

            Assert.That(result.Success, Is.True, result.Message);
            var request = (DeviceActionsRequest)_knox.Calls.Single().Args![0]!;
            Assert.That(request.Actions.Select(a => a.Action), Is.EqualTo(new[] { "unLock", "lock" }));
            Assert.That(request.Actions[1].Timestamp, Is.EqualTo(_time.Now.AddDays(1).ToUnixTimeMilliseconds()), "relocks a day ahead");
            Assert.That(_repo.Items[1].Status, Is.EqualTo(RecoveryStatus.Approved));
        }

        [Test]
        public async Task ReApprovingAPhoneAlreadyOnKnoxUpdatesItsEnrolmentsApproveId()
        {
            // 41109528: Knox lost it; uploaded again, new transaction id.
            var r = _repo.Items[1];
            r.CurrentLockGroup = 2;
            r.Status = RecoveryStatus.UploadSent;
            r.VeritechTransId = "vkdp-new";
            _enrolments.Items.Add(new Enrolment { Id = Guid.NewGuid(), AccountId = 8680519, IMEI = "355132188643698", VeriTechTransId = "vkdp465096utid" });

            var result = await _service.RunAsync(1, RecoveryAction.Approve, null, 7);

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(_enrolments.Items.Single().VeriTechTransId, Is.EqualTo("vkdp-new"));
            Assert.That(_repo.Log.Single().Response, Does.Contain("Enrolment approve id updated"));
        }

        [Test]
        public async Task MovingToKnoxHandsThePhoneToTheDailyJobs()
        {
            _repo.Items[1].Status = RecoveryStatus.LockSent;
            _repo.Items[1].VeritechTransId = "vkdp-123";

            var result = await _service.RunAsync(1, RecoveryAction.MoveToKnox, null, 7);

            Assert.That(result.Success, Is.True);
            var enrolment = _enrolments.Items.Single();
            Assert.That(enrolment.AccountId, Is.EqualTo(8680519));
            Assert.That(enrolment.IMEI, Is.EqualTo("355132188643698"));
            Assert.That(enrolment.VeriTechTransId, Is.EqualTo("vkdp-123"));
            Assert.That(enrolment.UpdatedBy, Is.EqualTo("RECOVERY"));
            Assert.That(_repo.LockGroupSet, Is.EqualTo((8680519L, 2, (bool?)true)));
            Assert.That(_repo.Items[1].Status, Is.EqualTo(RecoveryStatus.MovedToKnox));
        }

        [Test]
        public async Task MovingToKnoxIsRefusedWhenTheAccountHasAnEnrolment()
        {
            _repo.Items[1].Status = RecoveryStatus.Approved;
            _repo.Items[1].VeritechTransId = "vkdp-123";
            _enrolments.Items.Add(new Enrolment { AccountId = 8680519, IMEI = "999" });

            var result = await _service.RunAsync(1, RecoveryAction.MoveToKnox, null, 7);

            Assert.That(result.Success, Is.False);
            Assert.That(_enrolments.Items, Has.Count.EqualTo(1));
            Assert.That(_repo.LockGroupSet, Is.Null);
            Assert.That(_repo.Items[1].Status, Is.EqualTo(RecoveryStatus.Approved));
        }

        // ---- Transsion (account 8478363, a repossessed TECNO on Nuovo) ----

        private LockRecovery TranssionPhone(string status = RecoveryStatus.Investigating)
        {
            var r = new LockRecovery
            {
                Id = 2, AccountNo = 8478363, Imei = "352367242606595", Make = "TECNO", Model = "TECNO KM5",
                CustomerName = "Test", Status = status, CurrentLockGroup = 1,
            };
            _repo.Items[2] = r;
            return r;
        }

        [Test]
        public async Task SendingToTranssionPreEnrolsLockedOnSwitchOn()
        {
            TranssionPhone();

            var result = await _service.RunAsync(2, RecoveryAction.TranssionSend, null, 7);

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.True, result.Message);
                Assert.That(_payTrigger.PreEnrolCalls.Single().Items.Single().Imei, Is.EqualTo("352367242606595"));
                Assert.That(_payTrigger.PreEnrolCalls.Single().PreLock, Is.True, "a Recovery phone is assumed hacked: lock it as soon as it's active");
                Assert.That(_repo.Items[2].Status, Is.EqualTo(RecoveryStatus.UploadSent));
                Assert.That(_repo.Log.Single().Step, Is.EqualTo("TranssionSend"));
                Assert.That(_knox.Calls, Is.Empty);
            });
        }

        [Test]
        public async Task TranssionRefusingTheImeiIsLoggedAndChangesNothing()
        {
            TranssionPhone();
            _payTrigger.PreEnrolResponse = new PreEnrollImeiResponse
            {
                Code = 200, Message = "Success",
                Data = new List<PreEnrollImeiResultItem> { new() { Imei = "352367242606595", Message = "IMEI not supported", ErrCode = 7 } },
            };

            var result = await _service.RunAsync(2, RecoveryAction.TranssionSend, null, 7);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Contain("IMEI not supported"));
            Assert.That(_repo.Items[2].Status, Is.EqualTo(RecoveryStatus.Investigating));
            Assert.That(_repo.Log.Single().Success, Is.False);
        }

        [Test]
        public async Task CheckingAnActivePhoneHandsItToTheDailyJobs()
        {
            TranssionPhone(RecoveryStatus.UploadSent);
            _payTrigger.FindResponse = new FindLockStateResponse
            {
                Code = 200, Message = "Success",
                Data = new FindLockStateData { Imei = "352367242606595", LockState = 3000, MobileStatus = 2000, LastConnectTime = 1791900000, Model = "KM5" },
            };

            var result = await _service.RunAsync(2, RecoveryAction.TranssionCheck, null, 7);

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.True, result.Message);
                Assert.That(result.Message, Does.Contain("active"));
                Assert.That(result.Message, Does.Contain("moved from Nuovo to Transsion"));
                Assert.That(_payTrigger.FindCalls.Single().Imei, Is.EqualTo("352367242606595"));
                Assert.That(_repo.Items[2].Status, Is.EqualTo(RecoveryStatus.MovedToTranssion));
                Assert.That(_enrolments.Items.Single().IMEI, Is.EqualTo("352367242606595"));
                Assert.That(_repo.LockGroupSet, Is.EqualTo((8478363L, 3, (bool?)null)));
                Assert.That(_repo.Log.Select(l => l.Step), Is.EqualTo(new[] { "TranssionCheck", "MoveToTranssion" }));
            });
        }

        [Test]
        public async Task CheckingAnActivePhoneThatCantMoveStaysActive()
        {
            TranssionPhone(RecoveryStatus.UploadSent);
            _enrolments.Items.Add(new Enrolment { AccountId = 8478363, IMEI = "999" });
            _payTrigger.FindResponse = new FindLockStateResponse
            {
                Code = 200, Message = "Success",
                Data = new FindLockStateData { Imei = "352367242606595", LockState = 3000, MobileStatus = 1000 },
            };

            var result = await _service.RunAsync(2, RecoveryAction.TranssionCheck, null, 7);

            Assert.That(result.Success, Is.True);
            Assert.That(result.Message, Does.Contain("Not moved"));
            Assert.That(_repo.Items[2].Status, Is.EqualTo(RecoveryStatus.Approved), "Move to Transsion by hand once sorted");
            Assert.That(_repo.LockGroupSet, Is.Null);
        }

        [Test]
        public async Task CheckingAPhoneTranssionDoesntKnowFailsAndChangesNothing()
        {
            // What Transsion says for 352367242606595 today (2026-10-14).
            TranssionPhone(RecoveryStatus.UploadSent);

            var result = await _service.RunAsync(2, RecoveryAction.TranssionCheck, null, 7);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Contain("device-lock not exist"));
            Assert.That(_repo.Items[2].Status, Is.EqualTo(RecoveryStatus.UploadSent));
        }

        [Test]
        public async Task CheckingAPhoneThatHasntConnectedSaysSo()
        {
            // Transsion's reply for 352367242606595 just after Send (2026-10-14).
            TranssionPhone(RecoveryStatus.UploadSent);
            _payTrigger.FindResponse = new FindLockStateResponse
            {
                Code = 200, Message = "Success",
                Data = new FindLockStateData { Imei = "352367242606595", LockState = 0, MobileStatus = 2000, ServerState = 500, ServerLockStatus = 2000 },
            };

            var result = await _service.RunAsync(2, RecoveryAction.TranssionCheck, null, 7);

            Assert.That(result.Success, Is.True);
            Assert.That(result.Message, Does.Contain("hasn't contacted Transsion yet"));
            Assert.That(result.Message, Does.Not.Contain("unlocked"));
            Assert.That(_repo.Items[2].Status, Is.EqualTo(RecoveryStatus.UploadSent));
        }

        [Test]
        public async Task LockingInTranssionSetsTheLockDateToNow()
        {
            TranssionPhone(RecoveryStatus.Approved);

            var result = await _service.RunAsync(2, RecoveryAction.Lock, null, 7);

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(_payTrigger.RepayCalls.Single().Imei, Is.EqualTo("352367242606595"));
            Assert.That(_payTrigger.RepayCalls.Single().NextRepayTime, Is.EqualTo(_time.Now.ToUnixTimeSeconds()));
            Assert.That(_repo.Items[2].Status, Is.EqualTo(RecoveryStatus.LockSent));
            Assert.That(_knox.Calls, Is.Empty, "Transsion phones never go to Knox");
        }

        [Test]
        public async Task UnlockingInTranssionMovesTheLockDateADayAhead()
        {
            TranssionPhone(RecoveryStatus.LockSent);

            var result = await _service.RunAsync(2, RecoveryAction.Unlock, null, 7);

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(_payTrigger.RepayCalls.Single().NextRepayTime, Is.EqualTo(_time.Now.AddDays(1).ToUnixTimeSeconds()));
            Assert.That(_repo.Items[2].Status, Is.EqualTo(RecoveryStatus.Approved));
        }

        [Test]
        public async Task MovingToTranssionHandsThePhoneToTheDailyJobs()
        {
            TranssionPhone(RecoveryStatus.LockSent);

            var result = await _service.RunAsync(2, RecoveryAction.MoveToTranssion, null, 7);

            Assert.That(result.Success, Is.True, result.Message);
            var enrolment = _enrolments.Items.Single();
            Assert.Multiple(() =>
            {
                Assert.That(enrolment.AccountId, Is.EqualTo(8478363));
                Assert.That(enrolment.IMEI, Is.EqualTo("352367242606595"), "LockDevicesPayTrigger reads the IMEI from here");
                Assert.That(enrolment.DeviceBrand, Is.EqualTo("TECNO"));
                Assert.That(enrolment.UpdatedBy, Is.EqualTo("RECOVERY"));
                Assert.That(enrolment.Status, Is.EqualTo(EnrolmentStatus.Approved));
                Assert.That(Ranalo.Services.DeviceLock.DeviceLockRules.NeedsSendingToTranssion(enrolment), Is.False);
                Assert.That(Ranalo.Services.DeviceLock.DeviceLockRules.ReadyToUnlock(enrolment), Is.False, "no Retry unlock button");
                Assert.That(_repo.LockGroupSet, Is.EqualTo((8478363L, 3, (bool?)true)));
                Assert.That(_repo.Items[2].Status, Is.EqualTo(RecoveryStatus.MovedToTranssion));
            });
        }

        [Test]
        public async Task MovingAnUpperCaseInfinixStoresTheBrandTheAppKnows()
        {
            TranssionPhone(RecoveryStatus.Approved).Make = "INFINIX";

            await _service.RunAsync(2, RecoveryAction.MoveToTranssion, null, 7);

            Assert.That(_enrolments.Items.Single().DeviceBrand, Is.EqualTo("Infinix"));
            Assert.That(Ranalo.Services.DeviceLock.DeviceLockRules.IsTranssion(_enrolments.Items.Single().DeviceBrand), Is.True);
        }

        [Test]
        public async Task MovingToTranssionIsRefusedBeforeItsActive()
        {
            TranssionPhone(RecoveryStatus.UploadSent);

            var result = await _service.RunAsync(2, RecoveryAction.MoveToTranssion, null, 7);

            Assert.That(result.Success, Is.False);
            Assert.That(_enrolments.Items, Is.Empty);
            Assert.That(_repo.LockGroupSet, Is.Null);
        }

        [Test]
        public async Task MovingToTranssionIsRefusedWhenTheAccountHasAnEnrolment()
        {
            TranssionPhone(RecoveryStatus.Approved);
            _enrolments.Items.Add(new Enrolment { AccountId = 8478363, IMEI = "999" });

            var result = await _service.RunAsync(2, RecoveryAction.MoveToTranssion, null, 7);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Contain("already has an enrolment"));
            Assert.That(_repo.LockGroupSet, Is.Null);
        }

        // ---- Automatic Transsion checks (daily job + webhook) ----

        private void TranssionSays(int lockState) => _payTrigger.FindResponse = new FindLockStateResponse
        {
            Code = 200, Message = "Success",
            Data = new FindLockStateData { Imei = "352367242606595", LockState = lockState, MobileStatus = 1000 },
        };

        [Test]
        public async Task SweepMovesAPhoneTranssionNowHasActive()
        {
            TranssionPhone(RecoveryStatus.UploadSent);
            TranssionSays(3000);

            var moved = await _service.SweepTranssionAsync();

            Assert.That(moved, Is.EqualTo(1));
            Assert.That(_repo.Items[2].Status, Is.EqualTo(RecoveryStatus.MovedToTranssion));
            Assert.That(_repo.Log.First().Response, Does.StartWith("Automatic check"));
        }

        [Test]
        public async Task SweepLeavesWaitingPhonesAloneAndUnlogged()
        {
            TranssionPhone(RecoveryStatus.UploadSent);
            TranssionSays(0);

            var moved = await _service.SweepTranssionAsync();

            Assert.That(moved, Is.EqualTo(0));
            Assert.That(_repo.Items[2].Status, Is.EqualTo(RecoveryStatus.UploadSent));
            Assert.That(_repo.Log, Is.Empty, "no log line every day for a phone still waiting");
        }

        [Test]
        public async Task SweepOnlyAsksAboutTranssionPhonesThatWereSent()
        {
            TranssionPhone(RecoveryStatus.Investigating);   // not sent yet
            TranssionSays(3000);

            await _service.SweepTranssionAsync();

            Assert.That(_payTrigger.FindCalls, Is.Empty, "Samsung (recovery 1) and unsent phones aren't asked about");
        }

        [Test]
        public async Task SweepCarriesOnPastAFailure()
        {
            TranssionPhone(RecoveryStatus.UploadSent);
            _payTrigger.Throw = new HttpRequestException("timeout");

            Assert.That(await _service.SweepTranssionAsync(), Is.EqualTo(0));
        }

        [Test]
        public async Task WebhookActivationMovesTheRecoveryWaitingOnThatImei()
        {
            TranssionPhone(RecoveryStatus.UploadSent);
            TranssionSays(3000);

            await _service.OnTranssionActivatedAsync("352367242606595");

            Assert.That(_repo.Items[2].Status, Is.EqualTo(RecoveryStatus.MovedToTranssion));
        }

        [Test]
        public async Task WebhookActivationOfAnotherImeiDoesNothing()
        {
            TranssionPhone(RecoveryStatus.UploadSent);

            await _service.OnTranssionActivatedAsync("111111111111111");

            Assert.That(_payTrigger.FindCalls, Is.Empty);
            Assert.That(_repo.Items[2].Status, Is.EqualTo(RecoveryStatus.UploadSent));
        }

        // ---- Daily Knox approval ----

        [Test]
        public async Task KnoxApproval_ApprovesASentPhoneKnoxNowHas()
        {
            _repo.Items[1].Status = RecoveryStatus.UploadSent;
            _repo.Items[1].VeritechTransId = "vkdp-123";
            _knoxHas.Add("355132188643698");

            var approved = await _service.SweepKnoxApprovalsAsync();

            Assert.Multiple(() =>
            {
                Assert.That(approved, Is.EqualTo(1));
                Assert.That(_repo.Items[1].Status, Is.EqualTo(RecoveryStatus.Approved));
                Assert.That(_knox.Calls.Count(c => c.Method == "ApproveDeviceAsync"), Is.EqualTo(1));
                Assert.That(_repo.Log.Single().Response, Does.StartWith("Automatic approval"));
            });
        }

        [Test]
        public async Task KnoxApproval_LeavesPhonesNotInKnoxYetAloneAndUnlogged()
        {
            _repo.Items[1].Status = RecoveryStatus.UploadSent;
            _repo.Items[1].VeritechTransId = "vkdp-123";

            Assert.That(await _service.SweepKnoxApprovalsAsync(), Is.EqualTo(0));
            Assert.That(_repo.Items[1].Status, Is.EqualTo(RecoveryStatus.UploadSent));
            Assert.That(_repo.Log, Is.Empty);
            Assert.That(_knox.Calls.Any(c => c.Method == "ApproveDeviceAsync"), Is.False);
        }

        [Test]
        public async Task KnoxApproval_SkipsPhonesNotSentOrNotSamsung()
        {
            _knoxHas.Add("355132188643698");
            _repo.Items[1].Status = RecoveryStatus.Investigating;   // not sent yet
            TranssionPhone(RecoveryStatus.UploadSent);              // a TECNO

            Assert.That(await _service.SweepKnoxApprovalsAsync(), Is.EqualTo(0));
            Assert.That(_knox.Calls.Any(c => c.Method == "ApproveDeviceAsync"), Is.False);
        }

        // ---- Daily locker check (after 41109528 dropped out of Knox) ----

        private void OnKnox(long account, string imei, string? imei2 = null)
        {
            _repo.LockerDevices.Add(new LockerDevice { AccountNo = account, Imei = imei, Imei2 = imei2, LockGroup = 2 });
            _repo.Devices[account] = new RecoveryDeviceLookup { AccountNo = account, Imei = imei, Make = null, Model = "SM-A165F/DS", LockGroup = 2 };
        }

        [Test]
        public async Task LockerCheck_PhoneGoneFromKnox_OpensARecovery()
        {
            OnKnox(41109528, "352801608547712", "357778958547713");

            var opened = await _service.CheckLockersAsync();

            var recovery = _repo.Items.Values.Single(r => r.AccountNo == 41109528);
            Assert.Multiple(() =>
            {
                Assert.That(opened.Single(), Does.StartWith("41109528"));
                Assert.That(recovery.Reason, Is.EqualTo(RecoveryReason.LockLost));
                Assert.That(recovery.Notes, Does.Contain("Not in Knox any more").And.Contain("357778958547713"));
                Assert.That(recovery.Status, Is.EqualTo(RecoveryStatus.Investigating));
            });
        }

        [Test]
        public async Task LockerCheck_PhoneInKnox_NothingOpened()
        {
            OnKnox(2565870, "355039219214193");
            _knoxHas.Add("355039219214193");

            Assert.That(await _service.CheckLockersAsync(), Is.Empty);
            Assert.That(_repo.Items.Values.Any(r => r.AccountNo == 2565870), Is.False);
        }

        [Test]
        public async Task LockerCheck_KnoxHasItUnderTheSecondImei_NothingOpened()
        {
            OnKnox(2565870, "355039219214193", "355747149214196");
            _knoxHas.Add("355747149214196");

            Assert.That(await _service.CheckLockersAsync(), Is.Empty);
        }

        [Test]
        public async Task LockerCheck_KnoxErrors_IsNotTakenAsGone()
        {
            OnKnox(2565870, "355039219214193");
            _knoxListResult = "FAIL";

            Assert.That(await _service.CheckLockersAsync(), Is.Empty);
        }

        [Test]
        public async Task LockerCheck_AlreadyInRecovery_NotOpenedTwice()
        {
            OnKnox(8680519, "355132188643698");   // recovery 1 is open for 8680519

            Assert.That(await _service.CheckLockersAsync(), Is.Empty);
            Assert.That(_repo.Items.Values.Count(r => r.AccountNo == 8680519), Is.EqualTo(1));
        }

        [TestCase(50071, 0, true)]
        [TestCase(200, 5000, true)]
        [TestCase(200, 3000, false)]
        [TestCase(500, 0, false)]
        public async Task LockerCheck_Transsion(int code, int lockState, bool lost)
        {
            _repo.LockerDevices.Add(new LockerDevice { AccountNo = 22800363, Imei = "356938035643809", LockGroup = 3 });
            _repo.Devices[22800363] = new RecoveryDeviceLookup { AccountNo = 22800363, Imei = "356938035643809", Make = "TECNO", LockGroup = 3 };
            _payTrigger.FindResponse = new FindLockStateResponse
            {
                Code = code, Message = code == 50071 ? "The device-lock not exist" : "x",
                Data = code == 200 ? new FindLockStateData { LockState = lockState } : null,
            };

            var opened = await _service.CheckLockersAsync();

            Assert.That(opened.Count, Is.EqualTo(lost ? 1 : 0));
        }

        [Test]
        public async Task TranssionUnreachableIsLoggedAsAFailure()
        {
            TranssionPhone();
            _payTrigger.Throw = new HttpRequestException("timeout");

            var result = await _service.RunAsync(2, RecoveryAction.TranssionSend, null, 7);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Contain("timeout"));
            Assert.That(_repo.Log.Single().Success, Is.False);
        }

        [Test]
        public async Task AddingCopiesTheDeviceAndRefusesASecondOpenRecovery()
        {
            _repo.Devices[8511324] = new RecoveryDeviceLookup
            {
                AccountNo = 8511324, Imei = "358253401180537", Make = "samsung", Model = "SM-A065F", CustomerName = "Denis", LockGroup = 1,
            };

            var (id, error) = await _service.AddAsync(8511324, "Fraud", null, 7);
            Assert.That(error, Is.Null);
            Assert.That(_repo.Items[id!.Value].Imei, Is.EqualTo("358253401180537"));
            Assert.That(_repo.Items[id.Value].Reason, Is.EqualTo("Fraud"));

            var (againId, againError) = await _service.AddAsync(8511324, "Fraud", null, 7);
            Assert.That(againError, Does.Contain("already in Recovery"));
            Assert.That(againId, Is.EqualTo(id));

            var (_, missing) = await _service.AddAsync(1234567, "Fraud", null, 7);
            Assert.That(missing, Does.Contain("No device"));
        }

        private sealed class FakeRecoveries : ILockRecoveryRepository
        {
            public Dictionary<int, LockRecovery> Items { get; } = new();
            public Dictionary<long, RecoveryDeviceLookup> Devices { get; } = new();
            public List<LockRecoveryAttempt> Log { get; } = new();
            public (long, int, bool?)? LockGroupSet { get; private set; }

            public Task<List<LockRecovery>> GetListAsync(string status) => Task.FromResult(Items.Values.ToList());
            public Task<LockRecovery?> GetAsync(int id) => Task.FromResult(Items.GetValueOrDefault(id));
            public Task<List<LockRecoveryAttempt>> GetAttemptsAsync(int recoveryId) => Task.FromResult(Log.Where(a => a.RecoveryId == recoveryId).ToList());
            public Task<RecoveryDeviceLookup?> LookupDeviceAsync(long accountNo) => Task.FromResult(Devices.GetValueOrDefault(accountNo));

            public Task<int?> GetOpenIdForAccountAsync(long accountNo) =>
                Task.FromResult(Items.Values.Where(r => r.AccountNo == accountNo && RecoveryRules.IsOpen(r.Status)).Select(r => (int?)r.Id).FirstOrDefault());

            public Task<int> CreateAsync(LockRecovery recovery)
            {
                recovery.Id = Items.Count == 0 ? 1 : Items.Keys.Max() + 1;
                Items[recovery.Id] = recovery;
                return Task.FromResult(recovery.Id);
            }

            public Task UpdateAsync(int id, string status, string? veritechTransId)
            {
                Items[id].Status = status;
                Items[id].VeritechTransId = veritechTransId ?? Items[id].VeritechTransId;
                return Task.CompletedTask;
            }

            public Task SetNotesAsync(int id, string? notes) { Items[id].Notes = notes; return Task.CompletedTask; }

            public Task LogAsync(int recoveryId, string step, bool success, string? response, int? userId)
            {
                Log.Add(new LockRecoveryAttempt { RecoveryId = recoveryId, Step = step, Success = success, Response = response });
                return Task.CompletedTask;
            }

            public Task SetLockGroupAsync(long accountNo, int lockGroup, bool? locked) { LockGroupSet = (accountNo, lockGroup, locked); return Task.CompletedTask; }
            public Task MarkMovedAsync(int id, string movedStatus) { Items[id].Status = movedStatus; return Task.CompletedTask; }
            public List<LockerDevice> LockerDevices { get; } = new();
            public Task<List<LockerDevice>> GetLockerDevicesAsync() => Task.FromResult(LockerDevices);
        }
    }
}
