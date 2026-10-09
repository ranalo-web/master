using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Ranalo.DataStore;
using Ranalo.Models;
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
            var actions = RecoveryRules.AllowedActions(Phone(make: "TECNO"));
            Assert.That(actions, Is.EquivalentTo(new[] { RecoveryAction.Note, RecoveryAction.NotCatchable, RecoveryAction.Close }));
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
            });

            Task<HttpResponseMessage> Reply(object?[]? _) =>
                Task.FromResult(new HttpResponseMessage(_knoxStatus) { Content = new StringContent("{\"result\":\"x\"}", Encoding.UTF8, "application/json") });
            IKnoxGuardClient knox;
            (knox, _knox) = InterfaceFake<IKnoxGuardClient>.Create(new()
            {
                ["ApproveDeviceAsync"] = Reply,
                ["LockDeviceAsync"] = Reply,
                ["UnlockDeviceAsync"] = Reply,
            });

            _service = new RecoveryService(_repo, _enrolments, veritech, knox, NullLogger<RecoveryService>.Instance);
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
            public Task MarkMovedToKnoxAsync(int id) { Items[id].Status = RecoveryStatus.MovedToKnox; return Task.CompletedTask; }
        }
    }
}
