using System.Collections.Concurrent;

namespace Ranalo.Services
{
    // The lock date each phone was last sent (Nuovo, Knox or Transsion) and
    // when, so a payment receipt can say "unlocked until" once that payment
    // has been counted. Nuovo's date only reaches Devices on a later pull, so
    // Devices can't be used for this. In memory only: after a restart the
    // receipt goes without the date until the lock jobs have run again.
    public static class LockDateBook
    {
        public record Entry(DateTime LockAtUtc, DateTime SentAtUtc);

        private static readonly ConcurrentDictionary<long, Entry> Entries = new();

        // autoLockDate is on the server's clock (DateTime.Now), as the lock
        // jobs build it. Fully paid (far future) and unset dates are skipped.
        public static void Record(long accountId, DateTime autoLockDate, DateTime? sentAtUtc = null)
        {
            if (autoLockDate == DateTime.MinValue || autoLockDate.Year >= 9999)
            {
                return;
            }

            var lockAtUtc = autoLockDate.Kind == DateTimeKind.Utc ? autoLockDate : autoLockDate.ToUniversalTime();
            Entries[accountId] = new Entry(lockAtUtc, sentAtUtc ?? DateTime.UtcNow);
        }

        public static Entry? Get(long accountId) => Entries.TryGetValue(accountId, out var entry) ? entry : null;

        public static void Clear() => Entries.Clear();
    }

    // When a payment receipt goes out, and whether it can say how long the
    // phone stays unlocked.
    public static class PaymentReceipt
    {
        // How long a receipt waits for the lock jobs to count the payment.
        // The refresh worker usually does it within a minute.
        public static readonly TimeSpan MaxWait = TimeSpan.FromMinutes(15);

        public enum Timing { Wait, Send }

        public static (Timing Timing, DateTime? UnlockedUntilUtc) Decide(DateTime? receivedAtUtc, LockDateBook.Entry? lastLock, DateTime nowUtc)
        {
            if (receivedAtUtc != null && lastLock != null && lastLock.SentAtUtc >= receivedAtUtc)
            {
                // Still behind after paying: the date is past, so say nothing.
                return (Timing.Send, lastLock.LockAtUtc > nowUtc ? lastLock.LockAtUtc : null);
            }

            if (receivedAtUtc != null && nowUtc - receivedAtUtc < MaxWait)
            {
                return (Timing.Wait, null);
            }

            return (Timing.Send, null);
        }
    }
}
