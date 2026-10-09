using System.Net;
using System.Reflection;
using System.Text;
using Ranalo.Calculator.Logic.Models;
using Ranalo.DataStore;
using Ranalo.Models;
using Ranalo.PayTrigger;
using Ranalo.PayTrigger.Models;
using Ranalo.Services.DeviceLock;
using Ranalo.SumsungKnox;
using Ranalo.SumsungKnox.Models;
using Ranalo.Woocommece.Api.DataStore;
using Ranalo.Woocommece.Api.Models;
using Device = Ranalo.Woocommece.Api.Models.Device;

namespace Ranolo.Web.Tests
{
    // In-memory stand-ins for the device-lock tests. The removal-task fake
    // mirrors the conditional UPDATEs in DeviceRemovalTaskRepository so the
    // service's double-processing protection is exercised.

    public sealed class FakeTime : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 6, 9, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    public sealed class FakeEnrolmentRepository : IEnrolmentRepository
    {
        public List<Enrolment> Items { get; } = new();
        public int UpdateCount { get; private set; }

        public Task<Enrolment> CreateEnrolmentAsync(Enrolment newEnrolment) { Items.Add(newEnrolment); return Task.FromResult(newEnrolment); }
        public Task<IEnumerable<Enrolment>> GetAllEnrolmentsAsync() => Task.FromResult<IEnumerable<Enrolment>>(Items);
        public Task<Enrolment?> GetByAccountIdAsync(long accountId) => Task.FromResult(Items.FirstOrDefault(e => e.AccountId == accountId));
        public Task<Enrolment> GetByEnrolmentIdAsync(Guid enrolmentId) => Task.FromResult(Items.FirstOrDefault(e => e.Id == enrolmentId)!);
        public Task<Enrolment?> GetByImeiNumberAsync(string imei) => Task.FromResult(Items.FirstOrDefault(e => e.IMEI == imei));
        public Task<IEnumerable<Enrolment>> GetEnrolmentsByDealerIdAsync(int dealerId) => Task.FromResult(Items.Where(e => e.DealerId == dealerId));
        public Task SaveAsync() => Task.CompletedTask;

        public Task<Enrolment> UpdateEnrolmentAsync(Enrolment updateEnrolment)
        {
            UpdateCount++;
            if (!Items.Contains(updateEnrolment)) Items.Add(updateEnrolment);
            return Task.FromResult(updateEnrolment);
        }

        public Task<Enrolment> UpdateEnrolmentPasswordAsync(int userId, string newPasswordHash) => throw new NotImplementedException();
        public Task<(IEnumerable<Enrolment> Items, int TotalCount)> GetDealerEnrolmentsAsync(int dealerId, int pageNumber, int pageSize, string? searchTerm = null) => throw new NotImplementedException();
        public Task<(IEnumerable<Enrolment> Items, int TotalCount)> GetAllEnrolmentsAsync(int pageNumber, int pageSize, string? searchTerm = null) => throw new NotImplementedException();
        public Task<bool> DeleteEnrolmentAsync(Enrolment enrolment) => throw new NotImplementedException();
    }

    public sealed class FakeDevices : IKosePaymentsRepository
    {
        public Dictionary<long, Device> ById { get; } = new();
        public List<Device> Saved { get; } = new();
        public List<Device> Updated { get; } = new();

        public Task<Device?> GetDeviceByAccountId(long accountId) => Task.FromResult(ById.GetValueOrDefault(accountId));
        public Task<Device?> GetDeviceByImeiAsync(string imei) => Task.FromResult(ById.Values.FirstOrDefault(d => d.ImeiNo == imei));
        public Task SaveDeviceToDatabaseAsync(Device device) { Saved.Add(device); ById.TryAdd(device.Id, device); return Task.CompletedTask; }
        public Task UpdateDeviceToDatabaseAsync(Device device) { Updated.Add(device); ById[device.Id] = device; return Task.CompletedTask; }

        public Task<IEnumerable<MpesaRecord>> GetAllAsync() => throw new NotImplementedException();
        public Task<MpesaRecord?> GetByIdAsync(int id) => throw new NotImplementedException();
        public Task<int> InsertAsync(MpesaRecord record) => throw new NotImplementedException();
        public Task<List<string>> SaveToDatabaseAsync(Dictionary<string, List<MpesaRecord>> groupedRecords) => throw new NotImplementedException();
        public Task SaveDevicesToDatabaseAsync(List<Device> groupedRecords) => throw new NotImplementedException();
        public Task UpdateDevicesToDatabaseAsync(List<Device> groupedRecords) => throw new NotImplementedException();
        public Task<int> AddContractAsync(ContractInfo contract) => throw new NotImplementedException();
        public Task UpdateOrderContract(long orderId, int contractId) => throw new NotImplementedException();
    }

    public sealed class FakePayTrigger : IPayTriggerClient
    {
        public List<(List<ImeiEnrollItem> Items, bool PreLock)> PreEnrolCalls { get; } = new();
        public List<UpdateRepayInfoRequest> RepayCalls { get; } = new();
        public List<RemoveLockRequest> RemoveCalls { get; } = new();

        public PreEnrollImeiResponse PreEnrolResponse { get; set; } = new() { Code = 200, Message = "Success" };
        public PayTriggerApiResponse RepayResponse { get; set; } = new() { Code = 200, Message = "Success" };
        public PayTriggerApiResponse RemoveResponse { get; set; } = new() { Code = 200, Message = "Success" };
        public Exception? Throw { get; set; }

        public Task<PreEnrollImeiResponse> PreEnrollImeiAsync(List<ImeiEnrollItem> items, bool preLockFlag)
        {
            if (Throw != null) throw Throw;
            PreEnrolCalls.Add((items, preLockFlag));
            return Task.FromResult(PreEnrolResponse);
        }

        // Answers in order when set, then falls back to RepayResponse.
        public Queue<PayTriggerApiResponse> RepayResponses { get; } = new();

        public Task<PayTriggerApiResponse> UpdateRepayInfoAsync(UpdateRepayInfoRequest request)
        {
            if (Throw != null) throw Throw;
            RepayCalls.Add(request);
            return Task.FromResult(RepayResponses.Count > 0 ? RepayResponses.Dequeue() : RepayResponse);
        }

        public Task<PayTriggerApiResponse> RemoveLockAsync(RemoveLockRequest request)
        {
            if (Throw != null) throw Throw;
            RemoveCalls.Add(request);
            return Task.FromResult(RemoveResponse);
        }

        public Task<FindLockStateResponse> FindLockStateAsync(FindLockStateRequest request) => throw new NotImplementedException();
        public Task<PayTriggerApiResponse> SendPushInfoAsync(SendPushInfoRequest request) => throw new NotImplementedException();
    }

    public sealed class FakeKnox : IKnoxGuardClient
    {
        public List<CompleteDeviceRequest> CompleteCalls { get; } = new();
        public List<DeviceActionsRequest> ActionCalls { get; } = new();
        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
        public string Body { get; set; } = "{\"result\":\"SUCCESS\"}";

        private HttpResponseMessage Response() => new(Status) { Content = new StringContent(Body, Encoding.UTF8, "application/json") };

        public Task<HttpResponseMessage> CompleteDeviceAsync(CompleteDeviceRequest request) { CompleteCalls.Add(request); return Task.FromResult(Response()); }
        public Task<HttpResponseMessage> ExecuteDeviceActionsAsync(DeviceActionsRequest request) { ActionCalls.Add(request); return Task.FromResult(Response()); }

        public Task<HttpResponseMessage> ApproveDeviceAsync(ApproveDeviceRequest request) => throw new NotImplementedException();
        public Task<HttpResponseMessage> SetBlinkingReminderAsync(SetBlinkingReminderRequest request) => throw new NotImplementedException();
        public Task<HttpResponseMessage> DeleteDeviceAsync(DeleteDeviceRequest request) => throw new NotImplementedException();
        public Task<HttpResponseMessage> LockDeviceAsync(LockDeviceRequest request) => throw new NotImplementedException();
        public Task<HttpResponseMessage> SendMessageAsync(SendMessageRequest request) => throw new NotImplementedException();
        public Task<HttpResponseMessage> GetDeviceInfoAsync(string deviceId) => throw new NotImplementedException();
        public Task<ListDevicesResponse> ListDevicesAsync(ListDevicesRequest request) => throw new NotImplementedException();
        public Task<HttpResponseMessage> UnlockDeviceAsync(UnlockDeviceRequest request) => throw new NotImplementedException();
    }

    public sealed class FakeDeviceLockRepository : IDeviceLockRepository
    {
        public List<BrandKeyword> Keywords { get; } = new();
        public Dictionary<string, string> Tacs { get; } = new();
        public List<DepositPayment> Payments { get; } = new();
        public Dictionary<long, List<string>> Products { get; } = new();
        public Dictionary<long, decimal?> DailySalePrices { get; } = new();
        public Dictionary<string, int> Dealers { get; } = new();
        public Dictionary<long, string> LiveDevices { get; } = new();

        public Task<string?> GetLiveDeviceImeiAsync(long accountId) => Task.FromResult(LiveDevices.GetValueOrDefault(accountId));

        public Task<List<BrandKeyword>> GetBrandKeywordsAsync() => Task.FromResult(Keywords.ToList());
        public Task<string?> GetTacBrandAsync(string tac) => Task.FromResult(Tacs.GetValueOrDefault(tac));

        public Task<string?> RecordTacAsync(string tac, string brand, string source, string? imei)
        {
            if (!Tacs.TryGetValue(tac, out var stored))
            {
                Tacs[tac] = brand;
                return Task.FromResult<string?>(null);
            }
            return Task.FromResult<string?>(string.Equals(stored, brand, StringComparison.OrdinalIgnoreCase) ? null : stored);
        }

        public Task<List<DepositPayment>> GetPaymentsByMpesaCodeAsync(string mpesaCode) =>
            Task.FromResult(Payments.Where(p => p.MpesaCode == mpesaCode.Trim()).ToList());

        public Task<List<string>> GetOrderProductNamesAsync(long orderId) =>
            Task.FromResult(Products.GetValueOrDefault(orderId) ?? new List<string>());

        public Task<decimal?> GetOrderDailySalePriceAsync(long orderId) => Task.FromResult(DailySalePrices.GetValueOrDefault(orderId));

        public Task<int?> GetDealerIdByReferenceAsync(string dealerReference) =>
            Task.FromResult(Dealers.TryGetValue(dealerReference.Trim(), out var id) ? id : (int?)null);
    }

    public sealed class FakeRemovalTasks : IDeviceRemovalTaskRepository
    {
        private static readonly string[] Live = { "Pending", "Processing", "Failed", "Completed" };
        public List<DeviceRemovalTask> Items { get; } = new();
        public List<DeviceRemovalTask> FullyPaidWithoutTask { get; } = new();
        private int _nextId = 1;

        public Task<bool> EnqueueAsync(DeviceRemovalTask task)
        {
            if (Items.Any(t => t.AccountId == task.AccountId && Live.Contains(t.Status)))
                return Task.FromResult(false);

            Items.Add(new DeviceRemovalTask
            {
                Id = _nextId++, AccountId = task.AccountId, Imei = task.Imei, CustomerName = task.CustomerName,
                LockGroup = task.LockGroup, Provider = task.Provider, Reason = task.Reason, Status = "Pending"
            });
            return Task.FromResult(true);
        }

        public Task<DeviceRemovalTask?> GetAsync(int id) => Task.FromResult(Items.FirstOrDefault(t => t.Id == id));
        public Task<(List<DeviceRemovalTask> Items, int TotalCount)> ListAsync(string status, int page, int pageSize) => Task.FromResult((Items.ToList(), Items.Count));
        public Task<int> CountAwaitingDecisionAsync() => Task.FromResult(Items.Count(t => t.Status is "Pending" or "Failed"));

        public Task<bool> TryClaimAsync(int id, int userId, DateTime nowUtc)
        {
            var t = Items.FirstOrDefault(x => x.Id == id);
            var stale = t?.Status == "Processing" && t.LastAttemptAtUtc < nowUtc.AddMinutes(-DeviceRemovalTaskRepository.StaleProcessingMinutes);
            if (t == null || !(t.Status is "Pending" or "Failed" || stale)) return Task.FromResult(false);
            t.Status = "Processing"; t.DecidedByUserId = userId; t.DecidedAtUtc = nowUtc; t.AttemptCount++; t.LastAttemptAtUtc = nowUtc;
            return Task.FromResult(true);
        }

        public Task MarkCompletedAsync(int id, string? response, DateTime nowUtc)
        {
            var t = Items.First(x => x.Id == id);
            if (t.Status == "Processing") { t.Status = "Completed"; t.LastResponse = response; t.CompletedAtUtc = nowUtc; }
            return Task.CompletedTask;
        }

        public Task MarkFailedAsync(int id, string? response, DateTime nowUtc)
        {
            var t = Items.First(x => x.Id == id);
            if (t.Status == "Processing") { t.Status = "Failed"; t.LastResponse = response; }
            return Task.CompletedTask;
        }

        public Task<bool> RejectAsync(int id, int userId, string? note, DateTime nowUtc)
        {
            var t = Items.FirstOrDefault(x => x.Id == id);
            if (t == null || !(t.Status is "Pending" or "Failed")) return Task.FromResult(false);
            t.Status = "Rejected"; t.DecidedByUserId = userId; t.DecidedAtUtc = nowUtc; t.DecisionNote = note;
            return Task.FromResult(true);
        }

        public Task<bool> ReopenRejectedAsync(int id)
        {
            var t = Items.FirstOrDefault(x => x.Id == id);
            if (t == null || t.Status != "Rejected") return Task.FromResult(false);
            if (Items.Any(o => o.AccountId == t.AccountId && o.Id != t.Id && Live.Contains(o.Status))) return Task.FromResult(false);
            t.Status = "Pending";
            return Task.FromResult(true);
        }

        public Task<List<DeviceRemovalTask>> FindFullyPaidWithoutTaskAsync() => Task.FromResult(FullyPaidWithoutTask.ToList());
    }

    // Stand-in for interfaces too large to fake by hand: each call is routed
    // to a handler registered by method name; anything else throws.
    public class InterfaceFake<T> : DispatchProxy where T : class
    {
        private Dictionary<string, Func<object?[]?, object?>> _handlers = new();
        public List<(string Method, object?[]? Args)> Calls { get; } = new();

        public static (T Instance, InterfaceFake<T> Fake) Create(Dictionary<string, Func<object?[]?, object?>> handlers)
        {
            var instance = Create<T, InterfaceFake<T>>();
            var fake = (InterfaceFake<T>)(object)instance;
            fake._handlers = handlers;
            return (instance, fake);
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            Calls.Add((targetMethod!.Name, args));
            if (_handlers.TryGetValue(targetMethod.Name, out var handler)) return handler(args);
            throw new NotImplementedException($"{typeof(T).Name}.{targetMethod.Name} was not expected");
        }
    }
}
