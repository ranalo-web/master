using Ranalo.Models;

namespace Ranalo.Services
{
    // Lock check rules (agreed with the business 2026-10-15). Kept free of
    // I/O so they can be unit tested; see Database/LockCheck/001.
    //
    // On 2026-10-15, of 611 open contracts 599 phones were on Nuovo, and 257
    // of those were unlocked although the customer was 7+ days behind --
    // Nuovo should have locked them (phone offline, or the Nuovo app
    // stripped without Nuovo noticing). Each Nuovo phone with an open
    // contract falls into one stage, checked in this order:
    //   1. Fully paid, contract open: close it -- ending it on the date it
    //      was paid off -- and release the phone (a shilling short counts as
    //      paid -- 7004982, 7098295).
    //   2. Already in Recovery.
    //   3. Lost from Nuovo: Nuovo reports it un_registered and money is
    //      owed (when it's paid off, un_registered is normal: Nuovo releases
    //      paid-off phones).
    //   4. Unlocked and N+ days behind (default 7):
    //      - Samsung, itel/TECNO/Infinix: needs a new locker -> Recovery
    //        (Knox / Transsion steps there);
    //      - other brands: only Nuovo can lock them -> send a Nuovo lock;
    //        once sent, "Lock sent" until Nuovo reports it locked.
    //   Anything else needs nothing.
    public static class LockCheckRules
    {
        public const int DefaultMinDaysBehind = 7;

        // A balance this small is a rounding difference, not money owed.
        public const decimal PaidTolerance = 1m;

        public static LockCheckBrand Brand(string? make, string? model) =>
            RecoveryRules.IsSamsung(make, model) ? LockCheckBrand.Samsung
            : RecoveryRules.IsTranssion(make) ? LockCheckBrand.Transsion
            : LockCheckBrand.Other;

        // Contract value and what's due to date, as the dashboards work them
        // out (30-day months, due capped at the full term).
        public static decimal ContractValue(LockCheckRow r) =>
            r.Deposit + r.Daily * 30 * r.TermInMonths + r.Weekly * (30m / 7m) * r.TermInMonths + r.Monthly * r.TermInMonths;

        public static decimal DueToDate(LockCheckRow r, DateTime today)
        {
            if (r.StartDate == null) return r.Deposit;
            var days = Math.Max(0, (today.Date - r.StartDate.Value.Date).Days);
            days = (int)Math.Min(days, r.TermInMonths * 30);
            return r.Deposit + r.Daily * days + r.Weekly * (days / 7m) + r.Monthly * (days / 30m);
        }

        public static void Evaluate(LockCheckRow r, DateTime today, int minDaysBehind)
        {
            r.Brand = Brand(r.Make, r.Model);
            r.ContractValue = Math.Round(ContractValue(r), 2);
            r.FullyPaid = r.StartDate != null && r.Paid >= r.ContractValue - PaidTolerance;
            r.Arrears = Math.Max(0, Math.Round(DueToDate(r, today) - r.Paid, 2));
            var perDay = r.Daily + r.Weekly / 7m + r.Monthly / 30m;
            r.DaysBehind = perDay > 0 ? (int)Math.Floor(r.Arrears / perDay) : 0;
            r.Stage = StageOf(r, minDaysBehind);
        }

        public static LockCheckStage StageOf(LockCheckRow r, int minDaysBehind)
        {
            if (r.FullyPaid) return LockCheckStage.FullyPaidOpen;
            if (r.RecoveryId.HasValue) return LockCheckStage.InRecovery;
            if (string.Equals(r.NuovoStatus, "un_registered", StringComparison.OrdinalIgnoreCase)) return LockCheckStage.LostFromNuovo;
            if (r.Locked == true || r.DaysBehind < minDaysBehind) return LockCheckStage.None;
            if (r.Brand != LockCheckBrand.Other) return LockCheckStage.NeedsNewLocker;
            return r.LastAction == LockCheckAction.NuovoLock && r.LastActionSuccess == true
                ? LockCheckStage.LockSent
                : LockCheckStage.NuovoOnly;
        }

        // Which actions each stage offers.
        public static bool CanSendToRecovery(LockCheckStage s) => s is LockCheckStage.NeedsNewLocker or LockCheckStage.LostFromNuovo;
        public static bool CanSendNuovoLock(LockCheckStage s) => s is LockCheckStage.NuovoOnly or LockCheckStage.LockSent or LockCheckStage.LostFromNuovo;
        public static bool CanCloseAndRelease(LockCheckStage s) => s == LockCheckStage.FullyPaidOpen;

        public static string Label(LockCheckStage s) => s switch
        {
            LockCheckStage.NeedsNewLocker => "Needs a new locker",
            LockCheckStage.NuovoOnly => "Nuovo only",
            LockCheckStage.LockSent => "Lock sent",
            LockCheckStage.InRecovery => "In Recovery",
            LockCheckStage.LostFromNuovo => "Lost from Nuovo",
            LockCheckStage.FullyPaidOpen => "Fully paid, still open",
            _ => "OK",
        };

        public static string BrandLabel(LockCheckBrand b) => b switch
        {
            LockCheckBrand.Samsung => "Samsung → Knox",
            LockCheckBrand.Transsion => "itel/TECNO/Infinix → Transsion",
            _ => "Nuovo only",
        };

        public static bool MatchesBrand(LockCheckRow r, string brand) => brand switch
        {
            "samsung" => r.Brand == LockCheckBrand.Samsung,
            "transsion" => r.Brand == LockCheckBrand.Transsion,
            "other" => r.Brand == LockCheckBrand.Other,
            _ => true,
        };
    }
}
