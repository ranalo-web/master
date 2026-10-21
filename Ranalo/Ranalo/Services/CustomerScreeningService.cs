using Microsoft.Extensions.Caching.Memory;
using Ranalo.DataStore;
using Ranalo.Models;

namespace Ranalo.Services
{
    public interface ICustomerScreeningService
    {
        Task<OrderScreening> GetOrderScreeningAsync(long orderId, DateTime orderCreated);
        Task<string?> CannotApproveAsync(long orderId, DateTime orderCreated);
        Task<string?> CannotEnrolAsync(string? orderStatus, DateTime orderCreated);
        Task LogScreeningCallAsync(ScreeningCall call);
        Task<List<WelfareCall>> GetWelfareCallsAsync(long accountNo);
        Task LogWelfareCallAsync(WelfareCall call);
        Task<CustomerCallsViewModel> GetCustomerCallsAsync();
        Task<int> GetBadgeCountAsync();
    }

    // Rules in ScreeningRules; everything here is worked out live.
    public class CustomerScreeningService : ICustomerScreeningService
    {
        private readonly ICustomerScreeningRepository _repository;
        private readonly IApplicationReportService _reports;
        private readonly TimeProvider _time;
        private readonly IMemoryCache _cache;

        // The not-paying count needs every account's arrears (about 1.5s), too
        // slow for the menu on every page, so the badge reuses it for this long.
        // The Customer Calls page itself is always live.
        public static readonly TimeSpan NotPayingCountCacheFor = TimeSpan.FromMinutes(10);
        private const string NotPayingCountKey = "CustomerCalls.NotPayingCount";

        public CustomerScreeningService(ICustomerScreeningRepository repository, IApplicationReportService reports, TimeProvider time, IMemoryCache cache)
        {
            _repository = repository;
            _reports = reports;
            _time = time;
            _cache = cache;
        }

        // Orders to screen and welfare calls due (live, cheap) plus customers
        // not paying in their first month (worked out at most every 10 minutes).
        public async Task<int> GetBadgeCountAsync()
        {
            var notPaying = await _cache.GetOrCreateAsync(NotPayingCountKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = NotPayingCountCacheFor;
                return (await GetNotPayingAsync()).Count;
            });

            var start = await _repository.GetScreeningStartAsync();
            if (start == null)
            {
                return notPaying;
            }

            return notPaying
                + await _repository.CountOrdersToScreenAsync(start.Value)
                + await _repository.CountWelfareCallsDueAsync(start.Value, ScreeningRules.WelfareDueDay);
        }

        public async Task<OrderScreening> GetOrderScreeningAsync(long orderId, DateTime orderCreated)
        {
            var required = ScreeningRules.RequiresScreening(orderCreated, await _repository.GetScreeningStartAsync());
            var calls = required ? await _repository.GetScreeningCallsAsync(orderId) : new List<ScreeningCall>();

            return new OrderScreening
            {
                OrderId = orderId,
                Status = ScreeningRules.Status(required, calls),
                CustomerPassed = calls.Any(ScreeningRules.CustomerCallPassed),
                NextOfKinPassed = calls.Any(ScreeningRules.NextOfKinCallPassed),
                Calls = calls
            };
        }

        public async Task<string?> CannotApproveAsync(long orderId, DateTime orderCreated) =>
            ScreeningRules.CannotApprove((await GetOrderScreeningAsync(orderId, orderCreated)).Status);

        public async Task<string?> CannotEnrolAsync(string? orderStatus, DateTime orderCreated) =>
            ScreeningRules.CannotEnrol(orderStatus,
                ScreeningRules.RequiresScreening(orderCreated, await _repository.GetScreeningStartAsync()));

        public Task LogScreeningCallAsync(ScreeningCall call) => _repository.AddScreeningCallAsync(call);

        public Task<List<WelfareCall>> GetWelfareCallsAsync(long accountNo) => _repository.GetWelfareCallsAsync(accountNo);

        public Task LogWelfareCallAsync(WelfareCall call) => _repository.AddWelfareCallAsync(call);

        public async Task<CustomerCallsViewModel> GetCustomerCallsAsync()
        {
            var nowUtc = _time.GetUtcNow().UtcDateTime;
            var model = new CustomerCallsViewModel { NotPaying = await GetNotPayingAsync() };
            _cache.Set(NotPayingCountKey, model.NotPaying.Count, NotPayingCountCacheFor);

            var start = await _repository.GetScreeningStartAsync();
            if (start == null)
            {
                return model;
            }

            // Orders to screen, oldest first.
            var orders = await _repository.GetOrdersAwaitingApprovalAsync(start.Value);
            var screeningCalls = (await _repository.GetScreeningCallsForOrdersAsync(orders.Select(o => o.OrderId)))
                .ToLookup(c => c.OrderId);
            foreach (var order in orders)
            {
                var calls = screeningCalls[order.OrderId].ToList();
                order.Attempts = calls.Count;
                order.CustomerPassed = calls.Any(ScreeningRules.CustomerCallPassed);
                order.NextOfKinPassed = calls.Any(ScreeningRules.NextOfKinCallPassed);
                order.LastCallUtc = calls.Count == 0 ? null : calls.Max(c => c.CalledAtUtc);
            }
            model.ToScreen = orders;

            // Welfare calls due or overdue, overdue first, longest waiting first.
            var enrolled = await _repository.GetEnrolmentsSinceAsync(start.Value);
            var welfareCalls = (await _repository.GetWelfareCallsForAccountsAsync(enrolled.Select(e => e.AccountNo)))
                .ToLookup(c => c.AccountNo);
            foreach (var row in enrolled)
            {
                var calls = welfareCalls[row.AccountNo].ToList();
                row.DaysSinceEnrolment = ScreeningRules.DaysSince(row.EnrolledAtUtc, nowUtc);
                row.Status = ScreeningRules.Welfare(row.EnrolledAtUtc, nowUtc, calls);
                row.Attempts = calls.Count;
                row.LastCallUtc = calls.Count == 0 ? null : calls.Max(c => c.CalledAtUtc);
            }
            model.Welfare = enrolled
                .Where(r => r.Status is WelfareStatus.Due or WelfareStatus.Overdue)
                .OrderByDescending(r => r.Status == WelfareStatus.Overdue)
                .ThenByDescending(r => r.DaysSinceEnrolment)
                .ToList();

            return model;
        }

        // In arrears in the first month of the contract, most behind first.
        private async Task<List<NotPayingRow>> GetNotPayingAsync()
        {
            var accounts = await _reports.GetStatusReportByDealer(null, null, 1, ReportPaging.All, "");
            return (accounts?.StatusReports ?? new List<MobileStatusReport>())
                .Where(a => ScreeningRules.NotPaying(a.FirstPaymentDate, a.Arrears, DateTime.Now))
                .Select(a => new NotPayingRow
                {
                    AccountNo = a.AccountNo,
                    FirstName = a.FirstName,
                    FirstPaymentDate = a.FirstPaymentDate,
                    DaysSinceStart = a.FirstPaymentDate.HasValue ? ScreeningRules.DaysSince(a.FirstPaymentDate.Value, DateTime.Now) : 0,
                    Daily = a.Daily,
                    TotalPaid = a.TotalPaid,
                    Arrears = a.Arrears,
                    LastPaymentDate = a.LastPaymentDate,
                    Locked = a.Locked
                })
                .OrderBy(a => a.Arrears)
                .ToList();
        }
    }
}
