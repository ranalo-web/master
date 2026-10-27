using System.Globalization;

namespace Ranalo.Services
{
    // One clock for lock dates (2026-10-27). LockTransaction.AutoLockDate is
    // UTC; each locker gets it converted once, here. Until now the jobs used
    // the server's clock (DateTime.Now): Kenya time on the old local server,
    // UTC on Azure. On Azure that sent Nuovo, which reads its date as Kenya
    // time, a time 3 hours early, and the restructured 30h opening (from the
    // M-Pesa payment time, which is Kenya time) went to Knox and Transsion
    // 3 hours late.
    public static class LockTime
    {
        public const string NuovoFormat = "dd/MM/yyyy'T'HH:mm:ss";

        // A date without a kind is taken as UTC: convert Kenya times with
        // KenyaToUtc before they get here.
        public static DateTime AsUtc(DateTime at) => at.Kind switch
        {
            DateTimeKind.Utc => at,
            DateTimeKind.Local => at.ToUniversalTime(),
            _ => DateTime.SpecifyKind(at, DateTimeKind.Utc),
        };

        // M-Pesa payment times and dates typed on our pages are Kenya time.
        public static DateTime KenyaToUtc(DateTime kenyaTime) =>
            DateTime.SpecifyKind(DateTime.SpecifyKind(kenyaTime, DateTimeKind.Unspecified) - CustomerMessages.KenyaOffset, DateTimeKind.Utc);

        public static DateTime? KenyaToUtc(DateTime? kenyaTime) =>
            kenyaTime == null ? null : KenyaToUtc(kenyaTime.Value);

        // Nuovo's auto_lock_date, in Kenya time. DateTime.MaxValue ("never")
        // goes as it is.
        public static string ForNuovo(DateTime lockAt)
        {
            var utc = AsUtc(lockAt);
            var kenya = utc.Year >= 9999 ? utc : utc + CustomerMessages.KenyaOffset;
            return kenya.ToString(NuovoFormat, CultureInfo.InvariantCulture);
        }

        // Knox (milliseconds) and Transsion (seconds) take Unix time.
        public static long UnixMillis(DateTime lockAt) => new DateTimeOffset(AsUtc(lockAt)).ToUnixTimeMilliseconds();

        public static long UnixSeconds(DateTime lockAt) => new DateTimeOffset(AsUtc(lockAt)).ToUnixTimeSeconds();
    }
}
