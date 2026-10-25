using System.Threading.Channels;

namespace Ranalo.Services
{
    // Accounts to bring up to date now rather than at the next scheduled run:
    // a payment has just come in or been assigned, or an enrolment has just
    // been approved. AccountRefreshWorker creates the contract if the order
    // now qualifies and sets the lock date from the payments, so a new
    // customer who pays isn't left locked for up to 2 hours (2026-10-25).
    public interface IAccountRefreshQueue
    {
        void Enqueue(long accountNo);
        void Enqueue(string? accountNo);
        IAsyncEnumerable<long> ReadAllAsync(CancellationToken cancellationToken);
    }

    public class AccountRefreshQueue : IAccountRefreshQueue
    {
        private readonly Channel<long> _accounts = Channel.CreateUnbounded<long>();

        public void Enqueue(long accountNo)
        {
            if (accountNo > 0)
            {
                _accounts.Writer.TryWrite(accountNo);
            }
        }

        // What the customer typed on M-Pesa: anything that isn't an account
        // number is left to the scheduled runs and Assign Payments.
        public void Enqueue(string? accountNo)
        {
            if (long.TryParse(accountNo?.Trim(), out var parsed))
            {
                Enqueue(parsed);
            }
        }

        public IAsyncEnumerable<long> ReadAllAsync(CancellationToken cancellationToken) =>
            _accounts.Reader.ReadAllAsync(cancellationToken);
    }
}
