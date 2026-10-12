using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Ranalo.DataStore;
using Ranalo.DataStore.DataModels;
using Ranalo.Models;
using Ranalo.ScheduledServices;
using Ranalo.Services;
using Ranalo.Services.DeviceLock;
using MobileStatusReport = Ranalo.Models.MobileStatusReport;

namespace Ranolo.Web.Tests
{
    // Lost & stolen phones: who can report and see what, evidence checks, and
    // that a confirmed phone stays locked.
    public class LostStolenRulesTests
    {
        private static User U(UserRole role, int userId = 10, int dealerId = 0, params string[] otherRoles) =>
            new() { UserId = userId, RoleId = role, DealerId = dealerId, OtherSelectedRoles = otherRoles.ToList() };

        private static LostStolenAccountScope Acct(int? dealer = 5, int? agent = 20, int? collector = 30, bool hasDevice = true) =>
            new() { AccountNo = 1, HasDevice = hasDevice, DealerId = dealer, AssignedAgentId = agent, CollectorUserId = collector };

        [Test]
        public void AdminAndApproverSeeEveryAccount()
        {
            Assert.That(LostStolenRules.InScope(U(UserRole.Admin), Acct(dealer: null, agent: null, collector: null)), Is.True);
            Assert.That(LostStolenRules.InScope(U(UserRole.Approver), Acct()), Is.True);
        }

        [Test]
        public void NoPhoneOnTheAccountIsNeverInScope()
        {
            Assert.That(LostStolenRules.InScope(U(UserRole.Admin), Acct(hasDevice: false)), Is.False);
        }

        [Test]
        public void DealerSeesOnlyTheirDealersAccounts()
        {
            Assert.That(LostStolenRules.InScope(U(UserRole.Dealer, dealerId: 5), Acct(dealer: 5)), Is.True);
            Assert.That(LostStolenRules.InScope(U(UserRole.Dealer, dealerId: 6), Acct(dealer: 5)), Is.False);
            Assert.That(LostStolenRules.InScope(U(UserRole.Dealer, dealerId: 0), Acct(dealer: null)), Is.False);
        }

        [Test]
        public void AgentSeesOnlyTheirAssignedAccounts()
        {
            Assert.That(LostStolenRules.InScope(U(UserRole.Agent, userId: 20), Acct(agent: 20)), Is.True);
            Assert.That(LostStolenRules.InScope(U(UserRole.Agent, userId: 21), Acct(agent: 20)), Is.False);
        }

        [Test]
        public void CollectorSeesOnlyAccountsTheyHold()
        {
            Assert.That(LostStolenRules.InScope(U(UserRole.Collector, userId: 30), Acct(collector: 30)), Is.True);
            Assert.That(LostStolenRules.InScope(U(UserRole.Collector, userId: 31), Acct(collector: 30)), Is.False);
        }

        [Test]
        public void DealerWhoIsAlsoACollectorSeesAccountsTheyHoldForOtherDealers()
        {
            var dealerCollector = U(UserRole.Dealer, userId: 30, dealerId: 9, "Collector");
            Assert.That(LostStolenRules.InScope(dealerCollector, Acct(dealer: 5, collector: 30)), Is.True);
            Assert.That(LostStolenRules.InScope(dealerCollector, Acct(dealer: 5, collector: 99)), Is.False);
        }

        [TestCase(UserRole.Admin, true)]
        [TestCase(UserRole.Approver, true)]
        [TestCase(UserRole.Dealer, true)]
        [TestCase(UserRole.Agent, true)]
        [TestCase(UserRole.Collector, true)]
        [TestCase(UserRole.Customer, false)]
        [TestCase(UserRole.Supplier, false)]
        [TestCase(UserRole.Guest, false)]
        public void WhoCanUseThePage(UserRole role, bool expected)
        {
            Assert.That(LostStolenRules.CanUsePage(U(role)), Is.EqualTo(expected));
        }

        [TestCase(UserRole.Admin, true)]
        [TestCase(UserRole.Approver, false)]
        [TestCase(UserRole.Dealer, false)]
        [TestCase(UserRole.Collector, false)]
        public void OnlyAdminDecides(UserRole role, bool expected)
        {
            Assert.That(LostStolenRules.CanDecide(role), Is.EqualTo(expected));
        }

        private static readonly DateTime Today = new(2026, 10, 12);

        private static NewLostStolenReport Valid() => new()
        {
            AccountNo = 1, Kind = LostStolenKind.Stolen, IncidentDate = Today.AddDays(-2), Description = "Snatched in town",
        };

        [Test]
        public void ValidReportPasses()
        {
            Assert.That(LostStolenRules.ValidateReport(Valid(), Today), Is.Null);
        }

        [Test]
        public void ReportProblemsAreExplained()
        {
            var r = Valid(); r.Kind = "Broken";
            Assert.That(LostStolenRules.ValidateReport(r, Today), Does.Contain("lost or stolen"));
            r = Valid(); r.IncidentDate = null;
            Assert.That(LostStolenRules.ValidateReport(r, Today), Does.Contain("date"));
            r = Valid(); r.IncidentDate = Today.AddDays(1);
            Assert.That(LostStolenRules.ValidateReport(r, Today), Does.Contain("future"));
            r = Valid(); r.Description = "  ";
            Assert.That(LostStolenRules.ValidateReport(r, Today), Does.Contain("what happened"));
            r = Valid(); r.Description = new string('x', 1001);
            Assert.That(LostStolenRules.ValidateReport(r, Today), Does.Contain("1000"));
            r = Valid(); r.PoliceRef = new string('x', 101);
            Assert.That(LostStolenRules.ValidateReport(r, Today), Does.Contain("too long"));
        }

        internal static readonly byte[] Jpeg = { 0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3 };
        internal static readonly byte[] Png = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A };
        internal static readonly byte[] Pdf = "%PDF-1.7 abstract"u8.ToArray();
        internal static readonly byte[] Webp = "RIFF\0\0\0\0WEBPVP8 "u8.ToArray();
        internal static readonly byte[] Exe = { 0x4D, 0x5A, 0x90, 0x00 };   // MZ

        [Test]
        public void RealImagesAndPdfsAreAccepted()
        {
            Assert.That(LostStolenRules.CheckEvidence("photo.JPG", Jpeg), Is.EqualTo(("image/jpeg", (string?)null)));
            Assert.That(LostStolenRules.CheckEvidence("photo.png", Png).ContentType, Is.EqualTo("image/png"));
            Assert.That(LostStolenRules.CheckEvidence("abstract.pdf", Pdf).ContentType, Is.EqualTo("application/pdf"));
            Assert.That(LostStolenRules.CheckEvidence("photo.webp", Webp).ContentType, Is.EqualTo("image/webp"));
        }

        [Test]
        public void RenamedOrOtherFilesAreRefused()
        {
            Assert.That(LostStolenRules.CheckEvidence("abstract.pdf", Exe).Error, Does.Contain("isn't a real PDF"));
            Assert.That(LostStolenRules.CheckEvidence("photo.jpg", Png).Error, Does.Contain("isn't a real JPG"));
            Assert.That(LostStolenRules.CheckEvidence("tool.exe", Exe).Error, Does.Contain("photo"));
            Assert.That(LostStolenRules.CheckEvidence("notes.docx", Pdf).Error, Does.Contain("photo"));
        }

        [Test]
        public void EmptyAndOversizedFilesAreRefused()
        {
            Assert.That(LostStolenRules.CheckEvidence("a.pdf", Array.Empty<byte>()).Error, Does.Contain("empty"));
            var big = new byte[LostStolenRules.MaxFileBytes + 1];
            Pdf.CopyTo(big, 0);
            Assert.That(LostStolenRules.CheckEvidence("a.pdf", big).Error, Does.Contain("0.25 MB"));
        }

        [Test]
        public void FileOfExactlyTheLimitIsAccepted()
        {
            Assert.That(LostStolenRules.MaxFileBytes, Is.EqualTo(256 * 1024));
            var atLimit = new byte[LostStolenRules.MaxFileBytes];
            Jpeg.CopyTo(atLimit, 0);
            Assert.That(LostStolenRules.CheckEvidence("abstract.jpg", atLimit).Error, Is.Null);
        }

        [Test]
        public void FileNamesLoseTheirPathAndQuotes()
        {
            Assert.That(LostStolenRules.SafeFileName(@"C:\Users\x\abstract.pdf"), Is.EqualTo("abstract.pdf"));
            Assert.That(LostStolenRules.SafeFileName("../../ab\"stract.pdf"), Is.EqualTo("abstract.pdf"));
            Assert.That(LostStolenRules.SafeFileName(""), Is.EqualTo("evidence"));
        }

        [Test]
        public void ConfirmNeedsAPendingReportWithEvidence()
        {
            var r = new LostStolenReport { Status = LostStolenStatus.Pending };
            Assert.That(LostStolenRules.CheckConfirm(r), Does.Contain("evidence"));
            r.Evidence.Add(new LostStolenEvidence());
            Assert.That(LostStolenRules.CheckConfirm(r), Is.Null);
            r.Status = LostStolenStatus.Confirmed;
            Assert.That(LostStolenRules.CheckConfirm(r), Does.Contain("already confirmed"));
        }

        [Test]
        public void LeaveLockedDropsOnlyLostStolenAccounts()
        {
            var records = new List<long> { 1, 2, 3 };
            LostStolenRules.LeaveLocked(records, x => x, new HashSet<long> { 2 });
            Assert.That(records, Is.EqualTo(new long[] { 1, 3 }));

            LostStolenRules.LeaveLocked(records, x => x, null);
            LostStolenRules.LeaveLocked<long>(null, x => x, new HashSet<long> { 1 });
            Assert.That(records, Is.EqualTo(new long[] { 1, 3 }));
        }
    }

    public class LostStolenServiceTests
    {
        private const long Account = 7320500;

        private Dictionary<long, LostStolenAccountScope> _scopes = null!;
        private List<LostStolenReport> _reports = null!;
        private bool _createSucceeds;
        private bool _confirmSucceeds;
        private (bool Sent, string Result) _lockResult;
        private List<(long AccountNo, int? LockGroup)> _locks = null!;
        private InterfaceFake<ILostStolenRepository> _repo = null!;
        private LostStolenService _service = null!;

        private static readonly User Admin = new() { UserId = 1, RoleId = UserRole.Admin };
        private static readonly User Dealer5 = new() { UserId = 2, RoleId = UserRole.Dealer, DealerId = 5 };
        private static readonly User Dealer6 = new() { UserId = 3, RoleId = UserRole.Dealer, DealerId = 6 };
        private static readonly User Approver = new() { UserId = 4, RoleId = UserRole.Approver };

        [SetUp]
        public void SetUp()
        {
            _scopes = new() { [Account] = new LostStolenAccountScope { AccountNo = Account, HasDevice = true, DealerId = 5, LockGroup = 2 } };
            _reports = new();
            _createSucceeds = true;
            _confirmSucceeds = true;
            _lockResult = (true, "Knox: 200 ok");
            _locks = new();

            var (repo, fake) = InterfaceFake<ILostStolenRepository>.Create(new()
            {
                ["GetScopesAsync"] = a => Task.FromResult(((IReadOnlyCollection<long>)a![0]!)
                    .Where(_scopes.ContainsKey).ToDictionary(n => n, n => _scopes[n])),
                ["CreateAsync"] = _ => Task.FromResult<int?>(_createSucceeds ? 55 : null),
                ["AddEvidenceAsync"] = _ => Task.CompletedTask,
                ["GetReportAsync"] = a => Task.FromResult(_reports.FirstOrDefault(r => r.Id == (int)a![0]!)),
                ["GetReportsAsync"] = _ => Task.FromResult(_reports.ToList()),
                ["GetEvidenceFileAsync"] = a => Task.FromResult<LostStolenEvidenceFile?>(new LostStolenEvidenceFile { Id = (int)a![0]!, ReportId = 9 }),
                ["ConfirmAsync"] = _ => Task.FromResult(_confirmSucceeds),
                ["RejectAsync"] = _ => Task.FromResult(true),
                ["CloseAsync"] = _ => Task.FromResult(true),
                ["RecordLockAsync"] = _ => Task.CompletedTask,
            });
            _repo = fake;

            var (locker, _) = InterfaceFake<ILostStolenLocker>.Create(new()
            {
                ["LockNowAsync"] = a => { _locks.Add(((long)a![0]!, (int?)a[1])); return Task.FromResult(_lockResult); },
            });

            _service = new LostStolenService(repo, locker, new FakeTime { Now = new DateTimeOffset(2026, 10, 12, 9, 0, 0, TimeSpan.Zero) },
                NullLogger<LostStolenService>.Instance);
        }

        private static NewLostStolenReport Report(long account = Account) => new()
        {
            AccountNo = account, Kind = LostStolenKind.Stolen, IncidentDate = new DateTime(2026, 10, 10), Description = "Snatched in town",
        };

        private static LostStolenUpload File(string name, byte[] content) => new(name, null, content);

        private int Count(string method) => _repo.Calls.Count(c => c.Method == method);

        private LostStolenReport Pending(int id = 9, bool withEvidence = true)
        {
            var r = new LostStolenReport { Id = id, AccountNo = Account, Status = LostStolenStatus.Pending };
            if (withEvidence) r.Evidence.Add(new LostStolenEvidence { Id = 1, ReportId = id });
            _reports.Add(r);
            return r;
        }

        [Test]
        public async Task DealerReportsTheirOwnAccountWithEvidence()
        {
            var (ok, message) = await _service.ReportAsync(Dealer5, Report(), new[] { File("abstract.pdf", LostStolenRulesTests.Pdf), File("photo.jpg", LostStolenRulesTests.Jpeg) });

            Assert.That(ok, Is.True);
            Assert.That(message, Does.Contain("2 file(s)"));
            Assert.That(Count("CreateAsync"), Is.EqualTo(1));
            Assert.That(Count("AddEvidenceAsync"), Is.EqualTo(2));
            var created = _repo.Calls.Single(c => c.Method == "CreateAsync").Args!;
            Assert.That(created[1], Is.EqualTo(Dealer5.UserId));
            Assert.That(created[2], Is.EqualTo("Dealer"));
        }

        [Test]
        public async Task ReportWithoutEvidenceIsAcceptedButSaysItIsNeeded()
        {
            var (ok, message) = await _service.ReportAsync(Dealer5, Report(), Array.Empty<LostStolenUpload>());

            Assert.That(ok, Is.True);
            Assert.That(message, Does.Contain("Upload the police abstract"));
        }

        [Test]
        public async Task AnotherDealersAccountCantBeReported()
        {
            var (ok, message) = await _service.ReportAsync(Dealer6, Report(), Array.Empty<LostStolenUpload>());

            Assert.That(ok, Is.False);
            Assert.That(message, Does.Contain("isn't one of your accounts"));
            Assert.That(Count("CreateAsync"), Is.Zero);
        }

        [Test]
        public async Task UnknownAccountCantBeReportedEvenByAdmin()
        {
            var (ok, _) = await _service.ReportAsync(Admin, Report(account: 999), Array.Empty<LostStolenUpload>());

            Assert.That(ok, Is.False);
            Assert.That(Count("CreateAsync"), Is.Zero);
        }

        [Test]
        public async Task BadFileStopsTheWholeReport()
        {
            var (ok, message) = await _service.ReportAsync(Dealer5, Report(), new[] { File("abstract.pdf", LostStolenRulesTests.Exe) });

            Assert.That(ok, Is.False);
            Assert.That(message, Does.Contain("isn't a real PDF"));
            Assert.That(Count("CreateAsync"), Is.Zero);
        }

        [Test]
        public async Task TooManyFilesAreRefused()
        {
            var files = Enumerable.Range(0, 6).Select(i => File($"p{i}.jpg", LostStolenRulesTests.Jpeg)).ToList();

            var (ok, message) = await _service.ReportAsync(Dealer5, Report(), files);

            Assert.That(ok, Is.False);
            Assert.That(message, Does.Contain("at most 5"));
        }

        [Test]
        public async Task SecondOpenReportOnTheAccountIsRefused()
        {
            _createSucceeds = false;

            var (ok, message) = await _service.ReportAsync(Dealer5, Report(), Array.Empty<LostStolenUpload>());

            Assert.That(ok, Is.False);
            Assert.That(message, Does.Contain("already has an open"));
        }

        [Test]
        public async Task AdminConfirmLocksThePhoneWithItsProvider()
        {
            Pending();

            var (ok, message) = await _service.ConfirmAsync(Admin, 9, "abstract checked");

            Assert.That(ok, Is.True);
            Assert.That(message, Does.Contain("Lock sent"));
            Assert.That(_locks, Is.EqualTo(new[] { (Account, (int?)2) }));
            var recorded = (string)_repo.Calls.Single(c => c.Method == "RecordLockAsync").Args![1]!;
            Assert.That(recorded, Does.StartWith("Sent."));
        }

        [Test]
        public async Task FailedLockIsRecordedAndReported()
        {
            Pending();
            _lockResult = (false, "Knox: 500 error");

            var (ok, message) = await _service.ConfirmAsync(Admin, 9, null);

            Assert.That(ok, Is.False);
            Assert.That(message, Does.Contain("Lock again"));
            var recorded = (string)_repo.Calls.Single(c => c.Method == "RecordLockAsync").Args![1]!;
            Assert.That(recorded, Does.StartWith("FAILED."));
        }

        [Test]
        public async Task OnlyAdminCanConfirm()
        {
            Pending();

            var (ok, _) = await _service.ConfirmAsync(Approver, 9, null);

            Assert.That(ok, Is.False);
            Assert.That(_locks, Is.Empty);
            Assert.That(Count("ConfirmAsync"), Is.Zero);
        }

        [Test]
        public async Task CantConfirmWithoutEvidence()
        {
            Pending(withEvidence: false);

            var (ok, message) = await _service.ConfirmAsync(Admin, 9, null);

            Assert.That(ok, Is.False);
            Assert.That(message, Does.Contain("evidence"));
            Assert.That(_locks, Is.Empty);
        }

        [Test]
        public async Task SecondAdminConfirmingAtTheSameTimeDoesntLockTwice()
        {
            Pending();
            _confirmSucceeds = false;

            var (ok, _) = await _service.ConfirmAsync(Admin, 9, null);

            Assert.That(ok, Is.False);
            Assert.That(_locks, Is.Empty);
        }

        [Test]
        public async Task LockAgainOnlyForConfirmedReports()
        {
            var r = Pending();

            Assert.That((await _service.ResendLockAsync(Admin, 9)).Ok, Is.False);

            r.Status = LostStolenStatus.Confirmed;
            Assert.That((await _service.ResendLockAsync(Admin, 9)).Ok, Is.True);
            Assert.That(_locks, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task RejectNeedsAReason()
        {
            Pending();

            Assert.That((await _service.RejectAsync(Admin, 9, " ")).Ok, Is.False);
            Assert.That((await _service.RejectAsync(Admin, 9, "Abstract is for another phone")).Ok, Is.True);
            Assert.That(_locks, Is.Empty);
        }

        [Test]
        public async Task CloseNeedsAKnownOutcome()
        {
            Assert.That((await _service.CloseAsync(Admin, 9, "Lost", null)).Ok, Is.False);
            Assert.That((await _service.CloseAsync(Dealer5, 9, LostStolenOutcome.Found, null)).Ok, Is.False);

            var (ok, message) = await _service.CloseAsync(Admin, 9, LostStolenOutcome.Found, "returned by finder");
            Assert.That(ok, Is.True);
            Assert.That(message, Does.Contain("found"));
        }

        [Test]
        public async Task DealerSeesOnlyReportsOnTheirAccounts()
        {
            _scopes[42] = new LostStolenAccountScope { AccountNo = 42, HasDevice = true, DealerId = 6 };
            _reports.Add(new LostStolenReport { Id = 1, AccountNo = Account, Status = LostStolenStatus.Pending });
            _reports.Add(new LostStolenReport { Id = 2, AccountNo = 42, Status = LostStolenStatus.Confirmed });

            var dealer5 = await _service.GetPageAsync(Dealer5, "open");
            var admin = await _service.GetPageAsync(Admin, "open");

            Assert.That(dealer5.Reports.Select(r => r.Id), Is.EqualTo(new[] { 1 }));
            Assert.That(dealer5.CanDecide, Is.False);
            Assert.That(admin.Reports, Has.Count.EqualTo(2));
            Assert.That(admin.CanDecide, Is.True);
            Assert.That((admin.PendingCount, admin.ConfirmedCount), Is.EqualTo((1, 1)));
        }

        [Test]
        public async Task EvidenceIsOnlyDownloadableByThoseWhoCanSeeTheAccount()
        {
            Pending();

            Assert.That(await _service.GetEvidenceAsync(Dealer5, 1), Is.Not.Null);
            Assert.That(await _service.GetEvidenceAsync(Dealer6, 1), Is.Null);
        }

        [Test]
        public async Task EvidenceCantBeAddedToAClosedReport()
        {
            Pending().Status = LostStolenStatus.Closed;

            var (ok, message) = await _service.AddEvidenceAsync(Dealer5, 9, new[] { File("a.pdf", LostStolenRulesTests.Pdf) });

            Assert.That(ok, Is.False);
            Assert.That(message, Does.Contain("closed"));
        }
    }

    public class LostStolenLockerTests
    {
        private FakeKnox _knox = null!;
        private FakePayTrigger _payTrigger = null!;
        private FakeEnrolmentRepository _enrolments = null!;
        private List<LockTransaction> _nuovo = null!;
        private LostStolenLocker _locker = null!;
        private static readonly DateTimeOffset Now = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);

        [SetUp]
        public void SetUp()
        {
            _knox = new FakeKnox();
            _payTrigger = new FakePayTrigger();
            _enrolments = new FakeEnrolmentRepository();
            _enrolments.Items.Add(new Enrolment { AccountId = 7, IMEI = "356938035643809", VeriTechTransId = "vk1" });
            _nuovo = new();
            var (processor, _) = InterfaceFake<IDeviceProcessor>.Create(new()
            {
                ["ProcessSingleAsync"] = a => { var t = (LockTransaction)a![0]!; t.Result = "Device updated"; _nuovo.Add(t); return Task.FromResult(t); },
            });
            _locker = new LostStolenLocker(processor, _knox, _payTrigger, _enrolments, new FakeTime { Now = Now }, NullLogger<LostStolenLocker>.Instance);
        }

        [Test]
        public async Task KnoxLocksNowByImei()
        {
            var (sent, result) = await _locker.LockNowAsync(7, lockGroup: 2);

            Assert.That(sent, Is.True);
            Assert.That(result, Does.StartWith("Knox: 200"));
            var call = _knox.ActionCalls.Single();
            Assert.That(call.DeviceUid, Is.EqualTo("356938035643809"));
            Assert.That(call.Actions.Last().Action, Is.EqualTo("lock"));
            Assert.That(call.Actions.Last().Timestamp, Is.EqualTo(Now.ToUnixTimeMilliseconds()));
        }

        [Test]
        public async Task KnoxErrorIsAFailedLock()
        {
            _knox.Status = HttpStatusCode.InternalServerError;

            var (sent, _) = await _locker.LockNowAsync(7, lockGroup: 2);

            Assert.That(sent, Is.False);
        }

        [Test]
        public async Task TranssionLocksNowByImei()
        {
            var (sent, result) = await _locker.LockNowAsync(7, lockGroup: 3);

            Assert.That(sent, Is.True);
            Assert.That(result, Does.StartWith("Transsion: 200"));
            Assert.That(_payTrigger.RepayCalls.Single().NextRepayTime, Is.EqualTo(Now.ToUnixTimeSeconds()));
        }

        [Test]
        public async Task NuovoLocksNowByAccount()
        {
            var (sent, result) = await _locker.LockNowAsync(7, lockGroup: 1);

            Assert.That(sent, Is.True);
            Assert.That(result, Is.EqualTo("Nuovo: Device updated"));
            Assert.That(_nuovo.Single().AccountId, Is.EqualTo(7));
            Assert.That(_nuovo.Single().AutoLockDate, Is.EqualTo(Now.UtcDateTime));
        }

        [Test]
        public async Task KnoxWithoutAnEnrolmentCantLock()
        {
            var (sent, result) = await _locker.LockNowAsync(8, lockGroup: 2);

            Assert.That(sent, Is.False);
            Assert.That(result, Does.Contain("no IMEI"));
            Assert.That(_knox.ActionCalls, Is.Empty);
        }

        [Test]
        public async Task ProviderExceptionIsAFailedLockNotACrash()
        {
            _payTrigger.Throw = new HttpRequestException("timeout");

            var (sent, result) = await _locker.LockNowAsync(7, lockGroup: 3);

            Assert.That(sent, Is.False);
            Assert.That(result, Does.Contain("timeout"));
        }
    }

    public class LostStolenLockJobTests
    {
        private static MobileStatusReport Paid(int no, int? lockGroup) => new()
        {
            AccountNo = no, FirstName = $"C{no}", ImeiNo = $"35000000000000{no}", LockGroup = lockGroup,
            Arrears = 0, LoanBalance = 0, NextLockDate = $"15/10/{DateTime.UtcNow.Year}T10:00:00",
        };

        [Test]
        public async Task FullyPaidJobLeavesAStolenPhoneLockedAndQueuesNoRemoval()
        {
            var records = new List<MobileStatusReport> { Paid(1, 1), Paid(2, 2) };
            var (reports, _) = InterfaceFake<IApplicationReportService>.Create(new()
            {
                ["GetStatusReportByDealer"] = _ => Task.FromResult(new StatusReportViewModel { StatusReports = records })
            });
            List<LockTransaction>? nuovo = null;
            var (processor, _) = InterfaceFake<IDeviceProcessor>.Create(new()
            {
                ["ProcessBatchesAsync"] = a => { nuovo = (List<LockTransaction>)a![0]!; return Task.FromResult(nuovo); }
            });
            var (enrolmentService, enrolmentFake) = InterfaceFake<IEnrolmentService>.Create(new());
            var (payments, _) = InterfaceFake<IPaymentsRepository>.Create(new());
            var tasks = new FakeRemovalTasks();
            var removal = new DeviceRemovalService(tasks, new FakeEnrolmentRepository(), new FakeDevices(), new FakePayTrigger(), new FakeKnox(),
                processor, new FakeTime(), NullLogger<DeviceRemovalService>.Instance);

            var job = new ScheduledLockFullyPaid(NullLogger<ScheduledLockFullyPaid>.Instance, null!);
            await job.Process(processor, reports, payments, enrolmentService, removal, new HashSet<long> { 2 });

            Assert.That(nuovo!.Select(d => d.AccountId), Is.EqualTo(new long[] { 1 }));
            Assert.That(enrolmentFake.Calls.Select(c => c.Method), Does.Not.Contain("SetFullyPaidKnox"), "stolen Knox phone not released");
            Assert.That(tasks.Items.Select(t => t.AccountId), Is.EqualTo(new long[] { 1 }), "no removal queued for the stolen phone");
        }

        [Test]
        public async Task PayingJobDoesntMoveAStolenPhonesLockDate()
        {
            // Both paid ahead (positive Arrears): the job would normally push their
            // lock date forward (unlocking them).
            var records = new List<MobileStatusReport>
            {
                new() { AccountNo = 1, FirstName = "C1", LockGroup = 1, Arrears = 200, LoanBalance = 5000, Daily = 50, NextLockDate = "15/10/2026T10:00:00" },
                new() { AccountNo = 2, FirstName = "C2", LockGroup = 1, Arrears = 200, LoanBalance = 5000, Daily = 50, NextLockDate = "15/10/2026T10:00:00" },
            };
            var (reports, _) = InterfaceFake<IApplicationReportService>.Create(new()
            {
                ["GetStatusReportByDealer"] = _ => Task.FromResult(new StatusReportViewModel { StatusReports = records })
            });
            List<LockTransaction>? nuovo = null;
            var (processor, _) = InterfaceFake<IDeviceProcessor>.Create(new()
            {
                ["ProcessBatchesAsync"] = a => { nuovo = (List<LockTransaction>)a![0]!; return Task.FromResult(nuovo); }
            });
            var (enrolmentService, _) = InterfaceFake<IEnrolmentService>.Create(new());
            var (payments, _) = InterfaceFake<IPaymentsRepository>.Create(new());

            var job = new ScheduledLockPaying(NullLogger<ScheduledLockPaying>.Instance, null!);

            // Control: without the report, account 2's lock date moves too.
            await job.Process(processor, reports, payments, enrolmentService);
            Assert.That(nuovo!.Select(d => d.AccountId), Is.EqualTo(new long[] { 1, 2 }));

            await job.Process(processor, reports, payments, enrolmentService, new HashSet<long> { 2 });
            Assert.That(nuovo!.Select(d => d.AccountId), Is.EqualTo(new long[] { 1 }));
        }
    }
}
