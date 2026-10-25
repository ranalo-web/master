using Ranalo.Woocommece.Api.Services;

namespace Ranalo.Services
{
    // The contract is created when the enrolment is approved (2026-10-19),
    // instead of waiting for ScheduledTaskCreateContractOrders, which still
    // picks up any order that didn't qualify then (deposit code or payment
    // not in yet). Never stops the approval: a failure is logged and left to
    // the scheduled run. The account is then queued (IAccountRefreshQueue)
    // so its lock date is set straight away, and the contract tried again
    // there if it didn't qualify yet.
    public interface IOrderContractService
    {
        Task<bool> TryCreateAsync(long orderId, long accountNo);
    }

    public class OrderContractService : IOrderContractService
    {
        private readonly ISyncService _sync;
        private readonly IAccountRefreshQueue _refresh;
        private readonly ILogger<OrderContractService> _logger;

        public OrderContractService(ISyncService sync, IAccountRefreshQueue refresh, ILogger<OrderContractService> logger)
        {
            _sync = sync;
            _refresh = refresh;
            _logger = logger;
        }

        public async Task<bool> TryCreateAsync(long orderId, long accountNo)
        {
            if (orderId <= 0)
            {
                return false;
            }

            try
            {
                var created = await _sync.CreateContractForOrderAsync(orderId);
                _logger.LogInformation(created
                    ? "Contract created on approval for order {OrderId}"
                    : "Order {OrderId} approved but doesn't qualify for a contract yet; the scheduled run will pick it up", orderId);
                return created;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Couldn't create the contract on approval for order {OrderId}; the scheduled run will retry", orderId);
                return false;
            }
            finally
            {
                _refresh.Enqueue(accountNo);
            }
        }
    }
}
