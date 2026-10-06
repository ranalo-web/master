using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Ranalo.Calculator.Logic.Models;
using Ranalo.Controllers;
using Ranalo.PayTrigger;
using Ranalo.PayTrigger.Models;
using Ranalo.Woocommece.Api.DataStore;
using Ranalo.Woocommece.Api.Models;

namespace Ranolo.Web.Tests
{
    public class PayTriggerSignerTests
    {
        [Test]
        public void Sign_MatchesIndependentOpenSslReference()
        {
            // printf 'apiKey=testkey&imei=123456789012345' | openssl dgst -sha256 -hmac testkey -hex
            //   -> uppercase hex -> base64
            var sign = PayTriggerSigner.Sign(new Dictionary<string, string?>
            {
                ["imei"] = "123456789012345",
                ["apiKey"] = "testkey"
            }, "testkey");

            Assert.That(sign, Is.EqualTo("Qzc4OEI5RkRFMTlBMUI1Qjk3RUFDQzU5NzNCQUQ2MDYwOEQwOTA3NDA3RkNGQzdCODM5MkU4MDAyQUFCRDlDNA=="));
        }

        [Test]
        public void BuildSignContent_SortsOrdinalAndSkipsEmpty()
        {
            var content = PayTriggerSigner.BuildSignContent(new Dictionary<string, string?>
            {
                ["imei"] = "1",
                ["Zeta"] = "z",
                ["apiKey"] = "k",
                ["deviceTag"] = "",
                ["orderNum"] = null
            });

            Assert.That(content, Is.EqualTo("Zeta=z&apiKey=k&imei=1"));
        }

        [Test]
        public void ExtractFieldsFromJson_RendersBooleansAndNumbersAsJsonText()
        {
            var content = PayTriggerSigner.BuildSignContent(
                PayTriggerSigner.ExtractFieldsFromJson("{\"preLockFlag\":false,\"amount\":12.50,\"tag\":null,\"s\":\"x\"}"));

            Assert.That(content, Is.EqualTo("amount=12.50&preLockFlag=false&s=x"));
        }

        [Test]
        public void VerifyJson_AcceptsOwnSignature_RejectsTamperedOrMissing()
        {
            const string json = "{\"imei\":\"123\",\"mobileStatus\":1000}";
            var sign = PayTriggerSigner.SignJson(json, "key");

            Assert.Multiple(() =>
            {
                Assert.That(PayTriggerSigner.VerifyJson(json, "key", sign), Is.True);
                Assert.That(PayTriggerSigner.VerifyJson(json.Replace("1000", "2000"), "key", sign), Is.False);
                Assert.That(PayTriggerSigner.VerifyJson(json, "otherkey", sign), Is.False);
                Assert.That(PayTriggerSigner.VerifyJson(json, "key", null), Is.False);
            });
        }
    }

    public class PayTriggerClientTests
    {
        private sealed class CapturingHandler : HttpMessageHandler
        {
            public HttpRequestMessage? Request;
            public string? Body;
            public string ResponseJson = "{\"code\":200,\"message\":\"Success\"}";

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Request = request;
                Body = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(ResponseJson, Encoding.UTF8, "application/json")
                };
            }
        }

        private static (PayTriggerClient client, CapturingHandler handler) CreateClient()
        {
            var handler = new CapturingHandler();
            var http = new HttpClient(handler) { BaseAddress = new Uri("https://paytrigger.example/PayTrigger/") };
            var client = new PayTriggerClient(http, Options.Create(new PayTriggerSettings
            {
                BaseUrl = "https://paytrigger.example/PayTrigger/",
                ApiKey = "secret"
            }));
            return (client, handler);
        }

        [Test]
        public async Task FindLockState_PostsToCorrectPath_WithValidSignHeader()
        {
            var (client, handler) = CreateClient();
            handler.ResponseJson = "{\"code\":200,\"message\":\"Success\",\"data\":{\"imei\":\"123\",\"lockState\":3000,\"mobileStatus\":2000}}";

            var result = await client.FindLockStateAsync(new FindLockStateRequest { Imei = "123" });

            Assert.Multiple(() =>
            {
                Assert.That(handler.Request!.RequestUri!.ToString(),
                    Is.EqualTo("https://paytrigger.example/PayTrigger/api/partner/lock/v1/findLockState"));
                Assert.That(handler.Body, Does.Contain("\"apiKey\":\"secret\""));
                Assert.That(handler.Body, Does.Not.Contain("deviceTag"), "null fields must be omitted");
                var sign = handler.Request.Headers.GetValues("sign").Single();
                Assert.That(PayTriggerSigner.VerifyJson(handler.Body!, "secret", sign), Is.True);
                Assert.That(result.IsSuccess, Is.True);
                Assert.That(result.Data!.LockState, Is.EqualTo(3000));
            });
        }

        [Test]
        public async Task PreEnrollImei_SendsImeiInfoAsJsonString()
        {
            var (client, handler) = CreateClient();
            handler.ResponseJson = "{\"code\":200,\"message\":\"Success\"}";

            await client.PreEnrollImeiAsync(new List<ImeiEnrollItem> { new() { Imei = "111" } }, preLockFlag: true);

            using var doc = JsonDocument.Parse(handler.Body!);
            Assert.Multiple(() =>
            {
                Assert.That(handler.Request!.RequestUri!.AbsolutePath, Does.EndWith("api/partner/lock/v1/imei/input"));
                Assert.That(doc.RootElement.GetProperty("imeiInfo").ValueKind, Is.EqualTo(JsonValueKind.String));
                Assert.That(doc.RootElement.GetProperty("imeiInfo").GetString(), Does.Contain("\"imei\":\"111\""));
                Assert.That(doc.RootElement.GetProperty("preLockFlag").GetBoolean(), Is.True);
            });
        }

        [Test]
        public async Task UpdateRepayInfo_SetsRelatedMerchantToApiKey()
        {
            var (client, handler) = CreateClient();

            await client.UpdateRepayInfoAsync(new UpdateRepayInfoRequest());

            Assert.That(handler.Body, Does.Contain("\"relatedMerchant\":\"secret\""));
        }
    }

    public class PayTriggerWebhookControllerTests
    {
        private sealed class FakeRepo : IKosePaymentsRepository
        {
            public Device? Device;
            public Device? Updated;

            public Task<Device?> GetDeviceByImeiAsync(string imei) => Task.FromResult(Device?.ImeiNo == imei ? Device : null);
            public Task UpdateDeviceToDatabaseAsync(Device device) { Updated = device; return Task.CompletedTask; }

            public Task<IEnumerable<MpesaRecord>> GetAllAsync() => throw new NotImplementedException();
            public Task<MpesaRecord?> GetByIdAsync(int id) => throw new NotImplementedException();
            public Task<int> InsertAsync(MpesaRecord record) => throw new NotImplementedException();
            public Task<List<string>> SaveToDatabaseAsync(Dictionary<string, List<MpesaRecord>> groupedRecords) => throw new NotImplementedException();
            public Task SaveDevicesToDatabaseAsync(List<Device> groupedRecords) => throw new NotImplementedException();
            public Task UpdateDevicesToDatabaseAsync(List<Device> groupedRecords) => throw new NotImplementedException();
            public Task<int> AddContractAsync(ContractInfo contract) => throw new NotImplementedException();
            public Task UpdateOrderContract(long orderId, int contractId) => throw new NotImplementedException();
            public Task SaveDeviceToDatabaseAsync(Device device) => throw new NotImplementedException();
            public Task<Device?> GetDeviceByAccountId(long accountId) => throw new NotImplementedException();
        }

        private sealed class FakeWorkflow : Ranalo.Services.DeviceLock.ITranssionEnrolmentWorkflow
        {
            public List<string> Activated { get; } = new();
            public Exception? Throw;

            public Task OnActivatedAsync(string imei)
            {
                if (Throw != null) throw Throw;
                Activated.Add(imei);
                return Task.CompletedTask;
            }

            public Task<Ranalo.Models.Enrolment> PreEnrolAsync(Ranalo.Models.Enrolment enrolment, int? deviceGroupId) => throw new NotImplementedException();
            public Task<Ranalo.Services.DeviceLock.WorkflowResult> ApproveCallAsync(Guid enrolmentId, Ranalo.DataStore.DataModels.User actor) => throw new NotImplementedException();
            public Task<Ranalo.Services.DeviceLock.WorkflowResult> RetryUnlockAsync(Guid enrolmentId, Ranalo.DataStore.DataModels.User actor) => throw new NotImplementedException();
        }

        private static PayTriggerWebhookController CreateController(FakeRepo repo, string body, string? sign, FakeWorkflow? workflow = null)
        {
            var controller = new PayTriggerWebhookController(
                repo,
                workflow ?? new FakeWorkflow(),
                Options.Create(new PayTriggerSettings { ApiKey = "secret", BaseUrl = "https://x/" }),
                NullLogger<PayTriggerWebhookController>.Instance);

            var context = new DefaultHttpContext();
            context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
            if (sign != null)
            {
                context.Request.Headers["sign"] = sign;
            }
            controller.ControllerContext = new ControllerContext { HttpContext = context };
            return controller;
        }

        private static int? StatusOf(IActionResult result) => (result as ObjectResult)?.StatusCode;

        [Test]
        public async Task BadSignature_Returns401_AndDoesNotTouchDevice()
        {
            var repo = new FakeRepo { Device = new Device { ImeiNo = "123" } };
            const string body = "{\"imei\":\"123\",\"mobileStatus\":1000,\"notifyType\":1000}";

            var result = await CreateController(repo, body, "bogus").DeviceStatusCallback();

            Assert.That(StatusOf(result), Is.EqualTo(401));
            Assert.That(repo.Updated, Is.Null);
        }

        [Test]
        public async Task ValidLockCallback_LocksDevice_AndAcks()
        {
            var repo = new FakeRepo { Device = new Device { ImeiNo = "123" } };
            const string body = "{\"imei\":\"123\",\"mobileStatus\":1000,\"state\":3000,\"notifyType\":1000}";

            var result = await CreateController(repo, body, PayTriggerSigner.SignJson(body, "secret")).DeviceStatusCallback();

            Assert.Multiple(() =>
            {
                Assert.That(StatusOf(result), Is.EqualTo(200));
                Assert.That(JsonSerializer.Serialize(((ObjectResult)result).Value), Is.EqualTo("{\"code\":200,\"message\":\"Success\"}"));
                Assert.That(repo.Updated, Is.Not.Null);
                Assert.That(repo.Updated!.Locked, Is.True);
                Assert.That(repo.Updated.LockType, Is.EqualTo("complete"));
                Assert.That(repo.Updated.IsActivated, Is.True);
                Assert.That(repo.Updated.EnrollmentStatus, Is.EqualTo("Completed"));
            });
        }

        [Test]
        public async Task ActivationCallback_CompletesEnrolment()
        {
            var repo = new FakeRepo { Device = new Device { ImeiNo = "123", Status = "pending_activation" } };
            var workflow = new FakeWorkflow();
            const string body = "{\"imei\":\"123\",\"mobileStatus\":1000,\"state\":3000,\"notifyType\":1000}";

            var result = await CreateController(repo, body, PayTriggerSigner.SignJson(body, "secret"), workflow).DeviceStatusCallback();

            Assert.That(StatusOf(result), Is.EqualTo(200));
            Assert.That(workflow.Activated, Is.EqualTo(new[] { "123" }));
            Assert.That(repo.Updated!.Status, Is.EqualTo("pending_activation"), "webhook doesn't move it into the lock jobs");
        }

        [TestCase("{\"imei\":\"123\",\"mobileStatus\":1000,\"state\":1000,\"notifyType\":1000}")]
        [TestCase("{\"imei\":\"123\",\"mobileStatus\":2000,\"state\":5000,\"notifyType\":2000}")]
        [TestCase("{\"imei\":\"123\",\"mobileStatus\":1000,\"state\":3000,\"notifyType\":2000}")]
        [TestCase("{\"imei\":\"123\",\"notifyType\":4000,\"tip\":\"blocked\"}")]
        public async Task NonActivationCallbacks_DontCompleteEnrolment(string body)
        {
            var repo = new FakeRepo { Device = new Device { ImeiNo = "123" } };
            var workflow = new FakeWorkflow();

            await CreateController(repo, body, PayTriggerSigner.SignJson(body, "secret"), workflow).DeviceStatusCallback();

            Assert.That(workflow.Activated, Is.Empty);
        }

        [Test]
        public async Task ActivationCallback_WorkflowFails_Returns500SoPayTriggerRetries()
        {
            var repo = new FakeRepo { Device = new Device { ImeiNo = "123" } };
            var workflow = new FakeWorkflow { Throw = new InvalidOperationException("db down") };
            const string body = "{\"imei\":\"123\",\"mobileStatus\":1000,\"state\":3000,\"notifyType\":1000}";

            var result = await CreateController(repo, body, PayTriggerSigner.SignJson(body, "secret"), workflow).DeviceStatusCallback();

            Assert.That(StatusOf(result), Is.EqualTo(500));
        }

        [Test]
        public async Task RemovalCallback_MarksDeviceRemoved()
        {
            var repo = new FakeRepo { Device = new Device { ImeiNo = "123", Status = "enrolled" } };
            const string body = "{\"imei\":\"123\",\"mobileStatus\":2000,\"state\":5000,\"notifyType\":2000}";

            await CreateController(repo, body, PayTriggerSigner.SignJson(body, "secret")).DeviceStatusCallback();

            Assert.Multiple(() =>
            {
                Assert.That(repo.Updated!.Locked, Is.False);
                Assert.That(repo.Updated.Status, Is.EqualTo("enrolled"), "stays in reports");
                Assert.That(repo.Updated.EnrollmentStatus, Is.EqualTo("Removed"));
            });
        }

        [Test]
        public async Task UnknownImei_AcksWithoutUpdating()
        {
            var repo = new FakeRepo();
            const string body = "{\"imei\":\"999\",\"mobileStatus\":1000,\"notifyType\":1000}";

            var result = await CreateController(repo, body, PayTriggerSigner.SignJson(body, "secret")).DeviceStatusCallback();

            Assert.That(StatusOf(result), Is.EqualTo(200));
            Assert.That(repo.Updated, Is.Null);
        }
    }

    // Hits the real PayTrigger API with a read-only findLockState lookup on a
    // dummy IMEI, to confirm signing, apiKey and IP whitelist are accepted.
    // Run with: PAYTRIGGER_APIKEY=... dotnet test --filter Category=Live
    [Explicit, Category("Live")]
    public class PayTriggerLiveTests
    {
        [Test]
        public async Task FindLockState_RealApi_AcceptsSignature()
        {
            var apiKey = Environment.GetEnvironmentVariable("PAYTRIGGER_APIKEY");
            if (string.IsNullOrEmpty(apiKey))
            {
                Assert.Ignore("PAYTRIGGER_APIKEY not set.");
            }
            var baseUrl = Environment.GetEnvironmentVariable("PAYTRIGGER_BASEURL") ?? "https://paytrigger.transsion-os.com/PayTrigger/";

            var http = new HttpClient { BaseAddress = new Uri(baseUrl) };
            var client = new PayTriggerClient(http, Options.Create(new PayTriggerSettings { BaseUrl = baseUrl, ApiKey = apiKey! }));

            var result = await client.FindLockStateAsync(new FindLockStateRequest { Imei = "000000000000000" });

            TestContext.Out.WriteLine($"code={result.Code} message={result.Message}");
            Assert.That(result.Code, Is.Not.EqualTo(40000), "Sign error -- signing algorithm rejected");
            Assert.That(result.Code, Is.Not.EqualTo(20003), "apiKey invalid/expired");
            Assert.That(result.Code, Is.Not.AnyOf(40001, 40003), "server IP not whitelisted");
        }

        // Finds out which fully-paid lock date Transsion accepts. USE A TEST
        // PHONE: it unlocks that phone until the accepted date.
        // Run with: PAYTRIGGER_APIKEY=... PAYTRIGGER_TEST_IMEI=... dotnet test --filter Category=LiveWrite
        [Test, Explicit, Category("LiveWrite")]
        public async Task UpdateRepayInfo_RealApi_WhichFullyPaidYearIsAccepted()
        {
            var apiKey = Environment.GetEnvironmentVariable("PAYTRIGGER_APIKEY");
            var imei = Environment.GetEnvironmentVariable("PAYTRIGGER_TEST_IMEI");
            if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(imei))
            {
                Assert.Ignore("PAYTRIGGER_APIKEY and PAYTRIGGER_TEST_IMEI must be set.");
            }
            var baseUrl = Environment.GetEnvironmentVariable("PAYTRIGGER_BASEURL") ?? "https://paytrigger.transsion-os.com/PayTrigger/";
            var client = new PayTriggerClient(new HttpClient { BaseAddress = new Uri(baseUrl) },
                Options.Create(new PayTriggerSettings { BaseUrl = baseUrl, ApiKey = apiKey! }));

            foreach (var year in new[] { 9999, 2099 })
            {
                var result = await client.UpdateRepayInfoAsync(new UpdateRepayInfoRequest
                {
                    Imei = imei,
                    NextRepayTime = new DateTimeOffset(year, 12, 31, 23, 59, 59, TimeSpan.Zero).ToUnixTimeSeconds(),
                    Description = "Fully paid test"
                });
                TestContext.Out.WriteLine($"{year}: code={result.Code} message={result.Message}");
                if (result.IsSuccess)
                {
                    Assert.Pass($"Transsion accepted 31/12/{year}.");
                }
            }

            Assert.Fail("Transsion refused both 9999 and 2099 -- see output.");
        }
    }
}
